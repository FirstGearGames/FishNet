using FishNet.Authenticating;
using FishNet.Component.Observing;
using FishNet.Connection;
using FishNet.Managing.Debugging;
using FishNet.Managing.Logging;
using FishNet.Managing.Predicting;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using GameKit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FishNet.Managing.Server
{
    /// <summary>
    /// A container for server data and actions.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/ServerManager")]
    public sealed partial class ServerManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called after the local server connection state changes.
        /// </summary>
        public event Action<ServerConnectionStateArgs> OnServerConnectionState;
        /// <summary>
        /// Called when authenticator has concluded a result for a connection. Boolean is true if authentication passed, false if failed.
        /// </summary>
        public event Action<NetworkConnection, bool> OnAuthenticationResult;
        /// <summary>
        /// Called when a remote client state changes with the server.
        /// </summary>
        public event Action<NetworkConnection, RemoteConnectionStateArgs> OnRemoteConnectionState;
        /// <summary>
        /// True if the server connection has started.
        /// </summary>
        public bool Started { get; private set; }
        /// <summary>
        /// Handling and information for objects on the server.
        /// </summary>
        public ServerObjects Objects { get; private set; }
        /// <summary>
        /// Authenticated and non-authenticated connected clients.
        /// </summary>
        [HideInInspector]
        public Dictionary<int, NetworkConnection> Clients = new Dictionary<int, NetworkConnection>();
        /// <summary>
        /// NetworkManager for server.
        /// </summary>
        [HideInInspector]
        public NetworkManager NetworkManager { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// Authenticator for this ServerManager. May be null if not using authentication.
        /// </summary>
        [Obsolete("Use GetAuthenticator and SetAuthenticator.")] //Remove on 2023/06/01
        public Authenticator Authenticator
        {
            get => GetAuthenticator();
            set => SetAuthenticator(value);
        }
        /// <summary>
        /// Gets the Authenticator for this manager.
        /// </summary>
        /// <returns></returns>
        public Authenticator GetAuthenticator() => _authenticator;
        /// <summary>
        /// Gets the Authenticator for this manager, and initializes it.
        /// </summary>
        /// <returns></returns>
        public void SetAuthenticator(Authenticator value)
        {
            _authenticator = value;
            InitializeAuthenticator();
        }
        [Tooltip("Authenticator for this ServerManager. May be null if not using authentication.")]
        [SerializeField]
        private Authenticator _authenticator;
        /// <summary>
        /// What platforms to enable remote client timeout.
        /// </summary>
        [Tooltip("What platforms to enable remote client timeout.")]
        [SerializeField]
        private RemoteTimeoutType _remoteClientTimeout = RemoteTimeoutType.Development;
        /// <summary>
        /// How long in seconds client must go without sending any packets before getting disconnected. This is independent of any transport settings.
        /// </summary>
        [Tooltip("How long in seconds a client must go without sending any packets before getting disconnected. This is independent of any transport settings.")]
        [Range(1, MAXIMUM_REMOTE_CLIENT_TIMEOUT_DURATION)]
        [SerializeField]
        private ushort _remoteClientTimeoutDuration = 60;
        /// <summary>
        /// Sets timeout settings. Can be used at runtime.
        /// </summary>
        /// <returns></returns>
        public void SetRemoteClientTimeout(RemoteTimeoutType timeoutType, ushort duration)
        {
            _remoteClientTimeout = timeoutType;
            duration = (ushort)Mathf.Clamp(duration, 1, MAXIMUM_REMOTE_CLIENT_TIMEOUT_DURATION);
            _remoteClientTimeoutDuration = duration;
        }
        /// <summary>
        /// Default send rate for SyncTypes. A value of 0f will send changed values every tick.
        /// SyncTypeRate cannot yet be changed at runtime because this would require recalculating rates on SyncBase, which is not yet implemented.
        /// </summary>
        /// <returns></returns>
        internal float GetSynctypeRate() => _syncTypeRate;
        [Tooltip("Default send rate for SyncTypes. A value of 0f will send changed values every tick.")]
        [Range(0f, 60f)]
        [SerializeField]
        private float _syncTypeRate = 0.1f;
        /// <summary>
        /// How to pack object spawns.
        /// </summary>
        [Tooltip("How to pack object spawns.")]
        [SerializeField]
        internal TransformPackingData SpawnPacking = new TransformPackingData()
        {
            Position = AutoPackType.Unpacked,
            Rotation = AutoPackType.PackedLess,
            Scale = AutoPackType.PackedLess
        };
        /// <summary>
        /// True to automatically set the frame rate when the client connects.
        /// </summary>
        [Tooltip("True to automatically set the frame rate when the client connects.")]
        [SerializeField]
        private bool _changeFrameRate = true;
        /// <summary>
        /// Maximum frame rate the server may run at. When as host this value runs at whichever is higher between client and server.
        /// </summary>
        internal ushort FrameRate => (_changeFrameRate) ? _frameRate : (ushort)0;
        [Tooltip("Maximum frame rate the server may run at. When as host this value runs at whichever is higher between client and server.")]
        [Range(1, NetworkManager.MAXIMUM_FRAMERATE)]
        [SerializeField]
        private ushort _frameRate = NetworkManager.MAXIMUM_FRAMERATE;
        /// <summary>
        /// True to share the Ids of clients and the objects they own with other clients. No sensitive information is shared.
        /// </summary>
        internal bool ShareIds => _shareIds;
        [Tooltip("True to share the Ids of clients and the objects they own with other clients. No sensitive information is shared.")]
        [SerializeField]
        private bool _shareIds = true;
        /// <summary>
        /// Gets StartOnHeadless value.
        /// </summary>
        public bool GetStartOnHeadless() => _startOnHeadless;
        /// <summary>
        /// Sets StartOnHeadless value.
        /// </summary>
        /// <param name="value">New value to use.</param>
        public void SetStartOnHeadless(bool value) => _startOnHeadless = value;
        [Tooltip("True to automatically start the server connection when running as headless.")]
        [SerializeField]
        private bool _startOnHeadless = true;
        /// <summary>
        /// True to kick clients which send data larger than the MTU.
        /// </summary>
        internal bool LimitClientMTU => _limitClientMTU;
        [Tooltip("True to kick clients which send data larger than the MTU.")]
        [SerializeField]
        private bool _limitClientMTU = true;
        #endregion

        #region Private.
        /// <summary>
        /// The last index checked to see if a client has not sent a packet in awhile.
        /// </summary>
        private int _nextClientTimeoutCheckIndex;
        /// <summary>
        /// Next time a timeout check can be performed.
        /// </summary>
        private float _nextTimeoutCheckTime;
        /// <summary>
        /// Used to read splits.
        /// </summary>
        private SplitReader _splitReader = new SplitReader();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Logs data about parser to help debug.
        /// </summary>
        private ParseLogger _parseLogger = new ParseLogger();
#endif
        #endregion

        #region Const.
        /// <summary>
        /// Maximum value the remote client timeout can be set to.
        /// </summary>
        public const ushort MAXIMUM_REMOTE_CLIENT_TIMEOUT_DURATION = 1500;
        #endregion

        private void OnDestroy()
        {
            Objects?.SubscribeToSceneLoaded(false);
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void InitializeOnce_Internal(NetworkManager manager)
        {
            NetworkManager = manager;
            Objects = new ServerObjects(manager);
            Objects.SubscribeToSceneLoaded(true);
            InitializeRpcLinks();
            //Unsubscribe first incase already subscribed.
            SubscribeToTransport(false);
            SubscribeToTransport(true);
            NetworkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
            NetworkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
            NetworkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;

            if (_authenticator == null)
                _authenticator = GetComponent<Authenticator>();
            if (_authenticator != null)
                InitializeAuthenticator();

            _cachedLevelOfDetailInterval = NetworkManager.ClientManager.LevelOfDetailInterval;
            _cachedUseLod = NetworkManager.ObserverManager.GetEnableNetworkLod();
        }

        /// <summary>
        /// Initializes the authenticator to this manager.
        /// </summary>
        private void InitializeAuthenticator()
        {
            Authenticator auth = GetAuthenticator();
            if (auth == null || auth.Initialized)
                return;
            if (NetworkManager == null)
                return;

            auth.InitializeOnce(NetworkManager);
            auth.OnAuthenticationResult += _authenticator_OnAuthenticationResult;
        }

        /// <summary>
        /// Starts the server if configured to for headless.
        /// </summary>
        internal void StartForHeadless()
        {
            if (GetStartOnHeadless())
            {
                //Wrapping logic in check instead of everything so _startOnHeadless doesnt warn as unused in editor.
#if UNITY_SERVER
                StartConnection();
#endif
            }
        }

        /// <summary>
        /// Stops the local server connection.
        /// </summary>
        /// <param name="sendDisconnectMessage">True to send a disconnect message to all clients first.</param>
        public bool StopConnection(bool sendDisconnectMessage)
        {
            if (sendDisconnectMessage)
                SendDisconnectMessages(Clients.Values.ToList(), true);

            //Return stop connection result.
            return NetworkManager.TransportManager.Transport.StopConnection(true);
        }

        /// <summary>
        /// Sends a disconnect messge to connectionIds.
        /// This does not iterate outgoing automatically.
        /// </summary>
        /// <param name="connectionIds"></param>
        internal void SendDisconnectMessages(int[] connectionIds)
        {
            List<NetworkConnection> conns = new List<NetworkConnection>();
            foreach (int item in connectionIds)
            {
                if (Clients.TryGetValueIL2CPP(item, out NetworkConnection c))
                    conns.Add(c);
            }

            if (conns.Count > 0)
                SendDisconnectMessages(conns, false);
        }
        /// <summary>
        /// Sends a disconnect message to all clients and immediately iterates outgoing.
        /// </summary>
        private void SendDisconnectMessages(List<NetworkConnection> conns, bool iterate)
        {
            PooledWriter writer = WriterPool.Retrieve();
            writer.WritePacketId(PacketId.Disconnect);
            ArraySegment<byte> segment = writer.GetArraySegment();
            //Send segment to each client, authenticated or not.
            foreach (NetworkConnection c in conns)
                c.SendToClient((byte)Channel.Reliable, segment);
            //Recycle writer.
            writer.Store();

            if (iterate)
                NetworkManager.TransportManager.IterateOutgoing(true);
        }

        /// <summary>
        /// Starts the local server connection.
        /// </summary>
        public bool StartConnection()
        {
            return NetworkManager.TransportManager.Transport.StartConnection(true);
        }
        /// <summary>
        /// Starts the local server using port.
        /// </summary>
        /// <param name="port">Port to start on.</param>
        /// <returns></returns>
        public bool StartConnection(ushort port)
        {
            Transport t = NetworkManager.TransportManager.Transport;
            t.SetPort(port);
            return t.StartConnection(true);
        }

        /// <summary>
        /// Checks to timeout client connections.
        /// </summary>
        private void CheckClientTimeout()
        {
            if (_remoteClientTimeout == RemoteTimeoutType.Disabled)
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //If development but not set to development return.
            else if (_remoteClientTimeout != RemoteTimeoutType.Development)
                return;
#endif
            //Wait two timing intervals to give packets a chance to come through.
            if (NetworkManager.SceneManager.IsIteratingQueue(TimeManager.TIMING_INTERVAL * 2f))
                return;

            float unscaledTime = Time.unscaledTime;
            if (unscaledTime < _nextTimeoutCheckTime)
                return;
            //Check for timeouts every 200ms.
            const float TIMEOUT_CHECK_FREQUENCY = 0.2f;
            _nextTimeoutCheckTime = (unscaledTime + TIMEOUT_CHECK_FREQUENCY);
            //No clients.
            int clientsCount = Clients.Count;
            if (clientsCount == 0)
                return;

            /* If here can do checks. */
            //If to reset index.
            if (_nextClientTimeoutCheckIndex >= clientsCount)
                _nextClientTimeoutCheckIndex = 0;

            //Number of ticks passed for client to be timed out.
            uint requiredTicks = NetworkManager.TimeManager.TimeToTicks(_remoteClientTimeoutDuration, TickRounding.RoundUp);

            const float FULL_CHECK_TIME = 2f;
            /* Number of times this is expected to run every 2 seconds.
             * Iterations will try to complete the entire client collection
             * over these 2 seconds. */
            int checkCount = Mathf.CeilToInt(FULL_CHECK_TIME / TIMEOUT_CHECK_FREQUENCY);
            int targetIterations = Mathf.Max(clientsCount / checkCount, 1);

            uint localTick = NetworkManager.TimeManager.LocalTick;
            //Number of connections iterated in Clients.Values.
            int connsIterated = 0;
            foreach (NetworkConnection item in Clients.Values)
            {
                //If iterations are met then we can begin checking for timeouts.
                if (connsIterated >= _nextClientTimeoutCheckIndex)
                {
                    uint clientLocalTick = item.PacketTick.LocalTick;
                    /* If client tick has not been set yet then use the tick
                     * when they connected to the server. */
                    if (clientLocalTick == 0)
                        clientLocalTick = item.ServerConnectionTick;

                    uint difference = (localTick - clientLocalTick);
                    //Client has timed out.
                    if (difference >= requiredTicks)
                        item.Kick(KickReason.UnexpectedProblem, LoggingType.Common, $"{item.ToString()} has timed out. You can modify this feature on the ServerManager component.");
                    //If all iterations are complete.
                    if (--targetIterations <= 0)
                        break;
                }

                //Increase iterated count.
                connsIterated++;
            }
        }

        /// <summary>
        /// Called when the TimeManager calls OnPostTick.
        /// </summary>
        private void TimeManager_OnPostTick()
        {
            CheckClientTimeout();
        }

        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            /* If client is doing anything but started destroy pending.
             * Pending is only used for host mode. */
            if (obj.ConnectionState != LocalConnectionState.Started)
                Objects.DestroyPending();
        }

        /// <summary>
        /// Called when a client loads initial scenes after connecting.
        /// </summary>
        private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (asServer)
            {
                Objects.RebuildObservers(conn);
                /* If connection is host then renderers must be hidden
                 * for all objects not visible to the host. The observer system
                 * does handle this but only after an initial state is set.
                 * If the clientHost joins without observation of an object
                 * then the initial state will never be set. */
                if (conn.IsLocalClient)
                {
                    foreach (NetworkObject nob in Objects.Spawned.Values)
                    {
                        if (!nob.Observers.Contains(conn))
                            nob.SetRenderersVisible(false);
                    }
                }
            }
        }

        /// <summary>
        /// Changes subscription status to transport.
        /// </summary>
        /// <param name="subscribe"></param>
        private void SubscribeToTransport(bool subscribe)
        {
            if (NetworkManager == null || NetworkManager.TransportManager == null || NetworkManager.TransportManager.Transport == null)
                return;

            if (subscribe)
            {
                NetworkManager.TransportManager.Transport.OnServerReceivedData += Transport_OnServerReceivedData;
                NetworkManager.TransportManager.Transport.OnServerConnectionState += Transport_OnServerConnectionState;
                NetworkManager.TransportManager.Transport.OnRemoteConnectionState += Transport_OnRemoteConnectionState;
            }
            else
            {
                NetworkManager.TransportManager.Transport.OnServerReceivedData -= Transport_OnServerReceivedData;
                NetworkManager.TransportManager.Transport.OnServerConnectionState -= Transport_OnServerConnectionState;
                NetworkManager.TransportManager.Transport.OnRemoteConnectionState -= Transport_OnRemoteConnectionState;
            }
        }

        /// <summary>
        /// Called when authenticator has concluded a result for a connection. Boolean is true if authentication passed, false if failed.
        /// Server listens for this event automatically.
        /// </summary>
        private void _authenticator_OnAuthenticationResult(NetworkConnection conn, bool authenticated)
        {
            if (!authenticated)
                conn.Disconnect(false);
            else
                ClientAuthenticated(conn);
        }

        /// <summary>
        /// Called when a connection state changes for the local server.
        /// </summary>
        private void Transport_OnServerConnectionState(ServerConnectionStateArgs args)
        {
            /* Let the client manager know the server state is changing first.
             * This gives the client an opportunity to clean-up or prepare
             * before the server completes it's actions. */
            Started = AnyServerStarted();
            NetworkManager.ClientManager.Objects.OnServerConnectionState(args);
            //If no servers are started then reset match conditions.
            if (!Started)
            {
                MatchCondition.StoreCollections(NetworkManager);
                //Despawn without synchronizing network objects.
                Objects.DespawnWithoutSynchronization(true);
            }
            Objects.OnServerConnectionState(args);

            LocalConnectionState state = args.ConnectionState;

            if (NetworkManager.CanLog(LoggingType.Common))
            {
                Transport t = NetworkManager.TransportManager.GetTransport(args.TransportIndex);
                string tName = (t == null) ? "Unknown" : t.GetType().Name;
                string socketInformation = string.Empty;
                if (state == LocalConnectionState.Starting)
                    socketInformation = $" Listening on port {t.GetPort()}.";
                Debug.Log($"Local server is {state.ToString().ToLower()} for {tName}.{socketInformation}");
            }

            NetworkManager.UpdateFramerate();
            OnServerConnectionState?.Invoke(args);
        }

        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        private void Transport_OnRemoteConnectionState(RemoteConnectionStateArgs args)
        {
            //Sanity check to make sure transports are following proper types/ranges.
            int id = args.ConnectionId;
            int maxIdValue = short.MaxValue;
            if (id < 0 || id > maxIdValue)
            {
                Kick(args.ConnectionId, KickReason.UnexpectedProblem, LoggingType.Error, $"The transport you are using supplied an invalid connection Id of {id}. Connection Id values must range between 0 and {maxIdValue}. The client has been disconnected.");
                return;
            }
            //Valid Id.
            else
            {
                //If started then add to authenticated clients.
                if (args.ConnectionState == RemoteConnectionState.Started)
                {
                    NetworkManager.Log($"Remote connection started for Id {id}.");
                    NetworkConnection conn = new NetworkConnection(NetworkManager, id, args.TransportIndex, true);
                    Clients.Add(args.ConnectionId, conn);
                    OnRemoteConnectionState?.Invoke(conn, args);
                    //Connection is no longer valid. This can occur if the user changes the state using the OnRemoteConnectionState event.
                    if (!conn.IsValid)
                        return;
                    /* If there is an authenticator
                     * and the transport is not a local transport. */
                    Authenticator auth = GetAuthenticator();
                    if (auth != null && !NetworkManager.TransportManager.IsLocalTransport(id))
                        auth.OnRemoteConnection(conn);
                    else
                        ClientAuthenticated(conn);
                }
                //If stopping.
                else if (args.ConnectionState == RemoteConnectionState.Stopped)
                {
                    /* If client's connection is found then clean
                     * them up from server. */
                    if (Clients.TryGetValueIL2CPP(id, out NetworkConnection conn))
                    {
                        conn.SetDisconnecting(true);
                        OnRemoteConnectionState?.Invoke(conn, args);
                        Clients.Remove(id);
                        Objects.ClientDisconnected(conn);
                        BroadcastClientConnectionChange(false, conn);
                        //Return predictedObjectIds.
                        Queue<int> pqId = conn.PredictedObjectIds;
                        while (pqId.Count > 0)
                            Objects.CacheObjectId(pqId.Dequeue());

                        conn.Dispose();
                        NetworkManager.Log($"Remote connection stopped for Id {id}.");
                    }
                }
            }
        }

        /// <summary>
        /// Sends client their connectionId.
        /// </summary>
        /// <param name="connectionid"></param>
        private void SendAuthenticated(NetworkConnection conn)
        {
            PooledWriter writer = WriterPool.Retrieve();
            writer.WritePacketId(PacketId.Authenticated);
            writer.WriteNetworkConnection(conn);
            /* If predicted spawning is enabled then also send
             * reserved objectIds. */
            ;
            PredictionManager pm = NetworkManager.PredictionManager;
            if (pm.GetAllowPredictedSpawning())
            {
                int count = Mathf.Min(Objects.GetObjectIdCache().Count, pm.GetReservedObjectIds());
                writer.WriteByte((byte)count);

                for (int i = 0; i < count; i++)
                {
                    ushort val = (ushort)Objects.GetNextNetworkObjectId(false);
                    writer.WriteNetworkObjectId(val);
                    conn.PredictedObjectIds.Enqueue(val);
                }
            }

            NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, writer.GetArraySegment(), conn);
            writer.Store();
        }
        /// <summary>
        /// Called when the server socket receives data.
        /// </summary>
        private void Transport_OnServerReceivedData(ServerReceivedDataArgs args)
        {
            ParseReceived(args);
        }

        /// <summary>
        /// Called when the server receives data.
        /// </summary>
        /// <param name="args"></param>
        private void ParseReceived(ServerReceivedDataArgs args)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _parseLogger.Reset();
#endif

            //Not from a valid connection.
            if (args.ConnectionId < 0)
                return;
            ArraySegment<byte> segment = args.Data;
            NetworkManager.StatisticsManager.NetworkTraffic.LocalServerReceivedData((ulong)segment.Count);
            if (segment.Count <= TransportManager.TICK_BYTES)
                return;

            //FishNet internally splits packets so nothing should ever arrive over MTU.
            int channelMtu = NetworkManager.TransportManager.GetMTU(args.TransportIndex, (byte)args.Channel);
            //If over MTU kick client immediately.
            if (segment.Count > channelMtu && !NetworkManager.TransportManager.IsLocalTransport(args.ConnectionId))
            {
                ExceededMTUKick();
                return;
            }

            bool hasIntermediateLayer = NetworkManager.TransportManager.HasIntermediateLayer;
            PacketId packetId = PacketId.Unset;
            PooledReader reader = null;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            try
            {
#endif
            Reader.DataSource dataSource = Reader.DataSource.Client;
            reader = ReaderPool.Retrieve(segment, NetworkManager, dataSource);
            uint tick = reader.ReadTickUnpacked();
            NetworkManager.TimeManager.SetLastPacketTick(tick);
            /* This is a special condition where a message may arrive split.
            * When this occurs buffer each packet until all packets are
            * received. */
            if (reader.PeekPacketId() == PacketId.Split)
            {
                //Skip packetId.
                reader.ReadPacketId();

                int expectedMessages;
                _splitReader.GetHeader(reader, out expectedMessages);
                //If here split message can be written.
                _splitReader.Write(NetworkManager.TimeManager.LastPacketTick, reader, expectedMessages);

                /* If fullMessage returns 0 count then the split
                 * has not written fully yet. Otherwise, if there is
                 * data within then reinitialize reader with the
                 * full message. */
                ArraySegment<byte> fullMessage = _splitReader.GetFullMessage();
                if (fullMessage.Count == 0)
                    return;

                /* If here then all data has been received.
                 * It's possible the client could have exceeded 
                 * maximum MTU but not the maximum number of splits.
                 * This is because the length of each split
                 * is not written, so we don't know how much data of the
                 * final message actually belonged to the split vs
                 * unrelated data added afterwards. We're going to cut
                 * the client some slack in this situation for the sake
                 * of keeping things simple. */

                //Initialize reader with full message.
                if (hasIntermediateLayer)
                    reader.Initialize(NetworkManager.TransportManager.ProcessIntermediateIncoming(fullMessage, false), NetworkManager, dataSource);
                else
                    reader.Initialize(fullMessage, NetworkManager, dataSource);
            }
            //Not Split.
            else
            {
                //Override values with intermediate layer changes.
                if (hasIntermediateLayer)
                {
                    ArraySegment<byte> modified = NetworkManager.TransportManager.ProcessIntermediateIncoming(reader.GetRemainingData(), false);
                    reader.Initialize(modified, NetworkManager, dataSource);
                }
            }

            //Parse reader.
            while (reader.Remaining > 0)
            {
                packetId = reader.ReadPacketId();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _parseLogger.AddPacket(packetId);
#endif
                NetworkConnection conn;

                /* Connection isn't available. This should never happen.
                 * Force an immediate disconnect. */
                if (!Clients.TryGetValueIL2CPP(args.ConnectionId, out conn))
                {
                    Kick(args.ConnectionId, KickReason.UnexpectedProblem, LoggingType.Error, $"ConnectionId {args.ConnectionId} not found within Clients. Connection will be kicked immediately.");
                    return;
                }
                conn.PacketTick.Update(NetworkManager.TimeManager, tick, Timing.EstimatedTick.OldTickOption.SetLastRemoteTick);
                /* If connection isn't authenticated and isn't a broadcast
                 * then disconnect client. If a broadcast then process
                 * normally; client may still become disconnected if the broadcast
                 * does not allow to be called while not authenticated. */
                if (!conn.Authenticated && packetId != PacketId.Broadcast)
                {
                    conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {conn.ClientId} sent a Broadcast without being authenticated. Connection will be kicked immediately.");
                    return;
                }

                //Only check if not developer build because users pay pause editor.
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
                    /* If hasn't sent LOD recently enough. LODs are sent every half a second, so
                     * by multiplaying interval by 60 this gives the client a 30 second window. */
                    if (_cachedUseLod && conn.IsLateForLevelOfDetail(_cachedLevelOfDetailInterval * 60))
                    {
                        conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {conn.ClientId} has gone too long without sending a level of detail update. Connection will be kicked immediately.");
                        return;
                    }
#endif
                if (packetId == PacketId.Replicate)
                {
                    Objects.ParseReplicateRpc(reader, conn, args.Channel);
                }
                else if (packetId == PacketId.ServerRpc)
                {
                    Objects.ParseServerRpc(reader, conn, args.Channel);
                }
                else if (packetId == PacketId.ObjectSpawn)
                {
                    if (!NetworkManager.PredictionManager.GetAllowPredictedSpawning())
                    {
                        conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {conn.ClientId} sent a predicted spawn while predicted spawning is not enabled. Connection will be kicked immediately.");
                        return;
                    }
                    Objects.ReadPredictedSpawn(reader, conn);
                }
                else if (packetId == PacketId.ObjectDespawn)
                {
                    if (!NetworkManager.PredictionManager.GetAllowPredictedSpawning())
                    {
                        conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {conn.ClientId} sent a predicted spawn while predicted spawning is not enabled. Connection will be kicked immediately.");
                        return;
                    }
                    Objects.ReadPredictedDespawn(reader, conn);
                }
                else if (packetId == PacketId.NetworkLODUpdate)
                {
                    ParseNetworkLODUpdate(reader, conn);
                }
                else if (packetId == PacketId.Broadcast)
                {
                    ParseBroadcast(reader, conn, args.Channel);
                }
                else if (packetId == PacketId.PingPong)
                {
                    ParsePingPong(reader, conn);
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    NetworkManager.LogError($"Server received an unhandled PacketId of {(ushort)packetId} on channel {args.Channel} from connectionId {args.ConnectionId}. Remaining data has been purged.");
                    _parseLogger.Print(NetworkManager);
#else
                        NetworkManager.LogError($"Server received an unhandled PacketId of {(ushort)packetId} on channel {args.Channel} from connectionId {args.ConnectionId}. Connection will be kicked immediately.");
                        NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
#endif
                    return;
                }
            }
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            }
            catch (Exception e)
            {
                Kick(args.ConnectionId, KickReason.MalformedData, LoggingType.Error, $"Server encountered an error while parsing data for packetId {packetId} from connectionId {args.ConnectionId}. Connection will be kicked immediately. Message: {e.Message}.");
            }
            finally
            {
                reader?.Store();
            }
#else
            reader?.Store();
#endif

            //Kicks connection for exceeding MTU.
            void ExceededMTUKick()
            {
                Kick(args.ConnectionId, KickReason.ExploitExcessiveData, LoggingType.Common, $"ConnectionId {args.ConnectionId} sent a message larger than allowed amount. Connection will be kicked immediately.");
            }

        }

        /// <summary>
        /// Parses a received PingPong.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="conn"></param>
        private void ParsePingPong(PooledReader reader, NetworkConnection conn)
        {
            /* //security limit how often clients can send pings.
             * have clients use a stopwatch rather than frame time
             * for checks to ensure it's not possible to send
             * excessively should their game stutter then catch back up. */
            uint clientTick = reader.ReadTickUnpacked();
            if (conn.CanPingPong())
                NetworkManager.TimeManager.SendPong(conn, clientTick);
        }


        /// <summary>
        /// Called when a remote client authenticates with the server.
        /// </summary>
        /// <param name="connectionId"></param>
        private void ClientAuthenticated(NetworkConnection connection)
        {
            /* Immediately send connectionId to client. Some transports
            * don't give clients their remoteId, therefor it has to be sent
            * by the ServerManager. This packet is very simple and can be built
            * on the spot. */
            connection.ConnectionAuthenticated();
            /* Send client Ids before telling the client
             * they are authenticated. This is important because when the client becomes
             * authenticated they set their LocalConnection using Clients field in ClientManager,
             * which is set after getting Ids. */
            BroadcastClientConnectionChange(true, connection);
            SendAuthenticated(connection);

            OnAuthenticationResult?.Invoke(connection, true);
            NetworkManager.SceneManager.OnClientAuthenticated(connection);
        }

        /// <summary>
        /// Sends a client connection state change to owner and other clients if applicable.
        /// </summary>
        private void BroadcastClientConnectionChange(bool connected, NetworkConnection conn)
        {
            //If sharing Ids then send all connected client Ids first if is a connected state.
            if (ShareIds)
            {
                /* Send a broadcast to all authenticated clients with the clientId
                 * that just connected. The conn client will also get this. */
                ClientConnectionChangeBroadcast changeMsg = new ClientConnectionChangeBroadcast()
                {
                    Connected = connected,
                    Id = conn.ClientId
                };
                foreach (NetworkConnection c in Clients.Values)
                {
                    if (c.Authenticated)
                        Broadcast(c, changeMsg);
                }

                /* If state is connected then the conn client
                 * must also receive all currently connected client ids. */
                if (connected)
                {
                    //Send already connected clients to the connection that just joined.
                    List<int> cache = CollectionCaches<int>.RetrieveList();
                    foreach (int key in Clients.Keys)
                        cache.Add(key);

                    ConnectedClientsBroadcast allMsg = new ConnectedClientsBroadcast()
                    {
                        Values = cache
                    };
                    conn.Broadcast(allMsg);
                    CollectionCaches<int>.Store(cache);
                }
            }
            //If not sharing Ids then only send ConnectionChange to conn.
            else
            {
                if (connected)
                {
                    /* Send broadcast only to the client which just disconnected.
                     * Only send if connecting. If the client is disconnected there's no reason
                     * to send them a disconnect msg. */
                    ClientConnectionChangeBroadcast changeMsg = new ClientConnectionChangeBroadcast()
                    {
                        Connected = connected,
                        Id = conn.ClientId
                    };
                    Broadcast(conn, changeMsg, true, Channel.Reliable);
                }
            }

        }

    }


}
