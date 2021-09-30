using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object
{


    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Registered ServerRpc methods.
        /// </summary>
        private readonly Dictionary<uint, ServerRpcDelegate> _serverRpcDelegates = new Dictionary<uint, ServerRpcDelegate>();
        /// <summary>
        /// Registered ObserversRpc methods.
        /// </summary>
        private readonly Dictionary<uint, ClientRpcDelegate> _observersRpcDelegates = new Dictionary<uint, ClientRpcDelegate>();
        /// <summary>
        /// Registered TargetRpc methods.
        /// </summary>
        private readonly Dictionary<uint, ClientRpcDelegate> _targetRpcDelegates = new Dictionary<uint, ClientRpcDelegate>();
        /// <summary>
        /// Number of total RPC methods for scripts in the same inheritance tree for this instance.
        /// </summary>
        private ushort _rpcMethodCount = 0;
        /// <summary>
        /// RPCs buffered for new clients.
        /// </summary>
        private Dictionary<uint, (PooledWriter, Channel)> _bufferedRpcs = new Dictionary<uint, (PooledWriter, Channel)>();
        /// <summary>
        /// True if a buffered rpc has been registered.
        /// </summary>
        private bool _usesBufferedRpcs = false;
        #endregion

        /// <summary>
        /// Preinitializes RPCs.
        /// </summary>
        /// <param name="networkObject"></param>
        private void PreInitializeRpcs(NetworkObject networkObject)
        {
            if (_usesBufferedRpcs)
                networkObject.OnSendBufferedRpcs += NetworkObject_OnSendBufferedRpcs;
        }

        /// <summary>
        /// Called when buffered RPCs should be sent.
        /// </summary>
        private void NetworkObject_OnSendBufferedRpcs(NetworkConnection conn)
        {
            foreach ((PooledWriter writer, Channel ch) in _bufferedRpcs.Values)
                NetworkObject.NetworkManager.TransportManager.SendToClient((byte)ch, writer.GetArraySegment(), conn);
        }

        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="del"></param>
        protected internal void RegisterServerRpc(uint rpcHash, ServerRpcDelegate del)
        {
            _serverRpcDelegates[rpcHash] = del;
        }
        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="del"></param>
        protected internal void RegisterObserversRpc(uint rpcHash, ClientRpcDelegate del, bool buffered)
        {
            _observersRpcDelegates[rpcHash] = del;
            _usesBufferedRpcs |= buffered;
        }
        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="del"></param>
        protected internal void RegisterTargetRpc(uint rpcHash, ClientRpcDelegate del)
        {
            _targetRpcDelegates[rpcHash] = del;
        }

        /// <summary>
        /// Sets number of RPCs for scripts in the same inheritance tree as this NetworkBehaviour.
        /// </summary>
        /// <param name="count"></param>
        protected internal void SetRpcMethodCount(ushort count)
        {
            _rpcMethodCount = count;
        }

        /// <summary>
        /// Clears all buffered RPCs for this NetworkBehaviour.
        /// </summary>
        public void ClearBuffedRpcs()
        {
            foreach ((PooledWriter writer, Channel _) in _bufferedRpcs.Values)
                writer.Dispose();
            _bufferedRpcs.Clear();
        }

        /// <summary>
        /// Reads a RPC hash.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private uint ReadRpcHash(PooledReader reader)
        {
            ///* If more than 255 rpc methods then read a ushort,
            //* otherwise read a byte. */
            //ushort methodHash = (_rpcMethodCount > byte.MaxValue) ?
            //    reader.ReadUInt16() : reader.ReadByte();

            return reader.ReadUInt32(AutoPackType.Unpacked);
        }
        /// <summary>
        /// Called when a ServerRpc is received.
        /// </summary>
        internal void OnServerRpc(PooledReader reader, NetworkConnection senderClient)
        {
            uint methodHash = ReadRpcHash(reader);

            if (senderClient == null)
            {
                Debug.LogError($"NetworkConnection is null. ServerRpc {methodHash} will not complete.");
                return;
            }

            if (_serverRpcDelegates.TryGetValue(methodHash, out ServerRpcDelegate del))
                del.Invoke(this, reader, senderClient);
            else
                Debug.LogWarning($"ServerRpc not found for hash {methodHash}.");
        }

        /// <summary>
        /// Called when an ObserversRpc is received.
        /// </summary>
        internal void OnObserversRpc(PooledReader reader)
        {
            uint methodHash = ReadRpcHash(reader);

            if (_observersRpcDelegates.TryGetValue(methodHash, out ClientRpcDelegate del))
                del.Invoke(this, reader);
            else
                Debug.LogWarning($"ObserversRpc not found for hash {methodHash}.");
        }

        /// <summary>
        /// Called when an TargetRpc is received.
        /// </summary>
        internal void OnTargetRpc(PooledReader reader)
        {
            uint methodHash = ReadRpcHash(reader);

            if (_targetRpcDelegates.TryGetValue(methodHash, out ClientRpcDelegate del))
                del.Invoke(this, reader);
            else
                Debug.LogWarning($"TargetRpc not found for hash {methodHash}.");
        }

        /// <summary>
        /// Sends a RPC to server.
        /// Internal use.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        public void SendServerRpc(uint rpcHash, PooledWriter methodWriter, Channel channel)
        {
            if (!IsSpawnedWithWarning())
                return;

            PooledWriter writer = CreateRpc(rpcHash, methodWriter, PacketId.ServerRpc, channel);
            NetworkObject.NetworkManager.TransportManager.SendToServer((byte)channel, writer.GetArraySegment());
            writer.Dispose();
        }
        /// <summary>
        /// Sends a RPC to observers.
        /// Internal use.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        public void SendObserversRpc(uint rpcHash, PooledWriter methodWriter, Channel channel, bool buffered)
        {
            if (!IsSpawnedWithWarning())
                return;

            PooledWriter writer = CreateRpc(rpcHash, methodWriter, PacketId.ObserversRpc, channel);
            NetworkObject.NetworkManager.TransportManager.SendToClients((byte)channel, writer.GetArraySegment(), NetworkObject.Observers);

            /* If buffered then dispose of any already buffered
             * writers and replace with new one. Writers should
             * automatically dispose when references are lost
             * anyway but better safe than sorry. */
            if (buffered)
            {
                if (_bufferedRpcs.TryGetValue(rpcHash, out (PooledWriter pw, Channel ch) result))
                    result.pw.Dispose();
                _bufferedRpcs[rpcHash] = (writer, channel);
            }
            //If not buffered then dispose immediately.
            else
            {
                writer.Dispose();
            }
        }

        /// <summary>
        /// Sends a RPC to target.
        /// Internal use.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        /// <param name="target"></param>
        public void SendTargetRpc(uint rpcHash, PooledWriter methodWriter, Channel channel, NetworkConnection target)
        {
            if (!IsSpawnedWithWarning())
                return;

            /* These checks could be codegened in to save a very very small amount of performance
             * by performing them before the serializer is written, but the odds of these failing
             * are very low and I'd rather keep the complexity out of codegen. */
            if (target == null)
            {
                Debug.LogWarning($"Action cannot be completed as no Target is specified.");
                return;
            }
            else
            {
                /* If not using observers, sending to owner,
                 * or observers contains target. */
                //bool canSendTotarget = (!NetworkObject.UsingObservers ||
                //    NetworkObject.OwnerId == target.ClientId ||
                //    NetworkObject.Observers.Contains(target));
                bool canSendTotarget = NetworkObject.OwnerId == target.ClientId || NetworkObject.Observers.Contains(target);

                if (!canSendTotarget)
                {
                    Debug.LogWarning($"Action cannot be completed as Target is not an observer for object {gameObject.name}");
                    return;
                }
            }

            PooledWriter writer = CreateRpc(rpcHash, methodWriter, PacketId.TargetRpc, channel);
            NetworkObject.NetworkManager.TransportManager.SendToClient((byte)channel, writer.GetArraySegment(), target);
            writer.Dispose();
        }


        /// <summary>
        /// Returns if spawned and throws a warning if not.
        /// </summary>
        /// <returns></returns>
        private bool IsSpawnedWithWarning()
        {
            bool result = this.IsSpawned;
            if (!result)
                Debug.LogWarning($"Action cannot be completed as object {gameObject.name} is not spawned.");

            return result;
        }

        /// <summary>
        /// Creates a PooledWriter and writes the header for a rpc.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcHash"></param>
        /// <param name="packetId"></param>
        private PooledWriter CreateRpc(uint rpcHash, PooledWriter methodWriter, PacketId packetId, Channel channel)
        {
            //Writer containing object data.
            PooledWriter objectWriter = WriterPool.GetWriter();
            objectWriter.WriteNetworkBehaviour(this);
            //Writer containing full packet.    
            PooledWriter writer = WriterPool.GetWriter();
            writer.WriteByte((byte)packetId);

            //Only write length if unreliable.
            if (channel == Channel.Unreliable)
            {
                //Length for object, hash, data.
                int packetLengthAfterId = (objectWriter.Length + 4 + methodWriter.Length);
                writer.WriteInt32(packetLengthAfterId);
            }

            //Write object information.
            writer.WriteArraySegment(objectWriter.GetArraySegment());
            //Hash.
            writer.WriteUInt32(rpcHash, AutoPackType.Unpacked);
            //Data.
            writer.WriteArraySegment(methodWriter.GetArraySegment());

            objectWriter.Dispose();
            return writer;


            //           /* If more than 255 rpc methods then write a ushort,
            //* otherwise write a byte. */
            //           if (_rpcMethodCount > byte.MaxValue)
            //               writer.WriteUInt32(rpcHash);
            //           else
            //               writer.WriteByte((byte)rpcHash);

        }

    }


}