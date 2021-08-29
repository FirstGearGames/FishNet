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
        #endregion

        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="del"></param>
        protected internal void CreateServerRpcDelegate(uint rpcHash, ServerRpcDelegate del)
        {
            _serverRpcDelegates[rpcHash] = del;
        }
        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="del"></param>
        protected internal void CreateObserversRpcDelegate(uint rpcHash, ClientRpcDelegate del)
        {
            _observersRpcDelegates[rpcHash] = del;
        }
        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="del"></param>
        protected internal void CreateTargetRpcDelegate(uint rpcHash, ClientRpcDelegate del)
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
            PooledWriter writer = CreateRpc(rpcHash, methodWriter, PacketId.ServerRpc);
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
        public void SendObserversRpc(uint rpcHash, PooledWriter methodWriter, Channel channel)
        {
            PooledWriter writer = CreateRpc(rpcHash, methodWriter, PacketId.ObserversRpc);
            NetworkObject.NetworkManager.TransportManager.SendToClients((byte)channel, writer.GetArraySegment(), NetworkObject.Observers);

            writer.Dispose();
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

            PooledWriter writer = CreateRpc(rpcHash, methodWriter, PacketId.TargetRpc);
            NetworkObject.NetworkManager.TransportManager.SendToClient((byte)channel, writer.GetArraySegment(), target);
            writer.Dispose();
        }

        /// <summary>
        /// Creates a PooledWriter and writes the header for a rpc.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="rpcHash"></param>
        /// <param name="packetId"></param>
        private PooledWriter CreateRpc(uint rpcHash, PooledWriter methodWriter, PacketId packetId)
        {
            //Writer containing object data.
            PooledWriter objectWriter = WriterPool.GetWriter();
            objectWriter.WriteNetworkBehaviour(this);
            //Writer containing full packet.    
            PooledWriter writer = WriterPool.GetWriter();
            writer.WriteByte((byte)packetId);
            //Length for object, hash, data.
            int packetLengthAfterId = (objectWriter.Length + 4 + methodWriter.Length);
            writer.WriteInt32(packetLengthAfterId);
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