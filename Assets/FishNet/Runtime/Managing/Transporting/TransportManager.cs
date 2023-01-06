﻿using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Transporting
{

    /// <summary>
    /// Communicates with the Transport to send and receive data.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/TransportManager")]
    public sealed partial class TransportManager : MonoBehaviour
    {
        #region Types.
        private struct DisconnectingClient
        {
            public uint Tick;
            public NetworkConnection Connection;

            public DisconnectingClient(uint tick, NetworkConnection connection)
            {
                Tick = tick;
                Connection = connection;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Called before IterateOutgoing has started.
        /// </summary>
        internal event Action OnIterateOutgoingStart;
        /// <summary>
        /// Called after IterateOutgoing has completed.
        /// </summary>
        internal event Action OnIterateOutgoingEnd;
        /// <summary>
        /// Called before IterateIncoming has started. True for on server, false for on client.
        /// </summary>
        internal event Action<bool> OnIterateIncomingStart;
        /// <summary>
        /// Called after IterateIncoming has completed. True for on server, false for on client.
        /// </summary>
        internal event Action<bool> OnIterateIncomingEnd;
        /// <summary>
        /// The current Transport being used.
        /// </summary>
        [Tooltip("The current Transport being used.")]
        public Transport Transport;
        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Latency simulation settings.")]
        [SerializeField]
        private LatencySimulator _latencySimulator = new LatencySimulator();
        /// <summary>
        /// Latency simulation settings.
        /// </summary>
        public LatencySimulator LatencySimulator
        {
            get
            {
                //Shouldn't ever be null unless the user nullifies it.
                if (_latencySimulator == null)
                    _latencySimulator = new LatencySimulator();
                return _latencySimulator;
            }
        }
        #endregion

        #region Private.
        /// <summary>
        /// NetworkConnections on the server which have to send data to clients.
        /// </summary>
        private List<NetworkConnection> _dirtyToClients = new List<NetworkConnection>();
        /// <summary>
        /// PacketBundles to send to the server.
        /// </summary>
        private List<PacketBundle> _toServerBundles = new List<PacketBundle>();
        /// <summary>
        /// NetworkManager handling this TransportManager.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Clients which are pending disconnects.
        /// </summary>
        private List<DisconnectingClient> _disconnectingClients = new List<DisconnectingClient>();
        #endregion

        #region Consts.
        /// <summary>
        /// Number of bytes sent for PacketId.
        /// </summary>
        public const byte PACKET_ID_BYTES = 2;
        /// <summary>
        /// Number of bytes sent for ObjectId.
        /// </summary>
        public const byte OBJECT_ID_BYTES = 2;
        /// <summary>
        /// Number of bytes sent for ComponentIndex.
        /// </summary>
        public const byte COMPONENT_INDEX_BYTES = 1;
        /// <summary>
        /// Number of bytes sent for Tick.
        /// </summary>
        public const byte TICK_BYTES = 4;
        /// <summary>
        /// Number of bytes sent to indicate split count.
        /// </summary>
        private const byte SPLIT_COUNT_BYTES = 4;
        /// <summary>
        /// Number of bytes required for split data. 
        /// </summary>
        public const byte SPLIT_INDICATOR_SIZE = (PACKET_ID_BYTES + SPLIT_COUNT_BYTES);
        /// <summary>
        /// Number of channels supported.
        /// </summary>
        public const byte CHANNEL_COUNT = 2;
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        internal void InitializeOnceInternal(NetworkManager manager)
        {
            _networkManager = manager;
            /* If transport isn't specified then add default
             * transport. */
            if (Transport == null && !gameObject.TryGetComponent<Transport>(out Transport))
                Transport = gameObject.AddComponent<FishNet.Transporting.Tugboat.Tugboat>();

            Transport.Initialize(_networkManager, 0);
            InitializeToServerBundles();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _latencySimulator.Initialize(manager, Transport);
#endif
        }

        /// <summary>
        /// Sets a connection from server to client dirty.
        /// </summary>
        /// <param name="conn"></param>
        internal void ServerDirty(NetworkConnection conn)
        {
            _dirtyToClients.Add(conn);
        }

        /// <summary>
        /// Initializes ToServerBundles for use.
        /// </summary>
        private void InitializeToServerBundles()
        {
            /* For ease of use FishNet will always have
             * only two channels, reliable and unreliable. 
             * Even if the transport only supports reliable
             * also setup for unreliable. */
            for (byte i = 0; i < CHANNEL_COUNT; i++)
            {
                int mtu = Transport.GetMTU(i);
                _toServerBundles.Add(new PacketBundle(_networkManager, mtu));
            }
        }

        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="connection">Connection to send to. Use null for all clients.</param>
        /// <param name="splitLargeMessages">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, NetworkConnection connection, bool splitLargeMessages = true)
        {
            if (connection == null)
                return;

            //Split is needed.
            if (splitLargeMessages && SplitRequired(channelId, segment.Count, out int requiredMessages, out int maxMessageSize))
                SendSplitData(connection, ref segment, requiredMessages, maxMessageSize);
            //Split not needed.
            else
                connection.SendToClient(channelId, segment);
        }

        /// <summary>
        /// Sends data to observers.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <param name="observers"></param>
        /// <param name="splitLargeIntoReliable">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        internal void SendToClients(byte channelId, ArraySegment<byte> segment, HashSet<NetworkConnection> observers, bool splitLargeIntoReliable = true)
        {
            foreach (NetworkConnection conn in observers)
                SendToClient(channelId, segment, conn, splitLargeIntoReliable);
        }

        /// <summary>
        /// Sends data to all clients if networkObject has no observers, otherwise sends to observers.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="nob">NetworkObject being used to send data.</param>
        /// <param name="splitLargeMessages">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        internal void SendToClients(byte channelId, ArraySegment<byte> segment, NetworkObject networkObject, bool excludeOwner = false, bool splitLargeMessages = true)
        {
            if (!excludeOwner)
            {
                SendToClients(channelId, segment, networkObject.Observers, splitLargeMessages);
            }
            else
            {
                foreach (NetworkConnection conn in networkObject.Observers)
                {
                    if (conn != networkObject.Owner)
                        SendToClient(channelId, segment, conn, splitLargeMessages);
                }
            }
        }

        /// <summary>
        /// Sends data to all clients.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="splitLargeIntoReliable">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        internal void SendToClients(byte channelId, ArraySegment<byte> segment, bool splitLargeMessages = true)
        {
            /* To ensure proper order everything must be tossed into each
             * NetworkConnection rather than batch send. This is because there
             * is no way to know if batch send must iterate before or
             * after connection sends. By sending to each connection order
             * is maintained. */
            foreach (NetworkConnection conn in _networkManager.ServerManager.Clients.Values)
                SendToClient(channelId, segment, conn, splitLargeMessages);
        }


        /// <summary>
        /// Sends data to the server.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendToServer(byte channelId, ArraySegment<byte> segment, bool splitMessages = true)
        {
            if (channelId >= _toServerBundles.Count)
                channelId = (byte)Channel.Reliable;

            //Split is needed.
            if (splitMessages && SplitRequired(channelId, segment.Count, out int requiredMessages, out int maxMessageSize))
            {
                //Client is not allowed to send data sizes beyond MTU.
                //if (_networkManager.ServerManager.LimitClientMTU) //todo uncomment and finish
                //{
                //    if (_networkManager.CanLog(LoggingType.Error))
                //        Debug.LogError($"Local client attempted to send a packet size of {segment.Count} which would exceed the MTU, and settings do not allow this. To allow clients to send packets beyond MTU add a ServerManager component to your NetworkManager uncheck LimitClientMTU.");

                //    _networkManager.ClientManager.StopConnection();
                //    return;
                //}
                //If here split can be sent.
                SendSplitData(null, ref segment, requiredMessages, maxMessageSize);
            }
            //Split not needed.
            else
            {
                _toServerBundles[channelId].Write(segment);
            }
        }

        #region Splitting.     
        /// <summary>
        /// True if data must be split.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segmentSize"></param>
        /// <returns></returns>
        private bool SplitRequired(byte channelId, int segmentSize, out int requiredMessages, out int maxMessageSize)
        {
            maxMessageSize = Transport.GetMTU(channelId) - (TransportManager.TICK_BYTES + SPLIT_INDICATOR_SIZE);
            requiredMessages = Mathf.CeilToInt((float)segmentSize / maxMessageSize);

            return (requiredMessages > 1);
        }

        /// <summary>
        /// Splits data going to which is too large to fit within the transport MTU.
        /// </summary>
        /// <param name="conn">Connection to send to. If null data will be sent to the server.</param>
        /// <returns>True if data was sent split.</returns>
        private void SendSplitData(NetworkConnection conn, ref ArraySegment<byte> segment, int requiredMessages, int maxMessageSize)
        {
            if (requiredMessages <= 1)
            {
                _networkManager.LogError($"SendSplitData was called with {requiredMessages} required messages. This method should only be called if messages must be split into 2 pieces or more.");
                return;
            }

            byte channelId = (byte)Channel.Reliable;
            PooledWriter headerWriter = WriterPool.GetWriter();
            headerWriter.WritePacketId(PacketId.Split);
            headerWriter.WriteInt32(requiredMessages);
            ArraySegment<byte> headerSegment = headerWriter.GetArraySegment();

            int writeIndex = 0;
            bool firstWrite = true;
            //Send to connection until everything is written.
            while (writeIndex < segment.Count)
            {
                bool wasFirst = firstWrite;
                int headerReduction = 0;
                if (firstWrite)
                {
                    headerReduction = headerSegment.Count;
                    firstWrite = false;
                }
                int chunkSize = Mathf.Min(segment.Count - writeIndex - headerReduction, maxMessageSize);
                //Make a new array segment for the chunk that is getting split.
                ArraySegment<byte> splitSegment = new ArraySegment<byte>(
                    segment.Array, segment.Offset + writeIndex, chunkSize);

                //If connection is specified then it's going to a client.
                if (conn != null)
                {
                    conn.SendToClient(channelId, headerSegment, true);
                    conn.SendToClient(channelId, splitSegment);
                }
                //Otherwise it's going to the server.
                else
                {
                    _toServerBundles[channelId].Write(headerSegment, true);
                    _toServerBundles[channelId].Write(splitSegment, false);
                }

                writeIndex += chunkSize;
            }

            headerWriter.Dispose();
        }
        #endregion



        /// <summary>
        /// Processes data received by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        internal void IterateIncoming(bool server)
        {
            OnIterateIncomingStart?.Invoke(server);
            Transport.IterateIncoming(server);
            OnIterateIncomingEnd?.Invoke(server);
        }

        /// <summary>
        /// Processes data to be sent by the socket.
        /// </summary>
        /// <param name="toServer">True to process data received on the server.</param>
        internal void IterateOutgoing(bool toServer)
        {
            OnIterateOutgoingStart?.Invoke();
            int channelCount = CHANNEL_COUNT;
            ulong sentBytes = 0;
            bool latencySimulatorEnabled = LatencySimulator.CanSimulate;

            /* If sending to the client. */
            if (!toServer)
            {
                TimeManager tm = _networkManager.TimeManager;
                uint localTick = tm.LocalTick;
                //Write any dirty syncTypes.
                _networkManager.ServerManager.Objects.WriteDirtySyncTypes();

                int dirtyCount = _dirtyToClients.Count;

                //Run through all dirty connections to send data to.
                for (int z = 0; z < dirtyCount; z++)
                {
                    NetworkConnection conn = _dirtyToClients[z];
                    if (conn == null || !conn.IsValid)
                        continue;

                    //Get packets for every channel.
                    for (byte channel = 0; channel < channelCount; channel++)
                    {
                        if (conn.GetPacketBundle(channel, out PacketBundle pb))
                        {
                            for (int i = 0; i < pb.WrittenBuffers; i++)
                            {
                                //Length should always be more than 0 but check to be safe.
                                if (pb.GetBuffer(i, out ByteBuffer bb))
                                {
                                    ArraySegment<byte> segment = new ArraySegment<byte>(bb.Data, 0, bb.Length);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                    if (latencySimulatorEnabled)
                                        _latencySimulator.AddOutgoing(channel, segment, false, conn.ClientId);
                                    else
#endif
                                        Transport.SendToClient(channel, segment, conn.ClientId);
                                    sentBytes += (ulong)segment.Count;
                                }
                            }

                            pb.Reset();
                        }
                    }

                    /* When marked as disconnecting data will still be sent
                     * this iteration but the connection will be marked as invalid.
                     * This will prevent future data from going out/coming in.
                     * Also the connection will be added to a disconnecting collection
                     * so it will it disconnected briefly later to allow data from
                     * this tick to send. */
                    if (conn.Disconnecting)
                    {
                        uint requiredTicks = tm.TimeToTicks(0.1d, TickRounding.RoundUp);
                        /* Require 100ms or 2 ticks to pass
                         * before disconnecting to allow for the
                         * higher chance of success that remaining
                         * data is sent. */
                        requiredTicks = Math.Max(requiredTicks, 2);
                        _disconnectingClients.Add(new DisconnectingClient(requiredTicks + localTick, conn));
                    }

                    conn.ResetServerDirty();
                }

                //Iterate disconnects.
                for (int i = 0; i < _disconnectingClients.Count; i++)
                {
                    DisconnectingClient dc = _disconnectingClients[i];
                    if (localTick >= dc.Tick)
                    {
                        _networkManager.TransportManager.Transport.StopConnection(dc.Connection.ClientId, true);
                        _disconnectingClients.RemoveAt(i);
                        i--;
                    }
                }

                _networkManager.StatisticsManager.NetworkTraffic.LocalServerSentData(sentBytes);

                if (dirtyCount == _dirtyToClients.Count)
                    _dirtyToClients.Clear();
                else if (dirtyCount > 0)
                    _dirtyToClients.RemoveRange(0, dirtyCount);
            }
            /* If sending to the server. */
            else
            {
                for (byte channel = 0; channel < channelCount; channel++)
                {
                    if (PacketBundle.GetPacketBundle(channel, _toServerBundles, out PacketBundle pb))
                    {
                        for (int i = 0; i < pb.WrittenBuffers; i++)
                        {
                            if (pb.GetBuffer(i, out ByteBuffer bb))
                            {
                                ArraySegment<byte> segment = new ArraySegment<byte>(bb.Data, 0, bb.Length);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                if (latencySimulatorEnabled)
                                    _latencySimulator.AddOutgoing(channel, segment);
                                else
#endif
                                    Transport.SendToServer(channel, segment);
                                sentBytes += (ulong)segment.Count;
                            }
                        }
                        pb.Reset();
                    }
                }

                _networkManager.StatisticsManager.NetworkTraffic.LocalClientSentData(sentBytes);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (latencySimulatorEnabled)
                _latencySimulator.IterateOutgoing(toServer);
#endif

            Transport.IterateOutgoing(toServer);
            OnIterateOutgoingEnd?.Invoke();
        }

        #region Editor.
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Transport == null)
                Transport = GetComponent<Transport>();

            /* Update enabled state to force a reset if needed.
             * This may be required if the user checked the enabled
             * tick box at runtime. If enabled value didn't change
             * then the Get will be the same as the Set and nothing
             * will happen. */
            _latencySimulator.SetEnabled(_latencySimulator.GetEnabled());
        }
#endif
        #endregion

    }


}