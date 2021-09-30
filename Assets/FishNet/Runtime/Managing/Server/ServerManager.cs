using FishNet.Managing.Server.Object;
using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Authenticating;

namespace FishNet.Managing.Server
{
    [DisallowMultipleComponent]
    public partial class ServerManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called after local server connection state changes. This is performed after the state change has been handled internally.
        /// </summary>
        public event Action<ServerConnectionStateArgs> OnServerConnectionState;
        /// <summary>
        /// Called when authenitcator has concluded a result for a connection. Boolean is true if authentication passed, false if failed.
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
        /// ObjectHandler for server objects.
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

            if (_startOnHeadless && Application.isBatchMode)
                NetworkManager.TransportManager.Transport.StartConnection(true);
        }

        /// <summary>
        /// Called when a client loads initial scenes after connecting.
        /// </summary>
        private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn)
        {
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
                Debug.Log("Server started."); //tmp.
            else if (args.ConnectionState == LocalConnectionStates.Stopped)
                Debug.Log("Server stopped."); //tmp.

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
                    Debug.Log($"Remote connection started for Id {args.ConnectionId}."); //tmp.
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
                        OnRemoteConnectionState?.Invoke(conn, args);

                        Clients.Remove(args.ConnectionId);
                        Objects.ClientDisconnected(conn);
                        conn.Reset();

                        Debug.Log($"Remote connection stopped for Id {args.ConnectionId}."); //tmp.
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
                writer.WriteInt32(conn.ClientId);
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
                    while (reader.Remaining > 0)
                    {
                        packetId = (PacketId)reader.ReadByte();

                        ///<see cref="FishNet.Managing.Client.ClientManager.ParseReceived"/>
                        int dataLength = (args.Channel == Channel.Reliable || packetId == PacketId.Broadcast) ?
                            -1 : reader.ReadInt32();

                        NetworkConnection conn;
                        Clients.TryGetValue(args.ConnectionId, out conn);
                        /* Connection isn't available. This should never happen.
                         * Force an immediate disconnect. */
                        if (conn == null)
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
                            Objects.ParseServerRpc(reader, args.ConnectionId, dataLength);
                        }
                        else if (packetId == PacketId.Broadcast)
                        {
                            ParseBroadcast(reader, args.ConnectionId);
                        }
                        else
                        {
                            Debug.LogError($"Unhandled PacketId of {(byte)packetId}. Remaining data has been purged.");
                            return;
                        }
                    }
                }
            }
            //catch (Exception e)
            //{
            //    Debug.LogError($"Error parsing data. PacketId {packetId}. {e.Message}.");
            //}
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

    }


}
