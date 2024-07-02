#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
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
        /// Returns if an IntermediateLayer is in use.
        /// </summary>
        public bool HasIntermediateLayer => (_intermediateLayer != null);
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
        /// Layer used to modify data before it is sent or received.
        /// </summary>
        [Tooltip("Layer used to modify data before it is sent or received.")]
        [SerializeField]
        private IntermediateLayer _intermediateLayer;
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
        /// <summary>
        /// Lowest MTU of all transports for channels.
        /// </summary>
        private int[] _lowestMtus;
        /// <summary>
        /// Lowest MTU of all transports of all channels.
        /// </summary>
        private int _lowestMtu = 0;
        /// <summary>
        /// Used to cache NetworkConnections.
        /// </summary>
        private HashSet<NetworkConnection> _networkConnectionHashSet = new HashSet<NetworkConnection>();
        /// <summary>
        /// Custom amount to reserve on the MTU.
        /// </summary>
        private int _customMtuReserve = MINIMUM_MTU_RESERVE;
        #endregion

        #region Consts.
        /// <summary>
        /// Number of bytes sent for PacketId.
        /// </summary>
        public const byte PACKETID_LENGTH = 2;
        /// <summary>
        /// Number of bytes sent for ObjectId.
        /// </summary>
        public const byte OBJECT_ID_LENGTH = 2;
        /// <summary>
        /// Number of bytes sent for ComponentIndex.
        /// </summary>
        public const byte COMPONENT_INDEX_LENGTH = 1;
        /// <summary>
        /// Number of bytes sent for Tick.
        /// </summary>
        public const byte UNPACKED_TICK_LENGTH = 4;
        /// <summary>
        /// Number of bytes sent for an unpacked size, such as a collection or array size.
        /// </summary>
        public const byte UNPACKED_SIZE_LENGTH = 4;
        /// <summary>
        /// Number of bytes sent to indicate split count.
        /// </summary>
        private const byte SPLIT_COUNT_LENGTH = 4;
        /// <summary>
        /// Number of bytes required for split data. 
        /// </summary> //todo: This shouldn't have to include TickBytes but there is a parse error if it's not included. Figure out why.
        public const byte SPLIT_INDICATOR_LENGTH = (UNPACKED_TICK_LENGTH + PACKETID_LENGTH + SPLIT_COUNT_LENGTH);
        /// <summary>
        /// Number of channels supported.
        /// </summary>
        public const byte CHANNEL_COUNT = 2;
        /// <summary>
        /// MTU reserved for internal use.
        /// 1 byte is used to specify channel in packets for transports that do not include channel within their packet header. This is transport dependent.
        /// </summary>
        public const int MINIMUM_MTU_RESERVE = 1;
        /// <summary>
        /// Value to use when a MTU could not be found.
        /// </summary>
        public const int INVALID_MTU = -1;
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        internal void InitializeOnce_Internal(NetworkManager manager)
        {
            _networkManager = manager;
            TryAddDefaultTransport();
            Transport.Initialize(_networkManager, 0);
            SetLowestMTUs();
            InitializeToServerBundles();
            if (_intermediateLayer != null)
                _intermediateLayer.InitializeOnce(this);
#if DEVELOPMENT
            _latencySimulator.Initialize(manager, Transport);
#endif
        }

        /// <summary>
        /// Sets the lowest MTU values.
        /// </summary>
        private void SetLowestMTUs()
        {
            //Already set.
            if (_lowestMtu != 0)
                return;

            /* At least one transport is required. 
             * Try to add default. If a transport is already
             * specified the add method will just exit early. */
            TryAddDefaultTransport();

            int allLowest = int.MaxValue;
            //Cache lowest Mtus.
            _lowestMtus = new int[CHANNEL_COUNT];
            for (byte i = 0; i < CHANNEL_COUNT; i++)
            {
                int channelLowest = int.MaxValue;
                if (Transport is Multipass mp)
                {
                    foreach (Transport t in mp.Transports)
                    {
                        int mtu = t.GetMTU(i);
                        if (mtu != INVALID_MTU)
                            channelLowest = Mathf.Min(channelLowest, mtu);
                    }
                }
                else
                {
                    channelLowest = Transport.GetMTU(i);
                }

                _lowestMtus[i] = channelLowest;
                _lowestMtu = Mathf.Min(allLowest, channelLowest);
            }
        }

        /// <summary>
        /// Adds the default transport if a transport is not yet specified.
        /// </summary>
        private void TryAddDefaultTransport()
        {
            if (Transport == null && !gameObject.TryGetComponent<Transport>(out Transport))
                Transport = gameObject.AddComponent<FishNet.Transporting.Tugboat.Tugboat>();
        }

        ///// <summary>
        ///// Gets port for the first transport, or client transport if using Multipass.
        ///// </summary>
        //private ushort GetPort(bool asServer)
        //{
        //    if (Transport is Multipass mp)
        //    {
        //        if (asServer)
        //            return mp.Transports[0].GetPort();
        //        else
        //            return mp.ClientTransport.GetPort();
        //    }
        //    else
        //    {
        //        return Transport.GetPort();
        //    }
        //}


        ///// <summary>
        ///// Stops the local server or client connection.
        ///// </summary>
        //internal bool StopConnection(bool asServer)
        //{
        //    return Transport.StopConnection(asServer);
        //}

        ///// <summary>
        ///// Starts the local server or client connection.
        ///// </summary>
        //internal bool StartConnection(bool asServer)
        //{
        //    return Transport.StartConnection(asServer);
        //}

        ///// <summary>
        ///// Starts the local server or client connection.
        ///// </summary>
        //internal bool StartConnection(string address, bool asServer)
        //{
        //    return StartConnection(address, GetPort(asServer), asServer);
        //}

        ///// <summary>
        ///// Starts the local server or client connection on the first transport or ClientTransport if using Multipass and as client.
        ///// </summary>
        //internal bool StartConnection(string address, ushort port, bool asServer)
        //{
        //    Transport t;
        //    if (Transport is Multipass mp)
        //    {
        //        if (asServer)
        //            t = mp.Transports[0];
        //        else
        //            t = mp.ClientTransport;
        //    }
        //    else
        //    {
        //        t = Transport;
        //    }

        //    /* SetServerBindAddress must be called explictly. Only
        //     * set address if for client. */
        //    if (!asServer)
        //        t.SetClientAddress(address);
        //    t.SetPort(port);

        //    return t.StartConnection(asServer);
        //}

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
                int mtu = GetLowestMTU(i);
                _toServerBundles.Add(new PacketBundle(_networkManager, mtu));
            }
        }

        #region GetMTU.
        /// <summary>
        /// Returns MTU excluding reserve amount.
        /// </summary> 
        private int GetMTUWithReserve(int mtu)
        {
            int value = (mtu - MINIMUM_MTU_RESERVE - _customMtuReserve);
            /* If MTU is extremely low then warn user.
             * The number choosen has no significant value. */
            if (value <= 100)
            {
                string msg = $"Available MTU of {mtu} is significantly low; an invalid MTU will be returned. Check transport settings, or reduce MTU reserve if you set one using {nameof(TransportManager.SetMTUReserve)}";
                _networkManager.LogWarning(msg);

                return INVALID_MTU;
            }

            return value;
        }
        /// <summary>
        /// Sets a custom value to reserve for the internal buffers.
        /// This value is also deducted from transport MTU when using GetMTU methods.
        /// </summary>
        /// <param name="value">Value to use.</param>
        public void SetMTUReserve(int value)
        {
            if (_networkManager != null && _networkManager.IsClientStarted || _networkManager.IsServerStarted)
            {
                _networkManager.LogError($"A custom MTU reserve cannot be set after the server or client have been started or connected.");
                return;
            }

            if (value < MINIMUM_MTU_RESERVE)
            {
                _networkManager.Log($"MTU reserve {value} is below minimum value of {MINIMUM_MTU_RESERVE}. Value has been updated to {MINIMUM_MTU_RESERVE}.");
                value = MINIMUM_MTU_RESERVE;
            }

            _customMtuReserve = value;
            InitializeToServerBundles();
        }
        /// <summary>
        /// Returns the current MTU reserve.
        /// </summary>
        /// <returns></returns>
        public int GetMTUReserve() => _customMtuReserve;
        /// <summary>
        /// Returns the lowest MTU of all channels. When using multipass this will evaluate all transports within Multipass.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLowestMTU()
        {
            SetLowestMTUs();
            return GetMTUWithReserve(_lowestMtu);
        }
        /// <summary>
        /// Returns the lowest MTU for a channel. When using multipass this will evaluate all transports within Multipass.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLowestMTU(byte channel)
        {
            SetLowestMTUs();
            return GetMTUWithReserve(_lowestMtus[channel]);
        }
        /// <summary>
        /// Gets MTU on the current transport for channel.
        /// </summary>
        /// <param name="channel">Channel to get MTU of.</param>
        /// <returns></returns>
        public int GetMTU(byte channel)
        {
            SetLowestMTUs();
            int mtu = Transport.GetMTU(channel);
            if (mtu == INVALID_MTU)
                return mtu;

            return GetMTUWithReserve(mtu);
        }
        /// <summary>
        /// Gets MTU on the transportIndex for channel. This requires use of Multipass.
        /// </summary>
        /// <param name="transportIndex">Index of the transport to get the MTU on.</param>
        /// <param name="channel">Channel to get MTU of.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMTU(int transportIndex, byte channel)
        {
            if (Transport is Multipass mp)
            {
                int mtu = mp.GetMTU(channel, transportIndex);
                if (mtu == INVALID_MTU)
                    return INVALID_MTU;

                return GetMTUWithReserve(mtu);
            }
            //Using first/only transport.
            else if (transportIndex == 0)
            {
                return GetMTU(channel);
            }
            //Unhandled.
            else
            {
                _networkManager.LogWarning($"MTU cannot be returned with transportIndex because {typeof(Multipass).Name} is not in use.");
                return -1;
            }
        }
        /// <summary>
        /// Gets MTU on the transport type for channel. This requires use of Multipass.
        /// </summary>
        /// <typeparam name="T">Tyep of transport to use.</typeparam>
        /// <param name="channel">Channel to get MTU of.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMTU<T>(byte channel) where T : Transport
        {
            Transport transport = GetTransport<T>();
            if (transport != null)
            {
                int mtu = transport.GetMTU(channel);
                if (mtu == INVALID_MTU)
                    return mtu;

                return GetMTUWithReserve(mtu);
            }

            //Fall through.
            return INVALID_MTU;
        }
        #endregion

        /// <summary>
        /// Passes received to the intermediate layer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ArraySegment<byte> ProcessIntermediateIncoming(ArraySegment<byte> src, bool fromServer)
        {
            return _intermediateLayer.HandleIncoming(src, fromServer);
        }
        /// <summary>
        /// Passes sent to the intermediate layer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ArraySegment<byte> ProcessIntermediateOutgoing(ArraySegment<byte> src, bool toServer)
        {
            return _intermediateLayer.HandleOutgoing(src, toServer);
        }

        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="connection">Connection to send to. Use null for all clients.</param>
        /// <param name="splitLargeMessages">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, NetworkConnection connection, bool splitLargeMessages = true, DataOrderType orderType = DataOrderType.Default)
        {
            SetSplitValues(channelId, segment, splitLargeMessages, out int requiredMessages, out int maxSplitMessageSize);
            SendToClient(channelId, segment, connection, requiredMessages, maxSplitMessageSize, orderType);
        }

        private void SendToClient(byte channelId, ArraySegment<byte> segment, NetworkConnection connection, int requiredSplitMessages, int maxSplitMessageSize, DataOrderType orderType = DataOrderType.Default)
        {
            if (connection == null)
                return;

            if (requiredSplitMessages > 1)
                SendSplitData(connection, ref segment, requiredSplitMessages, maxSplitMessageSize, orderType);
            else
                connection.SendToClient(channelId, segment, false, orderType);
        }

        /// <summary>
        /// Sends data to observers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendToClients(byte channelId, ArraySegment<byte> segment, HashSet<NetworkConnection> observers, HashSet<NetworkConnection> excludedConnections = null, bool splitLargeMessages = true, DataOrderType orderType = DataOrderType.Default)
        {
            SetSplitValues(channelId, segment, splitLargeMessages, out int requiredMessages, out int maxSplitMessageSize);
            SendToClients(channelId, segment, observers, excludedConnections, requiredMessages, maxSplitMessageSize, orderType);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendToClients(byte channelId, ArraySegment<byte> segment, HashSet<NetworkConnection> observers, HashSet<NetworkConnection> excludedConnections, int requiredSplitMessages, int maxSplitMessageSize, DataOrderType orderType = DataOrderType.Default)
        {
            if (excludedConnections == null || excludedConnections.Count == 0)
            {
                foreach (NetworkConnection conn in observers)
                    SendToClient(channelId, segment, conn, requiredSplitMessages, maxSplitMessageSize, orderType);
            }
            else
            {
                foreach (NetworkConnection conn in observers)
                {
                    if (excludedConnections.Contains(conn))
                        continue;
                    SendToClient(channelId, segment, conn, requiredSplitMessages, maxSplitMessageSize, orderType);
                }
            }
        }

        /// <summary>
        /// Sends data to all clients.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="splitLargeMessages">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendToClients(byte channelId, ArraySegment<byte> segment, bool splitLargeMessages = true)
        {
            SetSplitValues(channelId, segment, splitLargeMessages, out int requiredMessages, out int maxSplitMessageSize);
            SendToClients_Internal(channelId, segment, requiredMessages, maxSplitMessageSize);
        }
        private void SendToClients_Internal(byte channelId, ArraySegment<byte> segment, int requiredSplitMessages, int maxSplitMessageSize)
        {
            /* Rather than buffer the message once and send to every client
             * it must be queued into every client. This ensures clients
             * receive the message in order of other packets being
             * delivered to them. */
            foreach (NetworkConnection conn in _networkManager.ServerManager.Clients.Values)
                SendToClient(channelId, segment, conn, requiredSplitMessages, maxSplitMessageSize);
        }

        /// <summary>
        /// Sends data to the server.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="splitLargeMessages">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendToServer(byte channelId, ArraySegment<byte> segment, bool splitLargeMessages = true, DataOrderType orderType = DataOrderType.Default)
        {
            SetSplitValues(channelId, segment, splitLargeMessages, out int requiredMessages, out int maxSplitMessageSize);
            SendToServer(channelId, segment, requiredMessages, maxSplitMessageSize, orderType);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendToServer(byte channelId, ArraySegment<byte> segment, int requiredMessages, int maxSplitMessageSize, DataOrderType orderType)
        {
            if (channelId >= _toServerBundles.Count)
                channelId = (byte)Channel.Reliable;

            if (requiredMessages > 1)
                SendSplitData(null, ref segment, requiredMessages, maxSplitMessageSize, orderType);
            else
                _toServerBundles[channelId].Write(segment, false, orderType);
        }

        #region Splitting.     
        /// <summary>
        /// Checks if a message can be split and outputs split information if so.
        /// </summary>
        private void SetSplitValues(byte channelId, ArraySegment<byte> segment, bool split, out int requiredMessages, out int maxSplitMessageSize)
        {
            if (!split)
            {
                requiredMessages = 0;
                maxSplitMessageSize = 0;
            }
            else
            {
                SplitRequired(channelId, segment.Count, out requiredMessages, out maxSplitMessageSize);
            }
        }

        /// <summary>
        /// Checks to set channel to reliable if dataLength is too long.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CheckSetReliableChannel(int dataLength, ref Channel channel)
        {
            if (channel == Channel.Reliable)
                return;

            bool requiresMultipleMessages = (GetRequiredMessageCount((byte)channel, dataLength, out _) > 1);
            if (requiresMultipleMessages)
                channel = Channel.Reliable;
        }

        /// <summary>
        /// Gets the required number of messages needed for segmentSize and channel.
        /// </summary>
        private int GetRequiredMessageCount(byte channelId, int segmentSize, out int maxMessageSize)
        {
            maxMessageSize = GetLowestMTU(channelId) - SPLIT_INDICATOR_LENGTH;
            return Mathf.CeilToInt((float)segmentSize / maxMessageSize);
        }

        /// <summary>
        /// True if data must be split.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segmentSize"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SplitRequired(byte channelId, int segmentSize, out int requiredMessages, out int maxMessageSize)
        {
            requiredMessages = GetRequiredMessageCount(channelId, segmentSize, out maxMessageSize);

            bool splitRequired = (requiredMessages > 1);
            if (splitRequired && channelId != (byte)Channel.Reliable)
                _networkManager.LogError($"A message of length {segmentSize} requires the reliable channel but was sent on channel {(Channel)channelId}. Please file this stack trace as a bug report.");

            return splitRequired;
        }

        /// <summary>
        /// Splits data going to which is too large to fit within the transport MTU.
        /// </summary>
        /// <param name="conn">Connection to send to. If null data will be sent to the server.</param>
        /// <returns>True if data was sent split.</returns>
        private void SendSplitData(NetworkConnection conn, ref ArraySegment<byte> segment, int requiredMessages, int maxMessageSize, DataOrderType orderType)
        {
            if (requiredMessages <= 1)
            {
                _networkManager.LogError($"SendSplitData was called with {requiredMessages} required messages. This method should only be called if messages must be split into 2 pieces or more.");
                return;
            }

            byte channelId = (byte)Channel.Reliable;
            PooledWriter headerWriter = WriterPool.Retrieve();
            headerWriter.WritePacketIdUnpacked(PacketId.Split);
            headerWriter.WriteInt32(requiredMessages);
            ArraySegment<byte> headerSegment = headerWriter.GetArraySegment();

            int writeIndex = 0;
            bool firstWrite = true;
            //Send to connection until everything is written.
            while (writeIndex < segment.Count)
            {
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
                    _toServerBundles[channelId].Write(headerSegment, true, orderType);
                    _toServerBundles[channelId].Write(splitSegment, false, orderType);
                }

                writeIndex += chunkSize;
            }

            headerWriter.Store();
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
#if DEVELOPMENT
            bool latencySimulatorEnabled = LatencySimulator.CanSimulate;
#endif
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
                            ProcessPacketBundle(pb);
                            ProcessPacketBundle(pb.GetSendLastBundle(), true);

                            void ProcessPacketBundle(PacketBundle ppb, bool isLast = false)
                            {
                                for (int i = 0; i < ppb.WrittenBuffers; i++)
                                {
                                    //Length should always be more than 0 but check to be safe.
                                    if (ppb.GetBuffer(i, out ByteBuffer bb))
                                    {
                                        ArraySegment<byte> segment = new ArraySegment<byte>(bb.Data, 0, bb.Length);
                                        if (HasIntermediateLayer)
                                            segment = ProcessIntermediateOutgoing(segment, false);
#if DEVELOPMENT
                                        if (latencySimulatorEnabled)
                                            _latencySimulator.AddOutgoing(channel, segment, false, conn.ClientId);
                                        else
#endif
                                            Transport.SendToClient(channel, segment, conn.ClientId);
                                        sentBytes += (ulong)segment.Count;
                                    }
                                }

                                ppb.Reset(false);
                            }
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
                        ProcessPacketBundle(pb);
                        ProcessPacketBundle(pb.GetSendLastBundle());

                        void ProcessPacketBundle(PacketBundle ppb)
                        {
                            for (int i = 0; i < ppb.WrittenBuffers; i++)
                            {
                                if (ppb.GetBuffer(i, out ByteBuffer bb))
                                {
                                    ArraySegment<byte> segment = new ArraySegment<byte>(bb.Data, 0, bb.Length);
                                    if (HasIntermediateLayer)
                                        segment = ProcessIntermediateOutgoing(segment, true);
#if DEVELOPMENT
                                    if (latencySimulatorEnabled)
                                        _latencySimulator.AddOutgoing(channel, segment);
                                    else
#endif
                                        Transport.SendToServer(channel, segment);
                                    sentBytes += (ulong)segment.Count;
                                }
                            }

                            ppb.Reset(false);
                        }
                    }
                }

                _networkManager.StatisticsManager.NetworkTraffic.LocalClientSentData(sentBytes);
            }

#if DEVELOPMENT
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