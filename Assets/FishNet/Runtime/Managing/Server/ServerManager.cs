using FishNet.Authenticating;
using FishNet.Component.Observing;
using FishNet.Connection;
using FishNet.Managing.Debugging;
using FishNet.Managing.Logging;
using FishNet.Managing.Transporting;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace FishNet.Managing.Server
{
    /// <summary>
    /// A container for server data and actions.
    /// </summary>
    [DisallowMultipleComponent]
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
        /// 
        /// </summary>
        [Tooltip("Authenticator for this ServerManager. May be null if not using authentication.")]
        [SerializeField]
        private Authenticator _authenticator;
        /// <summary>
        /// Authenticator for this ServerManager. May be null if not using authentication.
        /// </summary>
        public Authenticator Authenticator { get => _authenticator; set => _authenticator = value; }
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
        ///  
        /// </summary>
        [Tooltip("Maximum frame rate the server may run at. When as host this value runs at whichever is higher between client and server.")]
        [Range(1, NetworkManager.MAXIMUM_FRAMERATE)]
        [SerializeField]
        private ushort _frameRate = NetworkManager.MAXIMUM_FRAMERATE;
        /// <summary>
        /// Maximum frame rate the server may run at. When as host this value runs at whichever is higher between client and server.
        /// </summary>
        internal ushort FrameRate => (_changeFrameRate) ? _frameRate : (ushort)0;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to share the Ids of clients and the objects they own with other clients. No sensitive information is shared.")]
        [FormerlySerializedAs("_shareOwners")] //Remove on 2022/06/01.
        [SerializeField]
        private bool _shareIds = true;
        /// <summary>
        /// True to share the Ids of clients and the objects they own with other clients. No sensitive information is shared.
        /// </summary>
        internal bool ShareIds => _shareIds;
        /// <summary>
        /// True to automatically start the server connection when running as headless.
        /// </summary>
        [Tooltip("True to automatically start the server connection when running as headless.")]
        [SerializeField]
        private bool _startOnHeadless = true;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to kick clients which send data larger than the MTU.")]
        [SerializeField]
        private bool _limitClientMTU = true;
        /// <summary>
        /// True to kick clients which send data larger than the MTU.
        /// </summary>
        internal bool LimitClientMTU => _limitClientMTU;
        #endregion

        #region Private.
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

        private void OnDestroy()
        {
            Objects?.SubscribeToSceneLoaded(false);
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void InitializeOnceInternal(NetworkManager manager)
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

            if (_authenticator == null)
                _authenticator = GetComponent<Authenticator>();
            if (_authenticator != null)
            {
                _authenticator.InitializeOnce(manager);
                _authenticator.OnAuthenticationResult += _authenticator_OnAuthenticationResult;
            }
        }

        /// <summary>
        /// Starts the server if configured to for headless.
        /// </summary>
        internal void StartForHeadless()
        {
            if (_startOnHeadless)
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
            PooledWriter writer = WriterPool.GetWriter();
            writer.WritePacketId(PacketId.Disconnect);
            ArraySegment<byte> segment = writer.GetArraySegment();
            //Send segment to each client, authenticated or not.
            foreach (NetworkConnection c in conns)
                c.SendToClient((byte)Channel.Reliable, segment);
            //Recycle writer.
            writer.Dispose();

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
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            /* If client is doing anything but started destroy pending.
             * Pending is only used for host mode. */
            if (obj.ConnectionState != LocalConnectionStates.Started)
                Objects.DestroyPending();
        }

        /// <summary>
        /// Called when a client loads initial scenes after connecting.
        /// </summary>
        private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (asServer)
                Objects.RebuildObservers(conn);
        }

        /// <summary>
        /// Changes subscription status to transport.
        /// </summary>
        /// <param name="subscribe"></param>
        private void SubscribeToTransport(bool subscribe)
        {
            if (NetworkManager == null || NetworkManager.TransportManager == null || NetworkManager.TransportManager.Transport == null)
                return;

            if (!subscribe)
            {
                NetworkManager.TransportManager.Transport.OnServerReceivedData -= Transport_OnServerReceivedData;
                NetworkManager.TransportManager.Transport.OnServerConnectionState -= Transport_OnServerConnectionState;
                NetworkManager.TransportManager.Transport.OnRemoteConnectionState -= Transport_OnRemoteConnectionState;
            }
            else
            {
                NetworkManager.TransportManager.Transport.OnServerReceivedData += Transport_OnServerReceivedData;
                NetworkManager.TransportManager.Transport.OnServerConnectionState += Transport_OnServerConnectionState;
                NetworkManager.TransportManager.Transport.OnRemoteConnectionState += Transport_OnRemoteConnectionState;
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
                MatchCondition.ClearMatchesWithoutRebuilding();

            Objects.OnServerConnectionState(args);

            LocalConnectionStates state = args.ConnectionState;

            if (NetworkManager.CanLog(LoggingType.Common))
            {
                Transport t = NetworkManager.TransportManager.GetTransport(args.TransportIndex);
                string tName = (t == null) ? "Unknown" : t.GetType().Name;
                Debug.Log($"Local Server is {state.ToString().ToLower()} for {tName}.");
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
                NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"The transport you are using supplied an invalid connection Id of {id}. Connection Id values must range between 0 and {maxIdValue}. The client has been disconnected.");
                return;
            }
            //Valid Id.
            else
            {
                //If started then add to authenticated clients.
                if (args.ConnectionState == RemoteConnectionStates.Started)
                {
                    if (NetworkManager.CanLog(LoggingType.Common))
                        Debug.Log($"Remote connection started for Id {id}.");
                    NetworkConnection conn = new NetworkConnection(NetworkManager, id);
                    Clients.Add(args.ConnectionId, conn);

                    OnRemoteConnectionState?.Invoke(conn, args);
                    //Connection is no longer valid. This can occur if the user changes the state using the OnRemoteConnectionState event.
                    if (!conn.IsValid)
                        return;
                    /* If there is an authenticator
                     * and the transport is not a local transport. */
                    if (Authenticator != null && !NetworkManager.TransportManager.IsLocalTransport(id))
                        Authenticator.OnRemoteConnection(conn);
                    else
                        ClientAuthenticated(conn);
                }
                //If stopping.
                else if (args.ConnectionState == RemoteConnectionStates.Stopped)
                {
                    /* If client's connection is found then clean
                     * them up from server. */
                    if (Clients.TryGetValueIL2CPP(id, out NetworkConnection conn))
                    {
                        conn.SetDisconnecting(true);
                        OnRemoteConnectionState?.Invoke(conn, args);
                        Clients.Remove(id);
                        MatchCondition.RemoveFromMatchWithoutRebuild(conn, NetworkManager);
                        Objects.ClientDisconnected(conn);
                        BroadcastClientConnectionChange(false, conn);
                        conn.Reset();

                        if (NetworkManager.CanLog(LoggingType.Common))
                            Debug.Log($"Remote connection stopped for Id {id}.");
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
            using (PooledWriter writer = WriterPool.GetWriter())
            {
                writer.WritePacketId(PacketId.Authenticated);
                writer.WriteNetworkConnection(conn);
                NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, writer.GetArraySegment(), conn);
            }
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
            if (segment.Count <= TransportManager.TICK_BYTES)
                return;

            //FishNet internally splits packets so nothing should ever arrive over MTU.
            int channelMtu = NetworkManager.TransportManager.Transport.GetMTU((byte)args.Channel);
            //If over MTU kick client immediately.
            if (segment.Count > channelMtu && !NetworkManager.TransportManager.IsLocalTransport(args.ConnectionId))
            {
                ExceededMTUKick();
                return;
            }

            PacketId packetId = PacketId.Unset;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            try
            {
#endif
            using (PooledReader reader = ReaderPool.GetReader(segment, NetworkManager))
            {
                NetworkManager.TimeManager.LastPacketTick = reader.ReadUInt32(AutoPackType.Unpacked);
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
                    reader.Initialize(fullMessage, NetworkManager);
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
                        if (NetworkManager.CanLog(LoggingType.Common))
                            Debug.LogError($"ConnectionId {conn.ClientId} not found within Clients. Connection will be kicked immediately.");
                        NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
                        return;
                    }
                    /* If connection isn't authenticated and isn't a broadcast
                     * then disconnect client. If a broadcast then process
                     * normally; client may still become disconnected if the broadcast
                     * does not allow to be called while not authenticated. */
                    if (!conn.Authenticated && packetId != PacketId.Broadcast)
                    {
                        if (NetworkManager.CanLog(LoggingType.Common))
                            Debug.LogError($"ConnectionId {conn.ClientId} send a Broadcast without being authenticated. Connection will be kicked immediately.");
                        conn.Disconnect(true);
                        return;
                    }

                    if (packetId == PacketId.Replicate)
                    {
                        Objects.ParseReplicateRpc(reader, conn, args.Channel);
                    }
                    else if (packetId == PacketId.ServerRpc)
                    {
                        Objects.ParseServerRpc(reader, conn, args.Channel);
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
                        if (NetworkManager.CanLog(LoggingType.Error))
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.LogError($"Server received an unhandled PacketId of {(ushort)packetId} from connectionId {args.ConnectionId}. Remaining data has been purged.");
                            _parseLogger.Print(NetworkManager);
#else
                            Debug.LogError($"Server received an unhandled PacketId of {(ushort)packetId} from connectionId {args.ConnectionId}. Connection will be kicked immediately.");
                            NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
#endif
                        }
                        return;
                    }
                }
            }
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            }
            catch (Exception e)
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Server encountered an error while parsing data for packetId {packetId} from connectionId {args.ConnectionId}. Connection will be kicked immediately. Message: {e.Message}.");
                //Kick client immediately.
                NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
            }
#endif

            //Kicks connection for exceeding MTU.
            void ExceededMTUKick()
            {
                NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
                if (NetworkManager.CanLog(LoggingType.Common))
                    Debug.Log($"ConnectionId {args.ConnectionId} sent a message larger than allowed amount. Connection will be kicked immediately.");
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
            uint clientTick = reader.ReadUInt32(AutoPackType.Unpacked);
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
        /// Sends a client connection state change.
        /// </summary>
        /// <param name="connected"></param>
        /// <param name="id"></param>
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
                Broadcast(changeMsg);

                /* If state is connected then the conn client
                 * must also receive all currently connected client ids. */
                if (connected)
                {
                    //Send already connected clients to the connection that just joined.
                    ListCache<int> lc = ListCaches.IntCache;
                    lc.Reset();
                    foreach (int key in Clients.Keys)
                        lc.AddValue(key);

                    ConnectedClientsBroadcast allMsg = new ConnectedClientsBroadcast()
                    {
                        ListCache = lc
                    };
                    conn.Broadcast(allMsg);
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
