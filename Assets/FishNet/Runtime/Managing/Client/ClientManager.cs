using FishNet.Connection;
using FishNet.Managing.Debugging;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Managing.Transporting;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Client
{
    /// <summary>
    /// A container for local client data and actions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ClientManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called after local client has authenticated.
        /// </summary>
        public event Action OnAuthenticated;
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        public event Action<ClientConnectionStateArgs> OnClientConnectionState;
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
            if (args.Connected)
                Clients[args.Id] = new NetworkConnection(NetworkManager, args.Id);
            else
                Clients.Remove(args.Id);
        }

        /// <summary>
        /// Called when the server sends all currently connected clients.
        /// </summary>
        /// <param name="args"></param>
        private void OnConnectedClientsBroadcast(ConnectedClientsBroadcast args)
        {
            Clients.Clear();

            List<int> collection = args.Ids;
            int count = collection.Count;
            for (int i = 0; i < count; i++)
            {
                int id = collection[i];
                Clients[id] = new NetworkConnection(NetworkManager, id);
            }
        }


        /// <summary>
        /// Changes subscription status to transport.
        /// </summary>
        /// <param name="subscribe"></param>
        private void SubscribeToEvents(bool subscribe)
        {
            if (NetworkManager == null || NetworkManager.TransportManager == null || NetworkManager.TransportManager.Transport == null)
                return;

            if (!subscribe)
            {
                NetworkManager.TransportManager.OnIterateIncomingEnd -= TransportManager_OnIterateIncomingEnd;
                NetworkManager.TransportManager.Transport.OnClientReceivedData -= Transport_OnClientReceivedData;
                NetworkManager.TransportManager.Transport.OnClientConnectionState -= Transport_OnClientConnectionState;
            }
            else
            {
                NetworkManager.TransportManager.OnIterateIncomingEnd += TransportManager_OnIterateIncomingEnd;
                NetworkManager.TransportManager.Transport.OnClientReceivedData += Transport_OnClientReceivedData;
                NetworkManager.TransportManager.Transport.OnClientConnectionState += Transport_OnClientConnectionState;
            }
        }

        /// <summary>
        /// Stops the local client connection.
        /// </summary>
        public void StopConnection()
        {
            NetworkManager.TransportManager.Transport.StopConnection(false);
        }

        /// <summary>
        /// Starts the local client connection.
        /// </summary>
        public void StartConnection()
        {
            NetworkManager.TransportManager.Transport.StartConnection(false);
        }

        /// <summary>
        /// Sets the transport address and starts the local client connection.
        /// </summary>
        public void StartConnection(string address)
        {
            StartConnection(address, NetworkManager.TransportManager.Transport.GetPort());
        }
        /// <summary>
        /// Sets the transport address and port, and starts the local client connection.
        /// </summary>
        public void StartConnection(string address, ushort port)
        {
            NetworkManager.TransportManager.Transport.SetClientAddress(address);
            NetworkManager.TransportManager.Transport.SetPort(port);
            StartConnection();
        }

        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        /// <param name="args"></param>
        private void Transport_OnClientConnectionState(ClientConnectionStateArgs args)
        {
            LocalConnectionStates state = args.ConnectionState;
            Started = (state == LocalConnectionStates.Started);
            Objects.OnClientConnectionState(args);

            //Clear connection after so objects can update using current Connection value.
            if (!Started)
            {
                Connection = NetworkManager.EmptyConnection;
                Clients.Clear();
            }

            if (NetworkManager.CanLog(LoggingType.Common))
            {
                Transport t = NetworkManager.TransportManager.GetTransport(args.TransportIndex);
                string tName = (t == null) ? "Unknown" : t.GetType().Name;
                Debug.Log($"Local client is {state.ToString().ToLower()} for {tName}.");
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

            ArraySegment<byte> segment = args.Data;
            if (segment.Count <= TransportManager.TICK_BYTES)
                return;

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
                    _splitReader.Write(NetworkManager.TimeManager.LastPacketTick, reader, expectedMessages);
                    /* If fullMessage returns 0 count then the split
                     * has not written fully yet. Otherwise, if there is
                     * data within then reinitialize reader with the
                     * full message. */
                    ArraySegment<byte> fullMessage = _splitReader.GetFullMessage();
                    if (fullMessage.Count == 0)
                        return;

                    //Initialize reader with full message.
                    reader.Initialize(fullMessage, NetworkManager);
                }

                while (reader.Remaining > 0)
                {
                    packetId = reader.ReadPacketId();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
                            Objects.ParseRpcLink(reader, (ushort)packetId, args.Channel);
                        }
                        else if (packetId == PacketId.Reconcile)
                        {
                            Objects.ParseReconcileRpc(reader, args.Channel);
                        }
                        else if (packetId == PacketId.ObserversRpc)
                        {
                            Objects.ParseObserversRpc(reader, args.Channel);
                        }
                        else if (packetId == PacketId.TargetRpc)
                        {
                            Objects.ParseTargetRpc(reader, args.Channel);
                        }
                        else if (packetId == PacketId.Broadcast)
                        {
                            ParseBroadcast(reader, args.Channel);
                        }
                        else if (packetId == PacketId.PingPong)
                        {
                            ParsePingPong(reader);
                        }
                        else if (packetId == PacketId.SyncVar)
                        {
                            Objects.ParseSyncType(reader, false, args.Channel);
                        }
                        else if (packetId == PacketId.SyncObject)
                        {
                            Objects.ParseSyncType(reader, true, args.Channel);
                        }
                        else if (packetId == PacketId.TimingUpdate)
                        {
                            NetworkManager.TimeManager.ParseTimingUpdate();
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
                            reader.Skip(reader.Remaining);
                            StopConnection();
                        }
                        else
                        {
                            if (NetworkManager.CanLog(LoggingType.Error))
                            {
                                Debug.LogError($"Client received an unhandled PacketId of {(ushort)packetId}. Remaining data has been purged.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                _parseLogger.Print(NetworkManager);
#endif
                            }
                            return;
                        }
                    }
                }

                /* Iterate cache when reader is emptied.
                 * This is incase the last packet received
                 * was a spawned, which wouldn't trigger
                 * the above iteration. There's no harm
                 * in doing this check multiple times as there's
                 * an exit early check. */
                Objects.IterateObjectCache();
            }
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
            uint clientTick = reader.ReadUInt32(AutoPackType.Unpacked);
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
                    if (networkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"Unable to lookup LocalConnection for {connectionId} as host.");

                    Connection = new NetworkConnection(networkManager, connectionId);
                }
            }

            /* Set the TimeManager tick to lastReceivedTick.
             * This still doesn't account for latency but
             * it's the best we can do until the client gets
             * a ping response. */
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

    }

}
