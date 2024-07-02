#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Transporting;
using FishNet.Object.Delegating;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object
{


    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Types.
        private struct BufferedRpc
        {
            /// <summary>
            /// Writer containing the full RPC.
            /// </summary>
            public PooledWriter Writer;
            /// <summary>
            /// Which order to send the data in relation to other packets.
            /// </summary>
            public DataOrderType OrderType;

            public BufferedRpc(PooledWriter writer, DataOrderType orderType)
            {
                Writer = writer;
                OrderType = orderType;
            }
        }

        #endregion

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
        private Dictionary<uint, BufferedRpc> _bufferedRpcs = new Dictionary<uint, BufferedRpc>();
        /// <summary>
        /// Connections to exclude from RPCs, such as ExcludeOwner or ExcludeServer.
        /// </summary>
        private HashSet<NetworkConnection> _networkConnectionCache = new HashSet<NetworkConnection>();
        #endregion

        #region Const.
        /// <summary>
        /// This is an estimated value of what the maximum possible size of a RPC could be.
        /// Realistically this value is much smaller but this value is used as a buffer.
        /// </summary>
        private const int MAXIMUM_RPC_HEADER_SIZE = 10;
        #endregion

        /// <summary>
        /// Called when buffered RPCs should be sent.
        /// </summary>
        internal void SendBufferedRpcs(NetworkConnection conn)
        {
            TransportManager tm = _networkObjectCache.NetworkManager.TransportManager;
            foreach (BufferedRpc bRpc in _bufferedRpcs.Values)
                tm.SendToClient((byte)Channel.Reliable, bRpc.Writer.GetArraySegment(), conn, true, bRpc.OrderType);
        }

        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude]
        [MakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RegisterServerRpc(uint hash, ServerRpcDelegate del)
        {
            if (_serverRpcDelegates.TryAdd(hash, del))
                IncreaseRpcMethodCount();
            else
                NetworkManager.LogError($"ServerRpc key {hash} has already been added for {GetType().FullName} on {gameObject.name}");
        }
        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude]
        [MakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RegisterObserversRpc(uint hash, ClientRpcDelegate del)
        {
            if (_observersRpcDelegates.TryAdd(hash, del))
                IncreaseRpcMethodCount();
            else
                NetworkManager.LogError($"ObserversRpc key {hash} has already been added for {GetType().FullName} on {gameObject.name}");
        }
        /// <summary>
        /// Registers a RPC method.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude]
        [MakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RegisterTargetRpc(uint hash, ClientRpcDelegate del)
        {
            if (_targetRpcDelegates.TryAdd(hash, del))
                IncreaseRpcMethodCount();
            else
                NetworkManager.LogError($"TargetRpc key {hash} has already been added for {GetType().FullName} on {gameObject.name}");
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
            foreach (BufferedRpc bRpc in _bufferedRpcs.Values)
                bRpc.Writer.Store();
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
                return reader.ReadUInt8Unpacked();
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
                _networkObjectCache.NetworkManager.LogError($"NetworkConnection is null. ServerRpc {methodHash} on object {gameObject.name} [id {ObjectId}] will not complete. Remainder of packet may become corrupt.");
                return;
            }

            if (_serverRpcDelegates.TryGetValueIL2CPP(methodHash, out ServerRpcDelegate data))
                data.Invoke(reader, channel, sendingClient);
            else
                _networkObjectCache.NetworkManager.LogWarning($"ServerRpc not found for hash {methodHash} on object {gameObject.name} [id {ObjectId}]. Remainder of packet may become corrupt.");
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
                del.Invoke(reader, channel);
            else
                _networkObjectCache.NetworkManager.LogWarning($"ObserversRpc not found for hash {methodHash.Value} on object {gameObject.name} [id {ObjectId}] . Remainder of packet may become corrupt.");
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
                del.Invoke(reader, channel);
            else
                _networkObjectCache.NetworkManager.LogWarning($"TargetRpc not found for hash {methodHash.Value} on object {gameObject.name} [id {ObjectId}] . Remainder of packet may become corrupt.");
        }

        /// <summary>
        /// Sends a RPC to server.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        [MakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void SendServerRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType)
        {
            if (!IsSpawnedWithWarning())
                return;
            _transportManagerCache.CheckSetReliableChannel(methodWriter.Length + MAXIMUM_RPC_HEADER_SIZE, ref channel);

            PooledWriter writer = CreateRpc(hash, methodWriter, PacketId.ServerRpc, channel);
            _networkObjectCache.NetworkManager.TransportManager.SendToServer((byte)channel, writer.GetArraySegment(), true, orderType);
            writer.StoreLength();
        }


        /// <summary>
        /// Sends a RPC to observers.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        [APIExclude]
        [MakePublic] //Make internal.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void SendObserversRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType, bool bufferLast, bool excludeServer, bool excludeOwner)
        {
            if (!IsSpawnedWithWarning())
                return;
            _transportManagerCache.CheckSetReliableChannel(methodWriter.Length + MAXIMUM_RPC_HEADER_SIZE, ref channel);

            PooledWriter writer = lCreateRpc(channel);
            SetNetworkConnectionCache(excludeServer, excludeOwner);
            _networkObjectCache.NetworkManager.TransportManager.SendToClients((byte)channel, writer.GetArraySegment(), _networkObjectCache.Observers, _networkConnectionCache, true, orderType);

            /* If buffered then dispose of any already buffered
             * writers and replace with new one. Writers should
             * automatically dispose when references are lost
             * anyway but better safe than sorry. */
            if (bufferLast)
            {
                if (_bufferedRpcs.TryGetValueIL2CPP(hash, out BufferedRpc result))
                    result.Writer.StoreLength();

                /* If sent on unreliable the RPC has to be rebuilt for
                 * reliable headers since buffered RPCs always send reliably
                 * to new connections. */
                if (channel == Channel.Unreliable)
                {
                    writer.StoreLength();
                    writer = lCreateRpc(Channel.Reliable);
                }
                _bufferedRpcs[hash] = new BufferedRpc(writer, orderType);
            }
            //If not buffered then dispose immediately.
            else
            {
                writer.StoreLength();
            }

            PooledWriter lCreateRpc(Channel c)
            {
#if DEVELOPMENT
                if (NetworkManager.DebugManager.ObserverRpcLinks && _rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#else
            if (_rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#endif
                    writer = CreateLinkedRpc(link, methodWriter, c);
                else
                    writer = CreateRpc(hash, methodWriter, PacketId.ObserversRpc, c);

                return writer;
            }
        }

        /// <summary>
        /// Sends a RPC to target.
        /// </summary>
        [MakePublic] //Make internal.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void SendTargetRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType, NetworkConnection target, bool excludeServer, bool validateTarget = true)
        {
            if (!IsSpawnedWithWarning())
                return;

            _transportManagerCache.CheckSetReliableChannel(methodWriter.Length + MAXIMUM_RPC_HEADER_SIZE, ref channel);

            if (validateTarget)
            {
                if (target == null)
                {
                    _networkObjectCache.NetworkManager.LogWarning($"Action cannot be completed as no Target is specified.");
                    return;
                }
                else
                {
                    //If target is not an observer.
                    if (!_networkObjectCache.Observers.Contains(target))
                    {
                        _networkObjectCache.NetworkManager.LogWarning($"Action cannot be completed as Target is not an observer for object {gameObject.name} [id {ObjectId}].");
                        return;
                    }
                }
            }

            //Excluding server.
            if (excludeServer && target.IsLocalClient)
                return;

            PooledWriter writer;

#if DEVELOPMENT
            if (NetworkManager.DebugManager.TargetRpcLinks && _rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#else
            if (_rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#endif
                writer = CreateLinkedRpc(link, methodWriter, channel);
            else
                writer = CreateRpc(hash, methodWriter, PacketId.TargetRpc, channel);

            _networkObjectCache.NetworkManager.TransportManager.SendToClient((byte)channel, writer.GetArraySegment(), target, true, orderType);
            writer.Store();
        }

        /// <summary>
        /// Adds excluded connections to ExcludedRpcConnections.
        /// </summary>
        private void SetNetworkConnectionCache(bool addClientHost, bool addOwner)
        {
            _networkConnectionCache.Clear();
            if (addClientHost && IsClientStarted)
                _networkConnectionCache.Add(LocalConnection);
            if (addOwner && Owner.IsValid)
                _networkConnectionCache.Add(Owner);
        }


        /// <summary>
        /// Returns if spawned and throws a warning if not.
        /// </summary>
        /// <returns></returns>
        private bool IsSpawnedWithWarning()
        {
            bool result = this.IsSpawned;
            if (!result)
                _networkObjectCache.NetworkManager.LogWarning($"Action cannot be completed as object {gameObject.name} [Id {ObjectId}] is not spawned.");

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
            PooledWriter writer = WriterPool.Retrieve(rpcHeaderBufferLength + methodWriterLength);
            writer.WritePacketIdUnpacked(packetId);
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
                writer.WriteUInt8Unpacked((byte)hash);
            else
                writer.WriteUInt16((byte)hash);
        }
    }


}