﻿#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using System;
using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Object.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using System.Text;
using FishNet.Serializing.Helping;
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
        private readonly Dictionary<uint, ServerRpcDelegate> _serverRpcDelegates = new();
        /// <summary>
        /// Registered ObserversRpc methods.
        /// </summary>
        private readonly Dictionary<uint, ClientRpcDelegate> _observersRpcDelegates = new();
        /// <summary>
        /// Registered TargetRpc methods.
        /// </summary>
        private readonly Dictionary<uint, ClientRpcDelegate> _targetRpcDelegates = new();
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
        private readonly Dictionary<uint, BufferedRpc> _bufferedRpcs = new();
        /// <summary>
        /// Connections to exclude from RPCs, such as ExcludeOwner or ExcludeServer.
        /// </summary>
        private readonly HashSet<NetworkConnection> _networkConnectionCache = new();
#if DEVELOPMENT
        /// <summary>
        /// Reserved writer for debug.
        /// </summary>
        private static readonly ReservedLengthWriter _reservedLengthWriter = new();
#endif
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
        internal void ReadServerRpc(bool fromRpcLink, uint methodHash, PooledReader reader, NetworkConnection sendingClient, Channel channel)
        {
            if (!fromRpcLink)
                methodHash = ReadRpcHash(reader);

            if (sendingClient == null)
            {
                _networkObjectCache.NetworkManager.LogError($"NetworkConnection is null. ServerRpc {methodHash} on object {gameObject.name} [id {ObjectId}] will not complete. Remainder of packet may become corrupt.");
                return;
            }

            if (_serverRpcDelegates.TryGetValueIL2CPP(methodHash, out ServerRpcDelegate data))
                data.Invoke(reader, channel, sendingClient);
            else
                _networkObjectCache.NetworkManager.LogError($"ServerRpc not found for hash {methodHash} on object {gameObject.name} [id {ObjectId}]. Remainder of packet may become corrupt.");
        }

        /// <summary>
        /// Called when an ObserversRpc is received.
        /// </summary>
        internal void ReadObserversRpc(bool fromRpcLink, uint methodHash, PooledReader reader, Channel channel)
        {
            if (!fromRpcLink)
                methodHash = ReadRpcHash(reader);

            if (_observersRpcDelegates.TryGetValueIL2CPP(methodHash, out ClientRpcDelegate del))
                del.Invoke(reader, channel);
            else
                _networkObjectCache.NetworkManager.LogError($"ObserversRpc not found for hash {methodHash} on object {gameObject.name} [id {ObjectId}] . Remainder of packet may become corrupt.");
        }

        /// <summary>
        /// Called when an TargetRpc is received.
        /// </summary>
        internal void ReadTargetRpc(bool fromRpcLink, uint methodHash, PooledReader reader, Channel channel)
        {
            if (!fromRpcLink)
                methodHash = ReadRpcHash(reader);

            if (_targetRpcDelegates.TryGetValueIL2CPP(methodHash, out ClientRpcDelegate del))
                del.Invoke(reader, channel);
            else
                _networkObjectCache.NetworkManager.LogError($"TargetRpc not found for hash {methodHash} on object {gameObject.name} [id {ObjectId}] . Remainder of packet may become corrupt.");
        }

        /// <summary>
        /// Sends a RPC to server.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        [MakePublic]
        internal void SendServerRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType)
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
        [MakePublic]
        internal void SendObserversRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType, bool bufferLast, bool excludeServer, bool excludeOwner)
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
                _bufferedRpcs[hash] = new(writer, orderType);
            }
            //If not buffered then dispose immediately.
            else
            {
                writer.StoreLength();
            }

            PooledWriter lCreateRpc(Channel c)
            {
#if DEVELOPMENT
                if (!NetworkManager.DebugManager.DisableObserversRpcLinks && _rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
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
        [MakePublic]
        internal void SendTargetRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType, NetworkConnection target, bool excludeServer, bool validateTarget = true)
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
            if (!NetworkManager.DebugManager.DisableTargetRpcLinks && _rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
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
        private PooledWriter CreateRpc(uint hash, PooledWriter methodWriter, PacketId packetId, Channel channel)
        {
            int rpcHeaderBufferLength = GetEstimatedRpcHeaderLength();
            int methodWriterLength = methodWriter.Length;
            //Writer containing full packet.
            PooledWriter writer = WriterPool.Retrieve(rpcHeaderBufferLength + methodWriterLength);
            writer.WritePacketIdUnpacked(packetId);

#if DEVELOPMENT
            WriteDebugForValidateRpc(_reservedLengthWriter, writer, packetId, hash);
#endif

            writer.WriteNetworkBehaviour(this);

            //Only write length if reliable.
            if (channel == Channel.Reliable)
                writer.WriteInt32(methodWriterLength + _rpcHashSize);

            //Hash and data.
            WriteRpcHash(hash, writer);
            writer.WriteArraySegment(methodWriter.GetArraySegment());

#if DEVELOPMENT
            WriteDebugLengthForValidateRpc(_reservedLengthWriter);
#endif

            return writer;
        }

#if DEVELOPMENT
        /// <summary>
        /// Gets the method name for a Rpc using packetId and Rpc hash.
        /// </summary>
        private string GetRpcMethodName(PacketId packetId, uint hash)
        {
            try
            {
                if (packetId == PacketId.ObserversRpc)
                    return _observersRpcDelegates[hash].Method.Name;
                else if (packetId == PacketId.TargetRpc)
                    return _targetRpcDelegates[hash].Method.Name;
                else if (packetId == PacketId.ServerRpc)
                    return _serverRpcDelegates[hash].Method.Name;
                else if (packetId == PacketId.Replicate)
                    return _replicateRpcDelegates[hash].Method.Name;
                else if (packetId == PacketId.Reconcile)
                    return _reconcileRpcDelegates[hash].Method.Name;
                else
                    _networkObjectCache.NetworkManager.LogError($"Unhandled packetId of {packetId} for hash {hash}.");
            }
            //This should not ever happen.
            catch
            {
                _networkObjectCache.NetworkManager.LogError($"Rpc method name not found for packetId {packetId}, hash {hash}.");
            }

            return "Error";
        }
#endif

        /// <summary>
        /// Writes rpcHash to writer.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="writer"></param>
        private void WriteRpcHash(uint hash, PooledWriter writer)
        {
            if (_rpcHashSize == 1)
                writer.WriteUInt8Unpacked((byte)hash);
            else
                writer.WriteUInt16((byte)hash);
        }

#if DEVELOPMENT

        private void WriteDebugForValidateRpc(ReservedLengthWriter reservedLengthWriter, Writer writer, PacketId packetId, uint hash)
        {
            bool validateRpcLengths = _networkObjectCache.NetworkManager.DebugManager.ValidateRpcLengths;

            if (validateRpcLengths)
            {
                string txt = $"NetworkObject Details: {_networkObjectCache.ToString()}. NetworkBehaviour Details: Name [{GetType().Name}]. Rpc Details: Name [{GetRpcMethodName(packetId, hash)}] PacketId [{packetId}] Hash [{hash}]";
                writer.WriteString(txt);
                reservedLengthWriter.Initialize(writer, reservedBytes: 4);
            }
        }

        private void WriteDebugLengthForValidateRpc(ReservedLengthWriter reservedLengthWriter)
        {
            if (_networkObjectCache.NetworkManager.DebugManager.ValidateRpcLengths)
                reservedLengthWriter.WriteLength();
        }

        /// <summary>
        /// Parses written data used to validate a Rpc packet.
        /// </summary>
        internal static void ReadDebugForValidatedRpc(NetworkManager manager, PooledReader reader, out int startReaderRemaining, out string rpcInformation, out uint expectedReadAmount)
        {
            rpcInformation = null;
            expectedReadAmount = 0;

            if (manager.DebugManager.ValidateRpcLengths)
            {
                rpcInformation = reader.ReadStringAllocated();
                expectedReadAmount = ReservedLengthWriter.ReadLength(reader, reservedBytes: 4);
            }

            startReaderRemaining = reader.Remaining;
        }

        /// <summary>
        /// Prints an error if an Rpc packet did not validate correctly.
        /// </summary>
        /// <returns>True if an error occurred.</returns>
        internal static bool TryPrintDebugForValidatedRpc(bool fromRpcLink, NetworkManager manager, PooledReader reader, int startReaderRemaining, string rpcInformation, uint expectedReadAmount, Channel channel)
        {
            if (manager.DebugManager.ValidateRpcLengths)
            {
                int readAmount = (startReaderRemaining - reader.Remaining);
                if (readAmount != expectedReadAmount)
                {
                    string src = (fromRpcLink) ? "RpcLink" : "Rpc";
                    string msg = $"A {src} read an incorrect amount of data on channel {channel}. Read length was {readAmount}, expected length is {expectedReadAmount}. {rpcInformation}." +
                                 $" {manager.PacketIdHistory.GetReceivedPacketIds(packetsFromServer: (reader.Source == Reader.DataSource.Server))}.";
                    manager.LogError(msg);

                    return true;
                }
            }

            return false;
        }
#endif
    }
}