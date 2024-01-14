using FishNet.Connection;
using FishNet.Managing.Debugging;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using GameKit.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Client
{
    /// <summary>
    /// A container for local client data and actions.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/ClientManager")]
    public sealed partial class ClientManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called after local client has authenticated.
        /// </summary>
        public event Action OnAuthenticated;
        /// <summary>
        /// Called when the local client connection to the server has timed out.
        /// This is called immediately before disconnecting.
        /// </summary>
        public event Action OnClientTimeOut;
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        public event Action<ClientConnectionStateArgs> OnClientConnectionState;
        /// <summary>
        /// Called when a client other than self connects.
        /// This is only available when using ServerManager.ShareIds.
        /// </summary>
        public event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        /// <summary>
        /// Called when the server sends all currently connected clients.
        /// This is only available when using ServerManager.ShareIds.
        /// </summary>
        public event Action<ConnectedClientsArgs> OnConnectedClients;
        /// <summary>
        /// True if the client connection is connected to the server.
        /// </summary>
        public bool Started { get; private set; }
        /// <summary>
        /// NetworkConnection the local client is using to send data to the server.
        /// </summary>
        public NetworkConnection Connection = NetworkManager.EmptyConnection;
        /// <summary>
        /// Handling and information for objects known to the local client.
        /// </summary>
        public ClientObjects Objects { get; private set; }
        /// <summary>
        /// All currently connected clients. This field only contains data while ServerManager.ShareIds is enabled.
        /// </summary>
        public Dictionary<int, NetworkConnection> Clients = new Dictionary<int, NetworkConnection>();
        /// <summary>
        /// NetworkManager for client.
        /// </summary>
        [HideInInspector]
        public NetworkManager NetworkManager { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// What platforms to enable remote server timeout.
        /// </summary>
        [Tooltip("What platforms to enable remote server timeout.")]
        [SerializeField]
        private RemoteTimeoutType _remoteServerTimeout = RemoteTimeoutType.Development;
        /// <summary>
        /// How long in seconds server must go without sending any packets before the local client disconnects. This is independent of any transport settings.
        /// </summary>
        [Tooltip("How long in seconds server must go without sending any packets before the local client disconnects. This is independent of any transport settings.")]
        [Range(1, ServerManager.MAXIMUM_REMOTE_CLIENT_TIMEOUT_DURATION)]
        [SerializeField]
        private ushort _remoteServerTimeoutDuration = 60;
        /// <summary>
        /// Sets timeout settings. Can be used at runtime.
        /// </summary>
        /// <returns></returns>
        public void SetRemoteServerTimeout(RemoteTimeoutType timeoutType, ushort duration)
        {
            _remoteServerTimeout = timeoutType;
            duration = (ushort)Mathf.Clamp(duration, 1, ServerManager.MAXIMUM_REMOTE_CLIENT_TIMEOUT_DURATION);
            _remoteServerTimeoutDuration = duration;
        }
        //todo add remote server timeout (see ServerManager.RemoteClientTimeout).
        /// <summary>
        /// True to automatically set the frame rate when the client connects.
        /// </summary>
        [Tooltip("True to automatically set the frame rate when the client connects.")]
        [SerializeField]
        private bool _changeFrameRate = true;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum frame rate the client may run at. When as host this value runs at whichever is higher between client and server.")]
        [Range(1, NetworkManager.MAXIMUM_FRAMERATE)]
        [SerializeField]
        private ushort _frameRate = NetworkManager.MAXIMUM_FRAMERATE;
        /// <summary>
        /// Maximum frame rate the client may run at. When as host this value runs at whichever is higher between client and server.
        /// </summary>
        internal ushort FrameRate => (_changeFrameRate) ? _frameRate : (ushort)0;
        #endregion

        #region Private.
        /// <summary>
        /// Last unscaled time client got a packet.
        /// </summary>
        private float _lastPacketTime;
        /// <summary>
        /// Updates information about the last packet received.
        /// </summary>
        private void UpdateLastPacketDatas()
        {
            _lastPacketTime = Time.unscaledTime;
            LastPacketLocalTick = NetworkManager.TimeManager.LocalTick;
        }
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
        internal void InitializeOnce_Internal(NetworkManager manager)
        {
            NetworkManager = manager;
            Objects = new ClientObjects(manager);
            Objects.SubscribeToSceneLoaded(true);
            /* Unsubscribe before subscribing.
             * Shouldn't be an issue but better safe than sorry. */
            SubscribeToEvents(false);
            SubscribeToEvents(true);
            //Listen for client connections from server.
            RegisterBroadcast<ClientConnectionChangeBroadcast>(OnClientConnectionBroadcast);
            RegisterBroadcast<ConnectedClientsBroadcast>(OnConnectedClientsBroadcast);
        }


        /// <summary>
        /// Called when the server sends a connection state change for any client.
        /// </summary>
        /// <param name="args"></param>
        private void OnClientConnectionBroadcast(ClientConnectionChangeBroadcast args)
        {
            //If connecting invoke after added to clients, otherwise invoke before removed.
            RemoteConnectionStateArgs rcs = new RemoteConnectionStateArgs((args.Connected) ? RemoteConnectionState.Started : RemoteConnectionState.Stopped, args.Id, -1);

            if (args.Connected)
            {
                Clients[args.Id] = new NetworkConnection(NetworkManager, args.Id, -1, false);
                OnRemoteConnectionState?.Invoke(rcs);
            }
            else
            {
                OnRemoteConnectionState?.Invoke(rcs);
                if (Clients.TryGetValue(args.Id, out NetworkConnection c))
                {
                    c.Dispose();
                    Clients.Remove(args.Id);
                }
            }
        }

        /// <summary>
        /// Called when the server sends all currently connected clients.
        /// </summary>
        /// <param name="args"></param>
        private void OnConnectedClientsBroadcast(ConnectedClientsBroadcast args)
        {
            NetworkManager.ClearClientsCollection(Clients);

            List<int> collection = args.Values;
            //No connected clients except self.
            if (collection == null)
            {
                collection = new List<int>();
            }
            //Other clients.
            else
            {
                int count = collection.Count;
                for (int i = 0; i < count; i++)
                {
                    int id = collection[i];
                    Clients[id] = new NetworkConnection(NetworkManager, id, -1, false);
                }
            }

            OnConnectedClients?.Invoke(new ConnectedClientsArgs(collection));

        }

        /// <summary>
        /// Changes subscription status to transport.
        /// </summary>
        /// <param name="subscribe"></param>
        private void SubscribeToEvents(bool subscribe)
        {
            if (NetworkManager == null || NetworkManager.TransportManager == null || NetworkManager.TransportManager.Transport == null)
                return;

            if (subscribe)
            {
                NetworkManager.TransportManager.OnIterateIncomingEnd += TransportManager_OnIterateIncomingEnd;
                NetworkManager.TransportManager.Transport.OnClientReceivedData += Transport_OnClientReceivedData;
                NetworkManager.TransportManager.Transport.OnClientConnectionState += Transport_OnClientConnectionState;
                NetworkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
            }
            else
            {
                NetworkManager.TransportManager.OnIterateIncomingEnd -= TransportManager_OnIterateIncomingEnd;
                NetworkManager.TransportManager.Transport.OnClientReceivedData -= Transport_OnClientReceivedData;
                NetworkManager.TransportManager.Transport.OnClientConnectionState -= Transport_OnClientConnectionState;
                NetworkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        /// <summary>
        /// Gets the transport index being used for the local client.
        /// If only one transport is used this will return 0. If Multipass is being used this will return the client's transport in multipass.
        /// </summary>
        /// <returns></returns>
        public int GetTransportIndex()
        {
            if (NetworkManager.TransportManager.Transport is Multipass mp)
                return mp.ClientTransport.Index;
            else
                return 0;
        }

        /// <summary>
        /// Stops the local client connection.
        /// </summary>
        public bool StopConnection()
        {
            return NetworkManager.TransportManager.Transport.StopConnection(false);
        }

        /// <summary>
        /// Starts the local client connection.
        /// </summary>
        public bool StartConnection()
        {
            return NetworkManager.TransportManager.Transport.StartConnection(false);
        }

        /// <summary>
        /// Sets the transport address and starts the local client connection.
        /// </summary>
        public bool StartConnection(string address)
        {
            return StartConnection(address, NetworkManager.TransportManager.Transport.GetPort());
        }
        /// <summary>
        /// Sets the transport address and port, and starts the local client connection.
        /// </summary>
        public bool StartConnection(string address, ushort port)
        {
            NetworkManager.TransportManager.Transport.SetClientAddress(address);
            NetworkManager.TransportManager.Transport.SetPort(port);
            return StartConnection();
        }

        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        /// <param name="args"></param>
        private void Transport_OnClientConnectionState(ClientConnectionStateArgs args)
        {
            LocalConnectionState state = args.ConnectionState;
            Started = (state == LocalConnectionState.Started);
            Objects.OnClientConnectionState(args);

            //Clear connection after so objects can update using current Connection value.
            if (!Started)
            {
                Connection = NetworkManager.EmptyConnection;
                NetworkManager.ClearClientsCollection(Clients);
            }
            else
            {
                UpdateLastPacketDatas();
            }

            if (NetworkManager.CanLog(LoggingType.Common))
            {
                Transport t = NetworkManager.TransportManager.GetTransport(args.TransportIndex);
                string tName = (t == null) ? "Unknown" : t.GetType().Name;
                string socketInformation = string.Empty;
                if (state == LocalConnectionState.Starting)
                    socketInformation = $" Server IP is {t.GetClientAddress()}, port is {t.GetPort()}.";
                Debug.Log($"Local client is {state.ToString().ToLower()} for {tName}.{socketInformation}");
            }

            NetworkManager.UpdateFramerate();
            OnClientConnectionState?.Invoke(args);
        }

        /// <summary>
        /// Called when a socket receives data.
        /// </summary>
        private void Transport_OnClientReceivedData(ClientReceivedDataArgs args)
        {
            ParseReceived(args);
        }

        /// <summary>
        /// Called after IterateIncoming has completed.
        /// </summary>
        private void TransportManager_OnIterateIncomingEnd(bool server)
        {
            /* Should the last packet received be a spawn or despawn
             * then the cache won't yet be iterated because it only
             * iterates when a packet is anything but those two. Because
             * of such if any object caches did come in they must be iterated
             * at the end of the incoming cycle. This isn't as clean as I'd
             * like but it does ensure there will be no missing network object
             * references on spawned objects. */
            if (Started && !server)
                Objects.IterateObjectCache();
        }

        /// <summary>
        /// Parses received data.
        /// </summary>
        private void ParseReceived(ClientReceivedDataArgs args)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _parseLogger.Reset();
#endif
            UpdateLastPacketDatas();

            ArraySegment<byte> segment = args.Data;
            NetworkManager.StatisticsManager.NetworkTraffic.LocalClientReceivedData((ulong)segment.Count);
            if (segment.Count <= TransportManager.TICK_BYTES)
                return;

            PooledReader reader = ReaderPool.Retrieve(segment, NetworkManager, Reader.DataSource.Server);
            NetworkManager.TimeManager.SetLastPacketTick(reader.ReadTickUnpacked());
            ParseReader(reader, args.Channel);
            ReaderPool.Store(reader);

        }

        internal void ParseReader(PooledReader reader, Channel channel, bool print = false)
        {
            bool hasIntermediateLayer = NetworkManager.TransportManager.HasIntermediateLayer;
            PacketId packetId = PacketId.Unset;
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            try
            {
#endif
            Reader.DataSource dataSource = Reader.DataSource.Server;
            /* This is a special condition where a message may arrive split.
            * When this occurs buffer each packet until all packets are
            * received. */
            if (reader.PeekPacketId() == PacketId.Split)
            {
                //Skip packetId.
                reader.ReadPacketId();
                int expectedMessages;
                _splitReader.GetHeader(reader, out expectedMessages);
                _splitReader.Write(NetworkManager.TimeManager.LastPacketTick, reader, expectedMessages);
                /* If fullMessage returns 0 count then the split
                 * has not written fully yet. Otherwise, if there is
                 * data within then reinitialize reader with the
                 * full message. */
                ArraySegment<byte> fullMessage = _splitReader.GetFullMessage();
                if (fullMessage.Count == 0)
                    return;

                //Initialize reader with full message.
                if (hasIntermediateLayer)
                    reader.Initialize(NetworkManager.TransportManager.ProcessIntermediateIncoming(fullMessage, true), NetworkManager, dataSource);
                else
                    reader.Initialize(fullMessage, NetworkManager, dataSource);
            }
            //Not split.
            else
            {
                //Override values with intermediate layer changes.
                if (hasIntermediateLayer)
                {
                    ArraySegment<byte> modified = NetworkManager.TransportManager.ProcessIntermediateIncoming(reader.GetRemainingData(), false);
                    reader.Initialize(modified, NetworkManager, dataSource);
                }
            }

            while (reader.Remaining > 0)
            {
                packetId = reader.ReadPacketId();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (print)
                    Debug.Log($"PacketId {packetId} - Remaining {reader.Remaining}.");
                _parseLogger.AddPacket(packetId);
#endif
                bool spawnOrDespawn = (packetId == PacketId.ObjectSpawn || packetId == PacketId.ObjectDespawn);
                /* Length of data. Only available if using unreliable. Unreliable packets
                 * can arrive out of order which means object orientated messages such as RPCs may
                 * arrive after the object for which they target has already been destroyed. When this happens
                 * on lesser solutions they just dump the entire packet. However, since FishNet batches data.
                 * it's very likely a packet will contain more than one packetId. With this mind, length is
                 * sent as well so if any reason the data does have to be dumped it will only be dumped for
                 * that single packetId  but not the rest. Broadcasts don't need length either even if unreliable
                 * because they are not object bound. */

                //Is spawn or despawn; cache packet.
                if (spawnOrDespawn)
                {
                    if (packetId == PacketId.ObjectSpawn)
                        Objects.CacheSpawn(reader);
                    else if (packetId == PacketId.ObjectDespawn)
                        Objects.CacheDespawn(reader);
                }
                //Not spawn or despawn.
                else
                {
                    /* Iterate object cache should any of the
                     * incoming packets rely on it. Objects
                     * in cache will always be received before any messages
                     * that use them. */
                    Objects.IterateObjectCache();
                    //Then process packet normally.
                    if ((ushort)packetId >= NetworkManager.StartingRpcLinkIndex)
                    {
                        Objects.ParseRpcLink(reader, (ushort)packetId, channel);
                    }
                    else if (packetId == PacketId.Replicate)
                    {
                        Objects.ParseReplicateRpc(reader, null, channel);
                    }
                    else if (packetId == PacketId.Reconcile)
                    {
                        Objects.ParseReconcileRpc(reader, channel);
                    }
                    else if (packetId == PacketId.ObserversRpc)
                    {
                        Objects.ParseObserversRpc(reader, channel);
                    }
                    else if (packetId == PacketId.TargetRpc)
                    {
                        Objects.ParseTargetRpc(reader, channel);
                    }
                    else if (packetId == PacketId.Broadcast)
                    {
                        ParseBroadcast(reader, channel);
                    }
                    else if (packetId == PacketId.PingPong)
                    {
                        ParsePingPong(reader);
                    }
                    else if (packetId == PacketId.SyncVar)
                    {
                        Objects.ParseSyncType(reader, false, channel);
                    }
                    else if (packetId == PacketId.SyncObject)
                    {
                        Objects.ParseSyncType(reader, true, channel);
                    }
                    else if (packetId == PacketId.PredictedSpawnResult)
                    {
                        Objects.ParsePredictedSpawnResult(reader);
                    }
                    else if (packetId == PacketId.TimingUpdate)
                    {
                        NetworkManager.TimeManager.ParseTimingUpdate(reader);
                    }
                    else if (packetId == PacketId.OwnershipChange)
                    {
                        Objects.ParseOwnershipChange(reader);
                    }
                    else if (packetId == PacketId.Authenticated)
                    {
                        ParseAuthenticated(reader);
                    }
                    else if (packetId == PacketId.Disconnect)
                    {
                        reader.Clear();
                        StopConnection();
                    }
                    else
                    {

                        NetworkManager.LogError($"Client received an unhandled PacketId of {(ushort)packetId} on channel {channel}. Remaining data has been purged.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        _parseLogger.Print(NetworkManager);
#endif
                        return;
                    }
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (print)
                    Debug.Log($"Reader remaining {reader.Remaining}");
#endif
            }

            /* Iterate cache when reader is emptied.
            * This is incase the last packet received
            * was a spawned, which wouldn't trigger
            * the above iteration. There's no harm
            * in doing this check multiple times as there's
            * an exit early check. */
            Objects.IterateObjectCache();
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            }
            catch (Exception e)
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Client encountered an error while parsing data for packetId {packetId}. Message: {e.Message}.");
            }
#endif
        }

        /// <summary>
        /// Parses a PingPong packet.
        /// </summary>
        /// <param name="reader"></param>
        private void ParsePingPong(PooledReader reader)
        {
            uint clientTick = reader.ReadTickUnpacked();
            NetworkManager.TimeManager.ModifyPing(clientTick);
        }

        /// <summary>
        /// Parses a received connectionId. This is received before client receives connection state change.
        /// </summary>
        /// <param name="reader"></param>
        private void ParseAuthenticated(PooledReader reader)
        {
            NetworkManager networkManager = NetworkManager;
            int connectionId = reader.ReadNetworkConnectionId();
            //If only a client then make a new connection.
            if (!networkManager.IsServer)
            {
                Clients.TryGetValueIL2CPP(connectionId, out Connection);
                /* This is bad and should never happen unless the connection is dropping
                 * while receiving authenticated. Would have to be a crazy race condition
                 * but with the network anything is possible. */
                if (Connection == null)
                {
                    NetworkManager.LogWarning($"Client connection could not be found while parsing authenticated status. This usually occurs when the client is receiving a packet immediately before losing connection.");
                    Connection = new NetworkConnection(networkManager, connectionId, GetTransportIndex(), false);
                }
            }
            /* If also the server then use the servers connection
             * for the connectionId. This is to resolve host problems
             * where LocalConnection for client differs from the server Connection
             * reference, which results in different field values. */
            else
            {
                if (networkManager.ServerManager.Clients.TryGetValueIL2CPP(connectionId, out NetworkConnection conn))
                {
                    Connection = conn;
                }
                else
                {
                    networkManager.LogError($"Unable to lookup LocalConnection for {connectionId} as host.");
                    Connection = new NetworkConnection(networkManager, connectionId, GetTransportIndex(), false);
                }
            }

            //If predicted spawning is enabled also get reserved Ids.
            if (NetworkManager.PredictionManager.GetAllowPredictedSpawning())
            {
                byte count = reader.ReadByte();
                Queue<int> q = Connection.PredictedObjectIds;
                for (int i = 0; i < count; i++)
                    q.Enqueue(reader.ReadNetworkObjectId());
            }

            /* Set the TimeManager tick to lastReceivedTick.
             * This still doesn't account for latency but
             * it's the best we can do until the client gets
             * a ping response. */
            //Only do this if not also server.
            if (!networkManager.IsServer)
                networkManager.TimeManager.Tick = networkManager.TimeManager.LastPacketTick;

            //Mark as authenticated.
            Connection.ConnectionAuthenticated();
            OnAuthenticated?.Invoke();
            /* Register scene objects for all scenes
             * after being authenticated. This is done after
             * authentication rather than when the connection
             * is started because if also as server an online
             * scene may already be loaded on server, but not
             * for client. This means the sceneLoaded unity event
             * won't fire, and since client isn't authenticated
            * at the connection start phase objects won't be added. */
            Objects.RegisterAndDespawnSceneObjects();
        }

        /// <summary>
        /// Called when the TimeManager calls OnPostTick.
        /// </summary>
        private void TimeManager_OnPostTick()
        {
            CheckServerTimeout();
        }


        /// <summary>
        /// Checks to timeout client connections.
        /// </summary>
        private void CheckServerTimeout()
        {
            /* Not connected or host. There should be no way
             * for server to drop and client not know about it as host.
             * This would mean a game crash or force close in which
             * the client would be gone as well anyway. */
            if (!Started || NetworkManager.IsServer)
                return;
            if (_remoteServerTimeout == RemoteTimeoutType.Disabled)
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //If development but not set to development return.
            else if (_remoteServerTimeout != RemoteTimeoutType.Development)
                return;
#endif
            //Wait two timing intervals to give packets a chance to come through.
            if (NetworkManager.SceneManager.IsIteratingQueue(TimeManager.TIMING_INTERVAL * 2f))
                return;

            /* ServerManager version only checks every so often
             * to perform iterations over time so the checks are not
             * impactful on the CPU. The client however can check every tick
             * since it's simple math. */
            if (Time.unscaledTime - _lastPacketTime > _remoteServerTimeoutDuration)
            {
                OnClientTimeOut?.Invoke();
                NetworkManager.Log($"Server has timed out. You can modify this feature on the ClientManager component.");
                StopConnection();
            }
        }

    }

}
