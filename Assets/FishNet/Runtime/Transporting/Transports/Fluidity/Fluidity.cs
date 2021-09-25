using UnityEngine;
using System;
using FishNet.Transporting;

namespace Fluidity
{
    [DisallowMultipleComponent]
    public class Fluidity : Transport
    {

        #region Serialized.
        [Header("Channels")]
        /// <summary>
        /// Transport channels to use.
        /// </summary>
        [Tooltip("Transport channels to use.")]
        [SerializeField]
        private ChannelData[] _channels;

        [Header("Server")]
        /// <summary>
        /// True to have server bind to all interfaces.
        /// </summary>
        [Tooltip("True to bind to all interfaces.")]
        [SerializeField]
        private bool _serverBindsAll = true;
        /// <summary>
        /// Bind address to use.
        /// </summary>
        [Tooltip("Bind address to use.")]
        [SerializeField]
        private string _serverBindAddress = "localhost";
        /// <summary>
        /// Port to use.
        /// </summary>
        [Tooltip("Port to use.")]
        [SerializeField]
        private ushort _port = 7770;
        /// <summary>
        /// Maximum number of players which may be connected at once.
        /// </summary>
        [Tooltip("Maximum number of players which may be connected at once.")]
        [Range(1, 4095)]
        [SerializeField]
        private ushort _maximumClients = 4095;

        [Header("Client")]
        /// <summary>
        /// Address to connect.
        /// </summary>
        [Tooltip("Address to connect.")]
        [SerializeField]
        private string _clientAddress = "localhost";
        #endregion

        #region Private.
        /// <summary>
        /// Server socket and handler.
        /// </summary>
        private Server.ServerSocket _server = new Server.ServerSocket();
        /// <summary>
        /// Client socket and handler.
        /// </summary>
        private Client.ClientSocket _client = new Client.ClientSocket();
        /// <summary>
        /// Largest MTU of available channels.
        /// </summary>
        private int _largestMTU = 0;
        #endregion

        #region Const.
        /// <summary>
        /// How long to wait before timing out a poll within ENet. For non-blocking use 0.
        /// </summary>
        private const int POLL_TIMEOUT = 1;
        #endregion

        #region Initialization and unity.
        protected override void Awake()
        {
            if (!ENet.Library.Initialize())
                Debug.LogError("Failed to initialize ENet library.");
            /* If channels are missing then add them. This 
             * can occur if component is added at runtime. */
            if (_channels == null)
            {
                _channels = new ChannelData[2]
                {
                    new ChannelData(ChannelType.Reliable, 1200),
                    new ChannelData(ChannelType.Unreliable, 1200)
                };
            }
            //Set largest MTU. Used to create a buffer.
            for (int i = 0; i < _channels.Length; i++)
                _largestMTU = Mathf.Max(_largestMTU, _channels[i].MaximumTransmissionUnit);

            base.Awake();
        }
        protected void OnDestroy()
        {
            ENet.Library.Deinitialize();
        }
        #endregion

        #region ConnectionStates.
        /// <summary>
        /// Gets the address of a remote connection Id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        public override string GetConnectionAddress(int connectionId)
        {
            return _server.GetConnectionAddress(connectionId);
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
        public override LocalConnectionStates GetConnectionState(bool server)
        {
            if (server)
                return _server.GetConnectionState();
            else
                return _client.GetConnectionState();
        }
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        public override RemoteConnectionStates GetConnectionState(int connectionId)
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
        public override void IterateOutgoing(bool server)
        {
            if (server)
                _server.IterateOutgoing();
            else
                _client.IterateOutgoing();
        }
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
            _client.SendToServer(channelId, segment, _channels);
        }
        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <param name="connectionId"></param>
        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            _server.SendToClient(channelId, segment, _channels, connectionId);
        }
        #endregion

        #region Configuration.
        /// <summary>
        /// Sets which address the client will connect to.
        /// </summary>
        /// <param name="address"></param>
        public override void SetClientAddress(string address)
        {
            _clientAddress = address;
        }
        /// <summary>
        /// Sets which address the server will bind to.
        /// </summary>
        /// <param name="address"></param>
        public override void SetServerBindAddress(string address)
        {
            _serverBindAddress = address;
        }
        /// <summary>
        /// Sets which port to use.
        /// </summary>
        /// <param name="port"></param>
        public override void SetPort(ushort port)
        {
            _port = port;
        }
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
                return StartClient(_clientAddress);
        }

        /// <summary>
        /// Starts the local client.
        /// </summary>
        /// <param name="address">Address to connect to.</param>
        public override bool StartConnection(string address)
        {
            return StartClient(address);
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
        private bool StartServer()
        {
            _server.Initialize(this, _channels);
            string bindAddress = (_serverBindsAll) ? GlobalConstants.BIND_ALL : _serverBindAddress;
            return _server.StartConnection(bindAddress, _port, _maximumClients, (byte)_channels.Length, POLL_TIMEOUT);
        }

        /// <summary>
        /// Stops server.
        /// </summary>
        private bool StopServer()
        {
            return _server.StopConnection();
        }

        /// <summary>
        /// Starts the client.
        /// </summary>
        /// <param name="address"></param>
        private bool StartClient(string address)
        {
            _client.Initialize(this, _channels);
            return _client.StartConnection(address, _port, (byte)_channels.Length, POLL_TIMEOUT);
        }

        /// <summary>
        /// Stops the client.
        /// </summary>
        private bool StopClient()
        {
            return _client.StopConnection();
        }

        /// <summary>
        /// Stops a remote client on the server.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        private bool StopClient(int connectionId, bool immediately)
        {
            return _server.StopConnection(connectionId, immediately);
        }
        #endregion
        #endregion

        #region Channels.
        /// <summary>
        /// Returns how many channels the transport is using.
        /// </summary>
        /// <returns></returns>
        public override byte GetChannelCount()
        {
            return (byte)_channels.Length;
        }
        /// <summary>
        /// Returns which channel to use by default for reliable.
        /// </summary>
        public override byte GetDefaultReliableChannel()
        {
            return 0;
        }
        /// <summary>
        /// Returns which channel to use by default for unreliable.
        /// </summary>
        public override byte GetDefaultUnreliableChannel()
        {
            return 1;
        }
        /// <summary>
        /// Gets the MTU for a channel. This should take header size into consideration.
        /// For example, if MTU is 1200 and a packet header for this channel is 10 in size, this method should return 1190.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public override int GetMTU(byte channel)
        {
            if (channel >= _channels.Length)
            {
                Debug.LogError($"Channel {channel} is out of bounds.");
                return 0;
            }

            return _channels[channel].MaximumTransmissionUnit;
        }
        #endregion

        #region Editor.
#if UNITY_EDITOR
        private void OnValidate()
        {
            /* Force default channel settings. */
            //Make sure array is long enough.
            if (_channels == null || _channels.Length < 2)
                _channels = new ChannelData[2];
            //Set to defaults.
            _channels[0] = new ChannelData(ChannelType.Reliable, 1200);
            _channels[1] = new ChannelData(ChannelType.Unreliable, 1200);
        }

#endif
        #endregion

    }
}
