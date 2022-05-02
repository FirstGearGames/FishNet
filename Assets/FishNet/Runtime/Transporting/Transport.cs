using FishNet.Managing;
using FishNet.Managing.Logging;
using System;
using UnityEngine;

namespace FishNet.Transporting
{

    /// <summary>
    /// Processes connection states, and data sent to and from a socket.
    /// </summary>
    public abstract class Transport : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// NetworkManager for this transport.
        /// </summary>
        public NetworkManager NetworkManager { get; private set; }
        /// <summary>
        /// Index this transport belongs to when using multiple transports at once.
        /// </summary>
        public int Index { get; private set; }
        #endregion

        #region Initialization and unity.
        /// <summary>
        /// Initializes the transport. Use this instead of Awake.
        /// </summary>
        [Obsolete("This method is being replaced with Initialize(networkManager, transportIndex) to support multiple transports at once.")]  //Remove on 2022/06/01 in favor of AllowStacking.
        public virtual void Initialize(NetworkManager networkManager)
        {
            NetworkManager = networkManager;
        }
        /// <summary>
        /// Initializes the transport. Use this instead of Awake.
        /// <paramref name="transportIndex"/>Index this transport belongs to when using multiple transports at once.</param>
        /// </summary>
        public virtual void Initialize(NetworkManager networkManager, int transportIndex)
        {
            NetworkManager = networkManager;
            Index = transportIndex;
        }
        #endregion

        #region ConnectionStates.
        /// <summary>
        /// Gets the address of a remote connection Id.
        /// </summary>
        /// <param name="connectionId">Connectionid to get the address for.</param>
        /// <returns></returns>
        public abstract string GetConnectionAddress(int connectionId);
        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        public abstract event Action<ClientConnectionStateArgs> OnClientConnectionState;
        /// <summary>
        /// Called when a connection state changes for the local server.
        /// </summary>
        public abstract event Action<ServerConnectionStateArgs> OnServerConnectionState;
        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        public abstract event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        /// <summary>
        /// Handles a ConnectionStateArgs for the local client.
        /// </summary>
        /// <param name="connectionStateArgs">Data being handled.</param>
        public abstract void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs);
        /// <summary>
        /// Handles a ConnectionStateArgs for the local server.
        /// </summary>
        /// <param name="connectionStateArgs">Data being handled.</param>
        public abstract void HandleServerConnectionState(ServerConnectionStateArgs connectionStateArgs);
        /// <summary>
        /// Handles a ConnectionStateArgs for a remote client.
        /// </summary>
        /// <param name="connectionStateArgs">Data being handled.</param>
        public abstract void HandleRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs);
        /// <summary>
        /// Gets the current local ConnectionState.
        /// </summary>
        /// <param name="server">True if getting ConnectionState for the server.</param>
        public abstract LocalConnectionStates GetConnectionState(bool server);
        /// <summary>
        /// Gets the current ConnectionState of a client connected to the server. Can only be called on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        public abstract RemoteConnectionStates GetConnectionState(int connectionId);
        #endregion

        #region Sending.
        /// <summary>
        /// Sends to the server.
        /// </summary>
        /// <param name="channelId">Channel to use.</param>
        /// <param name="segment">Data to send.</param>
        public abstract void SendToServer(byte channelId, ArraySegment<byte> segment);
        /// <summary>
        /// Sends to a client.
        /// </summary>
        /// <param name="channelId">Channel to use.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="connectionId">ConnectionId to send to. When sending to clients can be used to specify which connection to send to.</param>
        public abstract void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId);
        #endregion

        #region Receiving
        /// <summary>
        /// Called when the client receives data.
        /// </summary>
        public abstract event Action<ClientReceivedDataArgs> OnClientReceivedData;
        /// <summary>
        /// Handles a ClientReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs">Data being handled.</param>
        public abstract void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs);
        /// <summary>
        /// Called when the server receives data.
        /// </summary>
        public abstract event Action<ServerReceivedDataArgs> OnServerReceivedData;
        /// <summary>
        /// Handles a ServerReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs">Data being handled.</param>
        public abstract void HandleServerReceivedDataArgs(ServerReceivedDataArgs receivedDataArgs);
        #endregion

        #region Iterating.
        /// <summary>
        /// Processes data received by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public abstract void IterateIncoming(bool server);
        /// <summary>
        /// Processes data to be sent by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public abstract void IterateOutgoing(bool server);
        #endregion

        #region Configuration.
        /// <summary>
        /// Returns if the transport is only run locally, offline.
        /// While true several security checks are disabled.
        /// </summary>
        public virtual bool IsLocalTransport(int connectionid) => false;
        /// <summary>
        /// How long in seconds until either the server or client socket must go without data before being timed out.
        /// </summary>
        /// <param name="asServer">True to get the timeout for the server socket, false for the client socket.</param>
        /// <returns></returns>
        public virtual float GetTimeout(bool asServer) => -1f;
        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server. If the transport does not support this method the value -1 is returned.
        /// </summary>
        /// <returns>Maximum clients transport allows.</returns>
        public virtual int GetMaximumClients()
        {
            bool canLog = (NetworkManager == null) ? NetworkManager.StaticCanLog(LoggingType.Warning) : NetworkManager.CanLog(LoggingType.Warning);
            if (canLog)
                Debug.LogWarning($"The current transport does not support this feature.");
            return -1;
        }
        /// <summary>
        /// Sets the maximum number of clients allowed to connect to the server. If applied at runtime and clients exceed this value existing clients will stay connected but new clients may not connect.
        /// </summary>
        /// <param name="value">Maximum clients to allow.</param>
        public virtual void SetMaximumClients(int value)
        {
            bool canLog = (NetworkManager == null) ? NetworkManager.StaticCanLog(LoggingType.Warning) : NetworkManager.CanLog(LoggingType.Warning);
            if (canLog)
                Debug.LogWarning($"The current transport does not support this feature.");
        }
        /// <summary>
        /// Sets which address the client will connect to.
        /// </summary>
        /// <param name="address">Address client will connect to.</param>
        public virtual void SetClientAddress(string address) { }
        /// <summary>
        /// Returns which address the client will connect to.
        /// </summary>
        public virtual string GetClientAddress() => string.Empty;
        /// <summary>
        /// Sets which address the server will bind to.
        /// </summary>
        /// <param name="address">Address server will bind to.</param>
        [Obsolete("Use SetServerBindAddress(IPAddressType)")] //Remove on 01/01/2023
        public virtual void SetServerBindAddress(string address) { }
        /// <summary>
        /// Gets which address the server will bind to.
        /// </summary>
        [Obsolete("Use GetServerBindAddress(IPAddressType)")] //Remove on 01/01/2023
        public virtual string GetServerBindAddress() => string.Empty;
        /// <summary>
        /// Sets which address the server will bind to.
        /// </summary>
        /// <param name="address">Address server will bind to.</param>
        /// <param name="addressType">Address type to set.</param>
        public virtual void SetServerBindAddress(string address, IPAddressType addressType) { }
        /// <summary>
        /// Gets which address the server will bind to.
        /// </summary>
        /// <param name="addressType">Address type to return.</param>
        public virtual string GetServerBindAddress(IPAddressType addressType) => string.Empty;
        /// <summary>
        /// Sets which port to use.
        /// </summary>
        /// <param name="port">Port to use.</param>
        public virtual void SetPort(ushort port) { }
        /// <summary>
        /// Gets which port to use.
        /// </summary>
        public virtual ushort GetPort() => 0;
        #endregion

        #region Start and stop.
        /// <summary>
        /// Starts the local server or client using configured settings.
        /// </summary>
        /// <param name="server">True to start server.</param>
        public abstract bool StartConnection(bool server);
        /// <summary>
        /// Stops the local server or client.
        /// </summary>
        /// <param name="server">True to stop server.</param>
        public abstract bool StopConnection(bool server);
        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        public abstract bool StopConnection(int connectionId, bool immediately);
        /// <summary>
        /// Stops both client and server.
        /// </summary>
        public abstract void Shutdown();
        #endregion

        #region Channels.
        /// <summary>
        /// Gets the number of channels this transport supports.
        /// </summary>
        /// <returns></returns>
        [Obsolete("This method is being removed in a future version. Channel count will always be 2, for reliable and unreliable.")] //Remove on 2022/06/01.
        public virtual byte GetChannelCount() { return 2; }
        /// <summary>
        /// Returns which channel to use by default for reliable.
        /// </summary>
        /// <returns>Channel as byte.</returns>
        [Obsolete("This method will be removed. Reliable will always be 0, and unreliable always 1.")]
        public virtual byte GetDefaultReliableChannel() { return 0; }  //Remove on 2022/06/01.
        /// <summary>
        /// Returns which channel to use by default for unreliable.
        /// </summary>
        /// <returns>Channel as byte.</returns>
        [Obsolete("This method will be removed. Reliable will always be 0, and unreliable always 1.")]
        public virtual byte GetDefaultUnreliableChannel() { return 1; } //Remove on 2022/06/01.
        /// <summary>
        /// Gets the MTU for a channel.
        /// </summary>
        /// <param name="channel">Channel to get MTU for.</param>
        /// <returns>MTU of channel.</returns>
        public abstract int GetMTU(byte channel);
        #endregion

    }
}
