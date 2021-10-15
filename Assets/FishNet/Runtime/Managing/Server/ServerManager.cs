using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Authenticating;
using FishNet.Managing.Transporting;
using FishNet.Managing.Logging;

namespace FishNet.Managing.Server
{
    /// <summary>
    /// A container for server data and actions.
    /// </summary>
    [DisallowMultipleComponent]
    public partial class ServerManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called after local the server connection state changes.
        /// </summary>
        public event Action<ServerConnectionStateArgs> OnServerConnectionState;
        /// <summary>
        /// Called when authenticator has concluded a result for a connection. Boolean is true if authentication passed, false if failed.
        /// </summary>
        public event Action<NetworkConnection, bool> OnAuthenticationResult;
        /// <summary>
        /// Called when a client state changes with the server.
        /// </summary>
        public event Action<NetworkConnection, RemoteConnectionStateArgs> OnRemoteConnectionState;
        /// <summary>
        /// True if the server connection has started.
        /// </summary>
        public bool Started { get; private set; } = false;
        /// <summary>
        /// Handling and information for objects on the server.
        /// </summary>
        public ServerObjects Objects { get; private set; } = null;
        /// <summary>
        /// Authenticated and non-authenticated connected clients.
        /// </summary>
        [HideInInspector]
        public Dictionary<int, NetworkConnection> Clients = new Dictionary<int, NetworkConnection>();
        /// <summary>
        /// NetworkManager for server.
        /// </summary>
        [HideInInspector]
        public NetworkManager NetworkManager = null;
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
        /// 
        /// </summary>
        [Tooltip("True to share current owner of objects with all clients. False to hide owner of objects from everyone but owner.")]
        [SerializeField]
        private bool _shareOwners = true;
        /// <summary>
        /// True to share current owner of objects with all clients. False to hide owner of objects from everyone but owner.
        /// </summary>
        internal bool ShareOwners => _shareOwners;
        /// <summary>
        /// True to automatically start the server connection when running as headless.
        /// </summary>
        [Tooltip("True to automatically start the server connection when running as headless.")]
        [SerializeField]
        private bool _startOnHeadless = true;
        /// <summary>
        /// True if to automatically kick clients which send packets larger than the transport MTU.
        /// </summary>
        [Tooltip("True if to automatically kick clients which send packets larger than the transport MTU.")]
        [SerializeField]
        private bool _limitClientMTU = true;
        /// <summary>
        /// Maximum amount of data a client may send. Use 0 for unlimited, otherwise specify an ammount. This is only applicable when LimitClientMTU is disabled; otherwise, clients are kicked immediately should they exceed MTU.
        /// </summary>
        [Tooltip("Maximum amount of data a client may send. Use 0 for unlimited, otherwise specify an ammount. This is only applicable when LimitClientMTU is disabled; otherwise, clients are kicked immediately should they exceed MTU.")]
        //[Range(0, int.MaxValue)] //The range attribute is broken via Unity. It shows negative values when using int or higher as max value.
        [SerializeField]
        private int _maximumClientMTU = 0;
        #endregion

        #region Private.
        /// <summary>
        /// Used to read splits.
        /// </summary>
        private SplitReader _splitReader = new SplitReader();
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void FirstInitialize(NetworkManager manager)
        {
            NetworkManager = manager;
            Objects = new ServerObjects(manager);
            //Unsubscrive first incase already subscribed.
            SubscribeToTransport(false);
            SubscribeToTransport(true);
            NetworkManager.TransportManager.OnIterateIncomingEnd += TransportManager_OnIterateIncomingEnd;
            NetworkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;

            if (_authenticator != null)
            {
                _authenticator.FirstInitialize(manager);
                _authenticator.OnAuthenticationResult += _authenticator_OnAuthenticationResult;
            }

            //If not limiting client MTU.
            if (!_limitClientMTU)
            {
                //If max value is less than reliable limit.
                int reliableMTU = NetworkManager.TransportManager.Transport.GetMTU((byte)Channel.Reliable);
                if (_maximumClientMTU > 0 && _maximumClientMTU <= reliableMTU)
                {
                    _maximumClientMTU = (reliableMTU * 2);
                    if (NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"Maximum Client MTU of {_maximumClientMTU} is set lower than the Maximum Reliable MTU of {reliableMTU} for the transport. Maximum Client MTU has been changed to {_maximumClientMTU}.");
                }
            }

            if (_startOnHeadless && Application.isBatchMode)
                NetworkManager.TransportManager.Transport.StartConnection(true);
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
        /// Called after IterateIncoming has completed. True for on server, false for on client.
        /// </summary>
        private void TransportManager_OnIterateIncomingEnd(bool server)
        {
            if (!server)
                Objects.DestroyPending();
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
        /// Called when a connection state changes local server.
        /// </summary>
        /// <param name="args"></param>
        private void Transport_OnServerConnectionState(ServerConnectionStateArgs args)
        {
            Started = (args.ConnectionState == LocalConnectionStates.Started);
            Objects.OnServerConnectionState(args);
            //If not connected then clear clients.
            if (args.ConnectionState != LocalConnectionStates.Started)
                Clients.Clear();

            if (args.ConnectionState == LocalConnectionStates.Started)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Common))
                    Debug.Log("Server has been started.");
            }
            else if (args.ConnectionState == LocalConnectionStates.Stopped)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Common))
                    Debug.Log("Server has been stopped.");
            }

            OnServerConnectionState?.Invoke(args);
        }

        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        private void Transport_OnRemoteConnectionState(RemoteConnectionStateArgs args)
        {
            //If connection state is for a remote client.
            if (args.ConnectionId >= 0)
            {
                //If started then add to authenticated clients.
                if (args.ConnectionState == RemoteConnectionStates.Started)
                {
                    if (NetworkManager.CanLog(Logging.LoggingType.Common))
                        Debug.Log($"Remote connection started for Id {args.ConnectionId}.");
                    NetworkConnection conn = new NetworkConnection(NetworkManager, args.ConnectionId);
                    Clients.Add(args.ConnectionId, conn);

                    OnRemoteConnectionState?.Invoke(conn, args);

                    if (Authenticator != null)
                        Authenticator.OnRemoteConnection(conn);
                    else
                        ClientAuthenticated(conn);
                }
                //If stopping.
                else if (args.ConnectionState == RemoteConnectionStates.Stopped)
                {
                    /* If client's connection is found then clean
                     * them up from server. */
                    if (Clients.TryGetValue(args.ConnectionId, out NetworkConnection conn))
                    {
                        conn.SetDisconnecting(true);
                        OnRemoteConnectionState?.Invoke(conn, args);
                        Clients.Remove(args.ConnectionId);
                        Objects.ClientDisconnected(conn);
                        conn.Reset();

                        if (NetworkManager.CanLog(Logging.LoggingType.Common))
                            Debug.Log($"Remote connection stopped for Id {args.ConnectionId}.");
                    }
                }
            }
        }

        /// <summary>
        /// Sends client their connectionId.
        /// </summary>
        /// <param name="connectionid"></param>
        private void SendConnectionId(NetworkConnection conn)
        {
            using (PooledWriter writer = WriterPool.GetWriter())
            {
                writer.WriteByte((byte)PacketId.ConnectionId);
                writer.WriteInt16((short)conn.ClientId);
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
            //Not from a valid connection.
            if (args.ConnectionId < 0)
                return;
            ArraySegment<byte> segment = args.Data;
            if (segment.Count == 0)
                return;

            PacketId packetId = PacketId.Unset;
            try
            {
                using (PooledReader reader = ReaderPool.GetReader(segment, NetworkManager))
                {
                    /* This is a special condition where a message may arrive split.
                    * When this occurs buffer each packet until all packets are
                    * received. */
                    if (segment.Array[0] == (byte)PacketId.Split)
                    {
                        /* Various conditions that will kick a client immediately.
                         * Splits are not allowed, or split size indicator is larger than maximum allowed. */
                        bool kickClient = false;
                        //Clients are limited to MTU size.
                        if (_limitClientMTU)
                        {
                            kickClient = true;
                        }
                        //Clients may exceed MTU to some value.
                        else
                        {
                            //If a maximum is set.
                            if (_maximumClientMTU > 0)
                            {
                                /* A quick check can be performed to see if
                                 * this packet + already written will exceed maximum client mtu.
                                 * If it does not, then read the expected splits
                                 * to see if those may exceed mtu. 
                                 * 
                                 * The goal is to kick quick as possible when
                                 * the client is sending too much data. If expected splits
                                 * will definitely exceed data then kick, or if next incoming
                                 * data would also exceed, kick.*/
                                if (_splitReader.Position + args.Data.Count > _maximumClientMTU)
                                {
                                    kickClient = true;
                                }
                                else
                                {
                                    //How many splits would be too many.
                                    int mtu = NetworkManager.TransportManager.Transport.GetMTU((byte)Channel.Reliable);
                                    /* A split will always be in at least 2 segments
                                     * so use a minimum of 2 segments when calculating
                                     * how many splits are allowed. This is here should the user
                                     * set maximumClientMTU to less than the MTU at runtime. */
                                    uint maxAllowedSplits = (uint)Math.Max(Mathf.CeilToInt(_maximumClientMTU / mtu), 2);

                                    ushort expected;
                                    _splitReader.ReadHeader(reader, true, out _, out expected);
                                    if (expected > maxAllowedSplits)
                                        kickClient = true;
                                }
                            }

                        }

                        /* Client did not pass checks.
                         * There are other ways to exploit the checks such
                         * as sending the same packet multiple times. This would
                         * result in the server trying to add onto the split
                         * over and over. However, once the server receives expected
                         * amount of splits it will process the data, and if
                         * the data is malformed client gets kicked anyway. */
                        if (kickClient)
                        {
                            NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
                            return;
                        }

                        ArraySegment<byte> result =
                            _splitReader.Write(reader,
                            NetworkManager.TransportManager.Transport.GetMTU((byte)args.Channel)
                            );

                        /* If there is no data in result then the split isn't fully received.
                         * Since splits arrive in reliable order exit method and wait for next
                         * packet. Once all packets are received the data will be processed. */
                        if (result.Count == 0)
                            return;
                        //Split has been read in full.
                        else
                            reader.Initialize(result, NetworkManager);
                    }


                    while (reader.Remaining > 0)
                    {
                        packetId = (PacketId)reader.ReadByte();
                        ///<see cref="FishNet.Managing.Client.ClientManager.ParseReceived"/>
                        int dataLength = (args.Channel == Channel.Reliable || packetId == PacketId.Broadcast) ?
                            -1 : reader.ReadInt16();

                        NetworkConnection conn;

                        /* Connection isn't available. This should never happen.
                         * Force an immediate disconnect. */
                        if (!Clients.TryGetValue(args.ConnectionId, out conn))
                        {
                            NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, true);
                            return;
                        }
                        /* If connection isn't authenticated and isn't a broadcast
                         * then disconnect client. If a broadcast then process
                         * normally; client may still become disconnected if the broadcast
                         * does not allow to be called while not authenticated. */
                        if (!conn.Authenticated && packetId != PacketId.Broadcast)
                        {
                            conn.Disconnect(true);
                            return;
                        }

                        if (packetId == PacketId.ServerRpc)
                        {
                            Objects.ParseServerRpc(reader, args.ConnectionId, dataLength, args.Channel);
                        }
                        else if (packetId == PacketId.Broadcast)
                        {
                            ParseBroadcast(reader, args.ConnectionId);
                        }
                        else
                        {
                            if (NetworkManager.CanLog(Logging.LoggingType.Error))
                                Debug.LogError($"Server received an unhandled PacketId of {(byte)packetId} from connectionId {args.ConnectionId}. Remaining data has been purged.");
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Error))
                    Debug.LogError($"Server encountered an error while parsing data for packetId {packetId} from connectionId {args.ConnectionId}. Message: {e.Message}.");
            }
            finally { }

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
            SendConnectionId(connection);

            OnAuthenticationResult?.Invoke(connection, true);
            NetworkManager.SceneManager.OnClientAuthenticated(connection);
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            _maximumClientMTU = Mathf.Clamp(_maximumClientMTU, 0, int.MaxValue);
        }
#endif
    }


}
