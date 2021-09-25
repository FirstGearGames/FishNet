#if !DISABLESTEAMWORKS
using FishNet.Transporting;
using Steamworks;
using System;
using System.IO;
using UnityEngine;

namespace FishySteamworks
{    
    public class FishySteamworks : Transport
    {
        #region Public.
        /// <summary>
        /// The SteamId for the local user after connecting to or starting the server. This is populated automatically.
        /// </summary>
        [System.NonSerialized]
        public ulong LocalUserSteamID = 0;
        #endregion

        #region Serialized.
        /// <summary>
        /// Steam application Id.
        /// </summary>
        [Tooltip("Steam application Id.")]
        [SerializeField]
        private ulong _steamAppID = 480;
        /// <summary>
        /// Address server should bind to.
        /// </summary>
        [Tooltip("Address server should bind to.")]
        [SerializeField]
        private string _serverBindAddress = string.Empty;
        [SerializeField]
        /// <summary>
        /// True if using peer to peer socket.
        /// </summary>
        private bool _peerToPeer = false;
        /// <summary>
        /// Address client should connect to.
        /// </summary>
        [Tooltip("Address client should connect to.")]
        [SerializeField]
        private string _clientAddress = string.Empty;
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
        [Range(1, ushort.MaxValue)]
        [SerializeField]
        private ushort _maximumClients = 4095;
        #endregion

        #region Private.
        /// <summary>
        /// Transport channels to use.
        /// </summary>
        private ChannelData[] _channels;
        /// <summary>
        /// Client for the transport.
        /// </summary>
        private Client.ClientSocket _client = new Client.ClientSocket();
        /// <summary>
        /// Server for the transport.
        /// </summary>
        private Server.ServerSocket _server = new Server.ServerSocket();
        /// <summary>
        /// Next time SteamId can be fetched.
        /// </summary>
        private float _nextGetIdTime = -1f;
        #endregion

        #region Const.
        /// <summary>
        /// How often to try and get SteamId.
        /// </summary>
        private const float GET_ID_INTERVAL = 1f;
        #endregion

        protected override void Awake()
        {
            base.Awake();
            CreateChannelData();
            WriteSteamAppId();
            _client.Initialize(this);
            _server.Initialize(this);
        }


        private void OnEnable()
        {
            _nextGetIdTime = Time.unscaledTime + GET_ID_INTERVAL;
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Update()
        {
            TrySetLocalUserSteamId();
        }

        #region Setup.
        /// <summary>
        /// Creates ChannelData for the transport.
        /// </summary>
        private void CreateChannelData()
        {
            _channels = new ChannelData[2]
            {
                new ChannelData(Channel.Reliable, 1048576),
                new ChannelData(Channel.Unreliable, 1200)
            };
        }
        /// <summary>
        /// Writes SteamAppId to file.
        /// </summary>
        private void WriteSteamAppId()
        {
            string fileName = "steam_appid.txt";
            string appIdText = _steamAppID.ToString();
            try
            {
                if (File.Exists(fileName))
                {
                    string content = File.ReadAllText(fileName);
                    if (content != appIdText)
                    {
                        File.WriteAllText(fileName, appIdText);
                        Debug.Log($"SteamId has been updated from {content} to {appIdText} within {fileName}.");
                    }
                }
                else
                {
                    File.WriteAllText(fileName, appIdText);
                    Debug.Log($"SteamId {appIdText} has been set within {fileName}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"There was an exception when trying to write {appIdText} to {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Tries to set LocalSteamId.
        /// </summary>
        private void TrySetLocalUserSteamId()
        {
            if (LocalUserSteamID != 0)
                return;
            if (_nextGetIdTime == -1 || Time.unscaledTime < _nextGetIdTime)
                return;

            _nextGetIdTime = Time.unscaledTime + GET_ID_INTERVAL;
            SetUserSteamID();
        }

        /// <summary>
        /// Sets LocalUserSteamId if available.
        /// </summary>
        private void SetUserSteamID()
        {
            if (SteamManager.Initialized)
            {
                SteamNetworkingUtils.InitRelayNetworkAccess();
                LocalUserSteamID = SteamUser.GetSteamID().m_SteamID;
            }
        }
        #endregion

        #region ConnectionStates.
        /// <summary>
        /// Gets the IP address of a remote connection Id.
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
        /// <returns>True if there were no blocks. A true response does not promise a socket will or has connected.</returns>
        private bool StartServer()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("SteamWorks not initialized. Client could not be started.");
                return false;
            }
            if (_client.GetConnectionState() != LocalConnectionStates.Stopped)
            {
                Debug.LogError("Server cannot run while client is running.");
                return false;
            }
            if (_server.GetConnectionState() != LocalConnectionStates.Stopped)
            {
                Debug.LogError("Server is already running.");
                return false;
            }

            _server.StartConnection(_serverBindAddress, _port, _maximumClients, _peerToPeer);
            return true;
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
        /// <returns>True if there were no blocks. A true response does not promise a socket will or has connected.</returns>
        private bool StartClient(string address)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("SteamWorks not initialized. Client could not be started.");
                return false;
            }
            if (_server.GetConnectionState() != LocalConnectionStates.Stopped)
            {
                Debug.LogError("Client cannot run while server is running.");
                return false;
            }
            if (_client.GetConnectionState() != LocalConnectionStates.Stopped)
            {
                Debug.LogError("Client is already running.");
                return false;
            }

            SetUserSteamID();
            _client.StartConnection(address, _port, _peerToPeer);
            return true;
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
            return _server.StopConnection(connectionId);
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

    }
}
#endif // !DISABLESTEAMWORKS