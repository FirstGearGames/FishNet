//PROSTART
using FishNet.Managing;
using FishNet.Managing.Logging;
using System;
using UnityEngine;

namespace FishNet.Transporting.Yak
{
    [AddComponentMenu("FishNet/Transport/Yak")]
    public class Yak : Transport
    {
        #region Private. 
        /// <summary>
        /// Client when acting as host.
        /// </summary>
        private Client.ClientSocket _client;
        /// <summary>
        /// Server for the transport.
        /// </summary>
        private Server.ServerSocket _server;
        #endregion

        #region Const.
        /// <summary>
        /// Id to use for client when acting as host.
        /// </summary>
        internal const int CLIENT_HOST_ID = short.MaxValue;
        private const int MTU = 5000;
        #endregion

        public override void Initialize(NetworkManager networkManager, int transportIndex)
        {
            base.Initialize(networkManager, transportIndex);

            _client = new Client.ClientSocket();
            _server = new Server.ServerSocket();

            _client.Initialize(this, _server);
            _server.Initialize(this, _client);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        #region ConnectionStates.
        /// <summary>
        /// Gets the IP address of a remote connection Id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        public override string GetConnectionAddress(int connectionId)
        {
            return String.Empty;
        }
        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        /// <summary>
        /// Called when a connection state changes for the local server.
        /// </summary>
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        /// <summary>
        /// Gets the current local ConnectionState.
        /// </summary>
        /// <param name="server">True if getting ConnectionState for the server.</param>
        public override LocalConnectionState GetConnectionState(bool server)
        {
            if (server)
                return _server.GetLocalConnectionState();
            else
                return _client.GetLocalConnectionState();
        }
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        public override RemoteConnectionState GetConnectionState(int connectionId)
        {
            return _server.GetConnectionState(connectionId);
        }
        /// <summary>
        /// Handles a ConnectionStateArgs for the local client.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        public override void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs)
        {
            OnClientConnectionState?.Invoke(connectionStateArgs);
        }
        /// <summary>
        /// Handles a ConnectionStateArgs for the local server.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        public override void HandleServerConnectionState(ServerConnectionStateArgs connectionStateArgs)
        {
            OnServerConnectionState?.Invoke(connectionStateArgs);
        }
        /// <summary>
        /// Handles a ConnectionStateArgs for a remote client.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        public override void HandleRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs)
        {
            OnRemoteConnectionState?.Invoke(connectionStateArgs);
        }
        #endregion

        #region Iterating.
        /// <summary>
        /// Processes data received by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public override void IterateIncoming(bool server)
        {
            if (server)
                _server.IterateIncoming();
            else
                _client.IterateIncoming();
        }

        /// <summary>
        /// Processes data to be sent by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public override void IterateOutgoing(bool server) { }
        #endregion

        #region ReceivedData.
        /// <summary>
        /// Called when client receives data.
        /// </summary>
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        /// <summary>
        /// Handles a ClientReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs"></param>
        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs)
        {
            OnClientReceivedData?.Invoke(receivedDataArgs);
        }
        /// <summary>
        /// Called when server receives data.
        /// </summary>
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;
        /// <summary>
        /// Handles a ClientReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs"></param>
        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs receivedDataArgs)
        {
            OnServerReceivedData?.Invoke(receivedDataArgs);
        }
        #endregion

        #region Sending.
        /// <summary>
        /// Sends to the server or all clients.
        /// </summary>
        /// <param name="channelId">Channel to use.</param>
        /// /// <param name="segment">Data to send.</param>
        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            _client.SendToServer(channelId, segment);
        }
        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <param name="connectionId"></param>
        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            _server.SendToClient(channelId, segment, connectionId);
        }
        #endregion

        #region Configuration.
        /// <summary>
        /// Returns if the transport is a local transport.
        /// While true several security checks are disabled.
        /// </summary>
        public override bool IsLocalTransport(int connectionId) => true;
        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server. If the transport does not support this method the value -1 is returned.
        /// </summary>
        /// <returns></returns>
        public override int GetMaximumClients()
        {
            return short.MaxValue;
        }
        /// <summary>
        /// Sets maximum number of clients allowed to connect to the server. If applied at runtime and clients exceed this value existing clients will stay connected but new clients may not connect.
        /// </summary>
        /// <param name="value"></param>
        public override void SetMaximumClients(int value) { }
        /// <summary>
        /// Sets which address the client will connect to.
        /// </summary>
        /// <param name="address"></param>
        public override void SetClientAddress(string address) { }
        /// <summary>
        /// Sets which address the server will bind to.
        /// </summary>
        /// <param name="address"></param>
        public override void SetServerBindAddress(string address, IPAddressType addressType) { }
        /// <summary>
        /// Sets which port to use.
        /// </summary>
        /// <param name="port"></param>
        public override void SetPort(ushort port) { }
        #endregion

        #region Start and stop.
        /// <summary>
        /// Starts the local server or client using configured settings.
        /// </summary>
        /// <param name="server">True to start server.</param>
        public override bool StartConnection(bool server)
        {
            if (server)
                return StartServer();
            else
                return StartClient();
        }

        /// <summary>
        /// Stops the local server or client.
        /// </summary>
        /// <param name="server">True to stop server.</param>
        public override bool StopConnection(bool server)
        {
            if (server)
                return StopServer();
            else
                return StopClient();
        }

        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        public override bool StopConnection(int connectionId, bool immediately)
        {
            return StopClient(connectionId, immediately);
        }

        /// <summary>
        /// Stops both client and server.
        /// </summary>
        public override void Shutdown()
        {
            //Stops client then server connections.
            StopConnection(false);
            StopConnection(true);
        }

        #region Privates.
        /// <summary>
        /// Starts server.
        /// </summary>
        /// <returns>True if there were no blocks. A true response does not promise a socket will or has connected.</returns>
        private bool StartServer()
        {
            if (_server.GetLocalConnectionState() != LocalConnectionState.Stopped)
            {
                NetworkManager.LogError("Server is already running.");
                return false;
            }

            bool result = _server.StartConnection();

            return result;
        }

        /// <summary>
        /// Stops server.
        /// </summary>
        private bool StopServer()
        {
            if (_server != null)
                return _server.StopConnection();

            return false;
        }

        /// <summary>
        /// Starts the client.
        /// </summary>
        /// <param name="address"></param>
        /// <returns>True if there were no blocks. A true response does not promise a socket will or has connected.</returns>
        private bool StartClient()
        {
            if (_client.GetLocalConnectionState() != LocalConnectionState.Stopped)
            {
                NetworkManager.LogError("Client is already running.");
                return false;
            }

            _client.StartConnection();
            return true;
        }

        /// <summary>
        /// Stops the client.
        /// </summary>
        private bool StopClient()
        {
            if (_client != null)
                return _client.StopConnection();
            return false;
        }

        /// <summary>
        /// Stops a remote client on the server.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        private bool StopClient(int connectionId, bool immediately)
        {
            return _server.StopConnection(connectionId);
        }
        #endregion
        #endregion

        #region Channels.        
        /// <summary>
        /// Gets the MTU for a channel. This should take header size into consideration.
        /// For example, if MTU is 1200 and a packet header for this channel is 10 in size, this method should return 1190.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public override int GetMTU(byte channel)
        {
            return MTU;
        }
        #endregion
    }

}
//PROEND