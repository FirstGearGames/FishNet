using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Transporting;
using LiteNetLib;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Transporting.Tugboat
{
    [DisallowMultipleComponent]
    public class Tugboat : Transport
    {

        #region Serialized.
        [Header("Channels")]
        /// <summary>
        /// Maximum transmission unit for the unreliable channel.
        /// </summary>
        [Tooltip("Maximum transmission unit for the unreliable channel.")]
        [Range(MINIMUM_UDP_MTU, MAXIMUM_UDP_MTU)]
        [SerializeField]
        private int _unreliableMTU = 1023;

        [Header("Server")]
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How the server should respond when it suspects a client is performing an attack.")]
        [SerializeField]
        private AttackResponseType _attackResponseType = AttackResponseType.WarnAndKick;
        /// <summary>
        /// How the server should respond when it suspects a client is performing an attack.
        /// </summary>
        public AttackResponseType AttackResponseType => _attackResponseType;
        /// <summary>
        /// IPv4 address to bind server to.
        /// </summary>
        [Tooltip("IPv4 Address to bind server to.")]
        [SerializeField]
        private string _ipv4BindAddress;
        /// <summary>
        /// IPv6 address to bind server to.
        /// </summary>
        [Tooltip("IPv6 Address to bind server to.")]
        [SerializeField]
        private string _ipv6BindAddress;
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
        [Range(1, 9999)]
        [SerializeField]
        private int _maximumClients = 4095;


        [Header("Client")]
        /// <summary>
        /// Address to connect.
        /// </summary>
        [Tooltip("Address to connect.")]
        [SerializeField]
        private string _clientAddress = "localhost";

        [Header("Misc")]
        /// <summary>
        /// How long in seconds until either the server or client socket must go without data before being timed out. Use 0f to disable timing out.
        /// </summary>
        [Tooltip("How long in seconds until either the server or client socket must go without data before being timed out. Use 0f to disable timing out.")]
        [Range(0, MAX_TIMEOUT_SECONDS)]
        [SerializeField]
        private ushort _timeout = 15;
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
        #endregion

        #region Const.
        private const ushort MAX_TIMEOUT_SECONDS = 1800;
        /// <summary>
        /// Minimum UDP packet size allowed.
        /// </summary>
        private const int MINIMUM_UDP_MTU = 576;
        /// <summary>
        /// Maximum UDP packet size allowed.
        /// </summary>
        private const int MAXIMUM_UDP_MTU = 1023;
        #endregion

        #region Initialization and unity.
        public override void Initialize(NetworkManager networkManager, int transportIndex)
        {
            base.Initialize(networkManager, transportIndex);
        }

        protected void OnDestroy()
        {
            Shutdown();
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
            UpdateTimeout();
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
        /// How long in seconds until either the server or client socket must go without data before being timed out.
        /// </summary>
        /// <param name="asServer">True to get the timeout for the server socket, false for the client socket.</param>
        /// <returns></returns>
        public override float GetTimeout(bool asServer)
        {
            return (asServer) ? -1f : (float)_timeout;
        }
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
        /// Gets which address the client will connect to.
        /// </summary>
        public override string GetClientAddress()
        {
            return _clientAddress;
        }

        /// <summary>
        /// Sets which address the server will bind to.
        /// </summary>
        /// <param name="address"></param>
        public override void SetServerBindAddress(string address, IPAddressType addressType)
        {
            if (addressType == IPAddressType.IPv4)
                _ipv4BindAddress = address;
            else
                _ipv6BindAddress = address;
        }
        /// <summary>
        /// Gets which address the server will bind to.
        /// </summary>
        /// <param name="address"></param>
        public override string GetServerBindAddress(IPAddressType addressType)
        {
            if (addressType == IPAddressType.IPv4)
                return _ipv4BindAddress;
            else
                return _ipv6BindAddress;
        }
        /// <summary>
        /// Sets which port to use.
        /// </summary>
        /// <param name="port"></param>
        public override void SetPort(ushort port)
        {
            _port = port;
        }
        /// <summary>
        /// Gets which port to use.
        /// </summary>
        /// <param name="port"></param>
        public override ushort GetPort()
        {
            return _port;
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
            _server.Initialize(this, _unreliableMTU);
            UpdateTimeout();
            return _server.StartConnection(_port, _maximumClients, AttackResponseType, _ipv4BindAddress, _ipv6BindAddress);
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
            _client.Initialize(this, _unreliableMTU);
            UpdateTimeout();
            return _client.StartConnection(address, _port);
        }

        /// <summary>
        /// Updates clients timeout values.
        /// </summary>
        private void UpdateTimeout()
        {
            //If server is running set timeout to max. This is for host only.
            //int timeout = (GetConnectionState(true) != LocalConnectionStates.Stopped) ? MAX_TIMEOUT_SECONDS : _timeout;
            int timeout = (Application.isEditor) ? MAX_TIMEOUT_SECONDS : _timeout;
            _client.UpdateTimeout(timeout);
            _server.UpdateTimeout(timeout);
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
            if (channelId < 0 || channelId >= TransportManager.CHANNEL_COUNT)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Channel of {channelId} is out of range of supported channels. Channel will be defaulted to reliable.");
                channelId = 0;
            }
        }
        /// <summary>
        /// Gets the MTU for a channel. This should take header size into consideration.
        /// For example, if MTU is 1200 and a packet header for this channel is 10 in size, this method should return 1190.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public override int GetMTU(byte channel)
        {
            return _unreliableMTU;
        }
        #endregion

        #region Editor.
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_unreliableMTU < 0)
                _unreliableMTU = MINIMUM_UDP_MTU;
            else if (_unreliableMTU > MAXIMUM_UDP_MTU)
                _unreliableMTU = MAXIMUM_UDP_MTU;
        }
#endif
        #endregion
    }
}
