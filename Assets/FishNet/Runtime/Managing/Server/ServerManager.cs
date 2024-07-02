#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
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
using GameKit.Dependencies.Utilities;
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
        public Dictionary<int, NetworkConnection> Clients = new();
        /// <summary>
        /// Clients dictionary as a list, containing only values.
        /// </summary>        
        private List<NetworkConnection> _clientsList = new();
        /// <summary>
        /// NetworkManager for server.
        /// </summary>
        [HideInInspector]
        public NetworkManager NetworkManager { get; private set; }
        #endregion

        #region Serialized.
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
        /// True to allow clients to use predicted spawning. While true, each NetworkObject you wish this feature to apply towards must have a PredictedSpawn component.
        /// Predicted spawns can have custom validation on the server.
        /// </summary>
        internal bool GetAllowPredictedSpawning() => _allowPredictedSpawning;
        [Tooltip("True to allow clients to use predicted spawning. While true, each NetworkObject you wish this feature to apply towards must have a PredictedSpawn component. Predicted spawns can have custom validation on the server.")]
        [SerializeField]
        private bool _allowPredictedSpawning = false;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of Ids to reserve on clients for predicted spawning. Higher values will allow clients to send more predicted spawns per second but may reduce availability of ObjectIds with high player counts.")]
        [Range(1, 100)]
        [SerializeField]
        private byte _reservedObjectIds = 15;
        /// <summary>
        /// Maximum number of Ids to reserve on clients for predicted spawning. Higher values will allow clients to send more predicted spawns per second but may reduce availability of ObjectIds with high player counts.
        /// </summary>
        /// <returns></returns>
        internal byte GetReservedObjectIds() => _reservedObjectIds;
        /// <summary>
        /// Default send rate for SyncTypes. A value of 0f will send changed values every tick.
        /// </summary>
        /// <returns></returns>
        internal float GetSyncTypeRate() => _syncTypeRate;
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
        /// Sets the maximum frame rate the client may run at. Calling this method will enable ChangeFrameRate.
        /// </summary>
        /// <param name="value">New value.</param>
        public void SetFrameRate(ushort value)
        {
            _frameRate = (ushort)Mathf.Clamp(value, 0, NetworkManager.MAXIMUM_FRAMERATE);
            _changeFrameRate = true;
            if (NetworkManager != null)
                NetworkManager.UpdateFramerate();
        }
        /// <summary>
        /// True to share the Ids of clients and the objects they own with other clients. No sensitive information is shared.
        /// </summary>
        public bool ShareIds => _shareIds;
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
#if DEVELOPMENT
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
            writer.WritePacketIdUnpacked(PacketId.Disconnect);
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
#if DEVELOPMENT
            //If development but not set to development return.
            else if (_remoteClientTimeout != RemoteTimeoutType.Development)
                return;
#endif
            //Wait two timing intervals to give packets a chance to come through.
            if (NetworkManager.SceneManager.IsIteratingQueue(2f))
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
            for (int i = 0; i < targetIterations; i++)
            {
                if (_nextClientTimeoutCheckIndex >= _clientsList.Count)
                    _nextClientTimeoutCheckIndex = 0;

                NetworkConnection item = _clientsList[_nextClientTimeoutCheckIndex];
                uint clientLocalTick = item.PacketTick.LocalTick;
                /* If client tick has not been set yet then use the tick
                 * when they connected to the server. */
                if (clientLocalTick == 0)
                    clientLocalTick = item.ServerConnectionTick;

                uint difference = (localTick - clientLocalTick);
                //Client has timed out.
                if (difference >= requiredTicks)
                    item.Kick(KickReason.UnexpectedProblem, LoggingType.Common, $"{item.ToString()} has timed out. You can modify this feature on the ServerManager component.");

                _nextClientTimeoutCheckIndex++;
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
        /// Checks to make sure the client is on the same version.
        /// This is to help developers make sure their builds are on the same FishNet version.
        /// </summary>
        private void ParseVersion(PooledReader reader, NetworkConnection conn, int transportId)
        {
            //Cannot be authenticated if havent sent version yet. This is a duplicate version send, likely exploit attempt.
            if (conn.HasSentVersion)
            {
                conn.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, $"Connection {conn.ToString()} has sent their FishNet version after being authenticated; this is not possible under normal conditions.");
                return;
            }

            conn.HasSentVersion = true;
            string version = reader.ReadString();
            //Version match.
            if (version == NetworkManager.FISHNET_VERSION)
            {
                /* Send to client if server is in development build or not.
                 * This is to allow the client to utilize some features/information
                 * received from the server only when it's in dev mode. */
                bool isDevelopmentBuild;
#if DEVELOPMENT
                isDevelopmentBuild = true;
#else
                isDevelopmentBuild = false;
#endif
                PooledWriter writer = WriterPool.Retrieve();
                writer.WritePacketIdUnpacked(PacketId.Version);
                writer.WriteBoolean(isDevelopmentBuild);
                conn.SendToClient((byte)Channel.Reliable, writer.GetArraySegment());
                WriterPool.Store(writer);

                /* If there is an authenticator
                 * and the transport is not a local transport. */
                Authenticator auth = GetAuthenticator();
                if (auth != null && !NetworkManager.TransportManager.IsLocalTransport(transportId))
                    auth.OnRemoteConnection(conn);
                else
                    ClientAuthenticated(conn);
            }
            else
            {
                conn.Kick(reader, KickReason.UnexpectedProblem, LoggingType.Warning, $"Connection {conn.ToString()} has been kicked for being on FishNet version {version}. Server version is {NetworkManager.FISHNET_VERSION}.");
            }
        }

        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        private void Transport_OnRemoteConnectionState(RemoteConnectionStateArgs args)
        {
            //Sanity check to make sure transports are following proper types/ranges.
            int id = args.ConnectionId;
            if (id < 0 || id > NetworkConnection.MAXIMUM_CLIENTID_VALUE)
            {
                Kick(args.ConnectionId, KickReason.UnexpectedProblem, LoggingType.Error, $"The transport you are using supplied an invalid connection Id of {id}. Connection Id values must range between 0 and {NetworkConnection.MAXIMUM_CLIENTID_VALUE}. The client has been disconnected.");
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
                    _clientsList.Add(conn);
                    OnRemoteConnectionState?.Invoke(conn, args);

                    //Do nothing else until the client sends it's version.
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
                        _clientsList.Remove(conn);
                        Objects.ClientDisconnected(conn);
                        BroadcastClientConnectionChange(false, conn);
                        //Return predictedObjectIds.
                        Queue<int> pqId = conn.PredictedObjectIds;
                        while (pqId.Count > 0)
                            Objects.CacheObjectId(pqId.Dequeue());

                        conn.ResetState();
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
            writer.WritePacketIdUnpacked(PacketId.Authenticated);
            writer.WriteNetworkConnection(conn);
            /* If predicted spawning is enabled then also send
             * reserved objectIds. */
            ;
            PredictionManager pm = NetworkManager.PredictionManager;
            if (GetAllowPredictedSpawning())
            {
                int count = Mathf.Min(Objects.GetObjectIdCache().Count, GetReservedObjectIds());
                writer.WriteUInt8Unpacked((byte)count);

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
#if DEVELOPMENT
            _parseLogger.Reset();
#endif

            //Not from a valid connection.
            if (args.ConnectionId < 0)
                return;

            ArraySegment<byte> segment;
            if (NetworkManager.TransportManager.HasIntermediateLayer)
                segment = NetworkManager.TransportManager.ProcessIntermediateIncoming(args.Data, false);
            else
                segment = args.Data;

            NetworkManager.StatisticsManager.NetworkTraffic.LocalServerReceivedData((ulong)segment.Count);
            if (segment.Count <= TransportManager.UNPACKED_TICK_LENGTH)
                return;

            //FishNet internally splits packets so nothing should ever arrive over MTU.
            int channelMtu = NetworkManager.TransportManager.GetMTU(args.TransportIndex, (byte)args.Channel);
            //If over MTU kick client immediately.
            if (segment.Count > channelMtu)
            {
                ExceededMTUKick();
                return;
            }

            TimeManager timeManager = NetworkManager.TimeManager;

            bool hasIntermediateLayer = NetworkManager.TransportManager.HasIntermediateLayer;
            PacketId packetId = PacketId.Unset;
            PooledReader reader = null;
#if !DEVELOPMENT
            try
            {
#endif
            Reader.DataSource dataSource = Reader.DataSource.Client;
            reader = ReaderPool.Retrieve(segment, NetworkManager, dataSource);
            uint tick = reader.ReadTickUnpacked();
            timeManager.LastPacketTick.Update(tick);
            /* This is a special condition where a message may arrive split.
            * When this occurs buffer each packet until all packets are
            * received. */
            if (reader.PeekPacketId() == PacketId.Split)
            {
                //Skip packetId.
                reader.ReadPacketId();

                int expectedMessages;
                _splitReader.GetHeader(reader, out expectedMessages);
                //If here split message is to be read into splitReader.
                _splitReader.Write(tick, reader, expectedMessages);

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
                reader.Initialize(fullMessage, NetworkManager, dataSource);
            }

            //Parse reader.
            while (reader.Remaining > 0)
            {
                packetId = reader.ReadPacketId();
#if DEVELOPMENT
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
                conn.LocalTick.Update(timeManager, tick, EstimatedTick.OldTickOption.Discard);
                conn.PacketTick.Update(timeManager, tick, EstimatedTick.OldTickOption.SetLastRemoteTick);
                /* If connection isn't authenticated and isn't a broadcast
                 * then disconnect client. If a broadcast then process
                 * normally; client may still become disconnected if the broadcast
                 * does not allow to be called while not authenticated. */
                if (!conn.IsAuthenticated && packetId != PacketId.Version && packetId != PacketId.Broadcast)
                {
                    conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {conn.ClientId} sent packetId {packetId} without being authenticated. Connection will be kicked immediately.");
                    return;
                }

                //Only check if not developer build because users pay pause editor.
#if !DEVELOPMENT
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
                    if (!GetAllowPredictedSpawning())
                    {
                        conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {conn.ClientId} sent a predicted spawn while predicted spawning is not enabled. Connection will be kicked immediately.");
                        return;
                    }
                    Objects.ReadPredictedSpawn(reader, conn);
                }
                else if (packetId == PacketId.ObjectDespawn)
                {
                    if (!GetAllowPredictedSpawning())
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
                else if (packetId == PacketId.Version)
                {
                    ParseVersion(reader, conn, args.TransportIndex);
                }
                else
                {
#if DEVELOPMENT
                    NetworkManager.LogError($"Server received an unhandled PacketId of {(ushort)packetId} on channel {args.Channel} from connectionId {args.ConnectionId}. Remaining data has been purged.");
                    _parseLogger.Print(NetworkManager);
#else
                        NetworkManager.LogError($"Server received an unhandled PacketId of {(ushort)packetId} on channel {args.Channel} from connectionId {args.ConnectionId}. Connection will be kicked immediately.");
                        NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
#endif
                    return;
                }
            }
#if !DEVELOPMENT
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
            //Only send if the connection was authenticated.
            if (!conn.IsAuthenticated)
                return;
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
                    if (c.IsAuthenticated)
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
