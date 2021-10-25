using UnityEngine;
using System;
using FishNet.Transporting;
using FishNet.Managing;
using FishNet.Managing.Logging;
using System.Runtime.CompilerServices;

namespace Fluidity
{
    [DisallowMultipleComponent]
    public class Fluidity : Transport
    {

        #region Serialized.
        [Header("Channels")]
        /// <summary>
        /// Maximum transmission unit for the reliable channel.
        /// </summary>
        [Tooltip("Maximum transmission unit for the reliable channel.")]
        [SerializeField]
        private int _reliableMTU = 1200;
        /// <summary>
        /// Maximum transmission unit for the unreliable channel.
        /// </summary>
        [Tooltip("Maximum transmission unit for the unreliable channel.")]
        [SerializeField]
        private int _unreliableMTU = 1200;

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
        private int _maximumClients = 4095;

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
        /// <summary>
        /// Number of Fluidity components initialized.
        /// </summary>
        private static uint _fluidityInitializedCount = 0;
        #endregion

        #region Const.
        /// <summary>
        /// How long to wait before timing out a poll within ENet. For non-blocking use 0.
        /// </summary>
        private const int POLL_TIMEOUT = 1;
        #endregion

        #region Initialization and unity.
        public override void Initialize(NetworkManager networkManager)
        {
            base.Initialize(networkManager);

            if (_fluidityInitializedCount == 0)
            {
                if (!ENet.Library.Initialize())
                {
                    if (base.NetworkManager.CanLog(LoggingType.Error))
                    {
                        Debug.LogError("Failed to initialize ENet library.");
                        return;
                    }
                }
            }
            _fluidityInitializedCount++;

            //Set largest MTU. Used to create a buffer.
            _largestMTU = Mathf.Max(_reliableMTU, _unreliableMTU);

            base.Initialize(networkManager);
        }

        protected void OnDestroy()
        {
            Shutdown();
            if (_fluidityInitializedCount > 0)
            {
                _fluidityInitializedCount--;
                if (_fluidityInitializedCount == 0)
                    ENet.Library.Deinitialize();
            }
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
        /// <param name="segment">Data to send.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            SanitizeChannel(ref channelId);
            _client.SendToServer(channelId, segment);
        }
        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <param name="connectionId"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            SanitizeChannel(ref channelId);
            _server.SendToClient(channelId, segment, connectionId);
        }
        #endregion

        #region Configuration.
        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server. If the transport does not support this method the value -1 is returned.
        /// </summary>
        /// <returns></returns>
        public override int GetMaximumClients()
        {
            return _server.GetMaximumClients();
        }
        /// <summary>
        /// Sets maximum number of clients allowed to connect to the server. If applied at runtime and clients exceed this value existing clients will stay connected but new clients may not connect.
        /// </summary>
        /// <param name="value"></param>
        public override void SetMaximumClients(int value)
        {
            if (_server.GetConnectionState() != LocalConnectionStates.Stopped)
            {
                if (base.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Cannot set maximum clients when server is running.");
            }
            else
            {
                _maximumClients = value;
            }
        }
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
            _server.Initialize(this, _reliableMTU, _unreliableMTU);
            string bindAddress = (_serverBindsAll) ? GlobalConstants.BIND_ALL : _serverBindAddress;
            return _server.StartConnection(bindAddress, _port, _maximumClients, GetChannelCount(), POLL_TIMEOUT);
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
            _client.Initialize(this, _reliableMTU, _unreliableMTU);
            return _client.StartConnection(address, _port, GetChannelCount(), POLL_TIMEOUT);
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
        /// If channelId is invalid then channelId becomes forced to reliable.
        /// </summary>
        /// <param name="channelId"></param>
        private void SanitizeChannel(ref byte channelId)
        {
            if (channelId < 0 || channelId >= GetChannelCount())
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Channel of {channelId} is out of range of supported channels. Channel will be defaulted to reliable.");
                channelId = GetDefaultReliableChannel();
            }
        }
        /// <summary>
        /// Returns how many channels the transport is using.
        /// </summary>
        /// <returns></returns>
        public override byte GetChannelCount()
        {
            return 2;
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
            if (channel == 0)
            {
                return _reliableMTU;
            }
            else if (channel == 1)
            {
                return _unreliableMTU;
            }
            else
            {
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Channel {channel} is out of bounds.");
                return 0;
            }
        }
        #endregion

        #region Editor.
#if UNITY_EDITOR
        private void OnValidate()
        {
            _reliableMTU = Math.Max(0, _reliableMTU);
            _unreliableMTU = Math.Max(0, _unreliableMTU);
        }
#endif
        #endregion
    }
}
