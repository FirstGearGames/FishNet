using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Managing.Transporting;
using FishNet.Object.Delegating;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private uint _rpcMethodCount;
        /// <summary>
        /// Size of every rpcHash for this networkBehaviour.
        /// </summary>
        private byte _rpcHashSize = 1;
        /// <summary>
        /// RPCs buffered for new clients.
        /// </summary>
        private Dictionary<uint, (PooledWriter, Channel)> _bufferedRpcs = new Dictionary<uint, (PooledWriter, Channel)>();
        #endregion

        /// <summary>
        /// Called when buffered RPCs should be sent.
        /// </summary>
        internal void SendBufferedRpcs(NetworkConnection conn)
        {
            TransportManager tm = _networkObjectCache.NetworkManager.TransportManager;
            foreach ((PooledWriter writer, Channel ch) in _bufferedRpcs.Values)
                tm.SendToClient((byte)ch, writer.GetArraySegment(), conn);
        }

        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude]
        [CodegenMakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterServerRpcInternal(uint hash, ServerRpcDelegate del)
        {
            bool contains = _serverRpcDelegates.ContainsKey(hash);
            _serverRpcDelegates[hash] = del;
            if (!contains)
                IncreaseRpcMethodCount();
        }
        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude]
        [CodegenMakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterObserversRpcInternal(uint hash, ClientRpcDelegate del)
        {
            bool contains = _observersRpcDelegates.ContainsKey(hash);
            _observersRpcDelegates[hash] = del;
            if (!contains)
                IncreaseRpcMethodCount();
        }
        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude]
        [CodegenMakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterTargetRpcInternal(uint hash, ClientRpcDelegate del)
        {
            bool contains = _targetRpcDelegates.ContainsKey(hash);
            _targetRpcDelegates[hash] = del;
            if (!contains)
                IncreaseRpcMethodCount();
        }

        /// <summary>
        /// Increases rpcMethodCount and rpcHashSize.
        /// </summary>
        private void IncreaseRpcMethodCount()
        {
            _rpcMethodCount++;
            if (_rpcMethodCount <= byte.MaxValue)
                _rpcHashSize = 1;
            else
                _rpcHashSize = 2;
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
            if (_rpcHashSize == 1)
                return reader.ReadByte();
            else
                return reader.ReadUInt16();
        }
        /// <summary>
        /// Called when a ServerRpc is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnServerRpc(PooledReader reader, NetworkConnection sendingClient, Channel channel)
        {
            uint methodHash = ReadRpcHash(reader);

            if (sendingClient == null)
            {
                if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"NetworkConnection is null. ServerRpc {methodHash} on object {gameObject.name} [id {ObjectId}] will not complete. Remainder of packet may become corrupt.");
                return;
            }

            if (_serverRpcDelegates.TryGetValueIL2CPP(methodHash, out ServerRpcDelegate data))
            {
                data.Invoke(reader, channel, sendingClient);
            }
            else
            {
                if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"ServerRpc not found for hash {methodHash} on object {gameObject.name} [id {ObjectId}]. Remainder of packet may become corrupt.");
            }
        }

        /// <summary>
        /// Called when an ObserversRpc is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnObserversRpc(uint? methodHash, PooledReader reader, Channel channel)
        {
            if (methodHash == null)
                methodHash = ReadRpcHash(reader);

            if (_observersRpcDelegates.TryGetValueIL2CPP(methodHash.Value, out ClientRpcDelegate del))
            {
                del.Invoke(reader, channel);
            }
            else
            {
                if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"ObserversRpc not found for hash {methodHash.Value} on object {gameObject.name} [id {ObjectId}] . Remainder of packet may become corrupt.");
            }
        }

        /// <summary>
        /// Called when an TargetRpc is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnTargetRpc(uint? methodHash, PooledReader reader, Channel channel)
        {
            if (methodHash == null)
                methodHash = ReadRpcHash(reader);

            if (_targetRpcDelegates.TryGetValueIL2CPP(methodHash.Value, out ClientRpcDelegate del))
            {
                del.Invoke(reader, channel);
            }
            else
            {
                if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"TargetRpc not found for hash {methodHash.Value} on object {gameObject.name} [id {ObjectId}] . Remainder of packet may become corrupt.");
            }
        }

        /// <summary>
        /// Sends a RPC to server.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        [CodegenMakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendServerRpcInternal(uint hash, PooledWriter methodWriter, Channel channel)
        {
            if (!IsSpawnedWithWarning())
                return;

            PooledWriter writer = CreateRpc(hash, methodWriter, PacketId.ServerRpc, channel);
            _networkObjectCache.NetworkManager.TransportManager.SendToServer((byte)channel, writer.GetArraySegment());
            writer.DisposeLength();
        }

        /// <summary>
        /// Sends a RPC to observers.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        [APIExclude]
        [CodegenMakePublic] //Make internal.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendObserversRpcInternal(uint hash, PooledWriter methodWriter, Channel channel, bool buffered)
        {
            if (!IsSpawnedWithWarning())
                return;

            PooledWriter writer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (NetworkManager.DebugManager.ObserverRpcLinks && _rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#else
            if (_rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#endif
                writer = CreateLinkedRpc(link, methodWriter, channel);
            else
                writer = CreateRpc(hash, methodWriter, PacketId.ObserversRpc, channel);

            _networkObjectCache.NetworkManager.TransportManager.SendToClients((byte)channel, writer.GetArraySegment(), _networkObjectCache.Observers);
            /* If buffered then dispose of any already buffered
             * writers and replace with new one. Writers should
             * automatically dispose when references are lost
             * anyway but better safe than sorry. */
            if (buffered)
            {
                if (_bufferedRpcs.TryGetValueIL2CPP(hash, out (PooledWriter pw, Channel ch) result))
                    result.pw.DisposeLength();
                _bufferedRpcs[hash] = (writer, channel);
            }
            //If not buffered then dispose immediately.
            else
            {
                writer.DisposeLength();
            }
        }

        /// <summary>
        /// Sends a RPC to target.
        /// </summary>
        [CodegenMakePublic] //Make internal.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendTargetRpcInternal(uint hash, PooledWriter methodWriter, Channel channel, NetworkConnection target, bool validateTarget = true)
        {
            if (!IsSpawnedWithWarning())
                return;

            if (validateTarget)
            {
                if (target == null)
                {
                    if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Action cannot be completed as no Target is specified.");
                    return;
                }
                else
                {
                    //If target is not an observer.
                    if (!_networkObjectCache.Observers.Contains(target))
                    {
                        if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Warning))
                            Debug.LogWarning($"Action cannot be completed as Target is not an observer for object {gameObject.name} [id {ObjectId}].");
                        return;
                    }
                }
            }

            PooledWriter writer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (NetworkManager.DebugManager.TargetRpcLinks && _rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#else
            if (_rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#endif
                writer = CreateLinkedRpc(link, methodWriter, channel);
            else
                writer = CreateRpc(hash, methodWriter, PacketId.TargetRpc, channel);

            _networkObjectCache.NetworkManager.TransportManager.SendToClient((byte)channel, writer.GetArraySegment(), target);
            writer.DisposeLength();
        }


        /// <summary>
        /// Returns if spawned and throws a warning if not.
        /// </summary>
        /// <returns></returns>
        private bool IsSpawnedWithWarning()
        {
            bool result = this.IsSpawned;
            if (!result)
            {
                if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Action cannot be completed as object {gameObject.name} [Id {ObjectId}] is not spawned.");
            }

            return result;
        }

        /// <summary>
        /// Writes a full RPC and returns the writer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PooledWriter CreateRpc(uint hash, PooledWriter methodWriter, PacketId packetId, Channel channel)
        {
            int rpcHeaderBufferLength = GetEstimatedRpcHeaderLength();
            int methodWriterLength = methodWriter.Length;
            //Writer containing full packet.
            PooledWriter writer = WriterPool.GetWriter(rpcHeaderBufferLength + methodWriterLength);
            writer.WritePacketId(packetId);
            writer.WriteNetworkBehaviour(this);
            //Only write length if reliable.
            if (channel == Channel.Reliable)
                writer.WriteLength(methodWriterLength + _rpcHashSize);
            //Hash and data.
            WriteRpcHash(hash, writer);
            writer.WriteArraySegment(methodWriter.GetArraySegment());
            return writer;
        }

        /// <summary>
        /// Writes rpcHash to writer.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="writer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteRpcHash(uint hash, PooledWriter writer)
        {
            if (_rpcHashSize == 1)
                writer.WriteByte((byte)hash);
            else
                writer.WriteUInt16((byte)hash);
        }
    }


}