using System;
using UnityEngine;

namespace FishNet.Transporting
{

    public abstract class Transport : MonoBehaviour
    {

        #region Initialization and unity.
        protected virtual void Awake()
        {
            Application.quitting += Application_Quitting;
        }

        /// <summary>
        /// Called when application quits.
        /// </summary>
        protected virtual void Application_Quitting()
        {
            Shutdown();
        }
        #endregion
        
        #region ConnectionStates.
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
        /// <param name="connectionStateArgs"></param>
        public abstract void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs);
        /// <summary>
        /// Handles a ConnectionStateArgs for the local server.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        public abstract void HandleServerConnectionState(ServerConnectionStateArgs connectionStateArgs);
        /// <summary>
        /// Handles a ConnectionStateArgs for a remote client.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
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
        public abstract LocalConnectionStates GetConnectionState(int connectionId);
        #endregion

        #region Sending.
        /// <summary>
        /// Sends to the server.
        /// </summary>
        /// <param name="toServer">True if sending to the server.</param>        
        /// <param name="channelId">Channel to use.</param>
        /// /// <param name="segment">Data to send.</param>
        /// <param name="connectionId">ConnectionId to send to. When sending to clients can be used to specify which connection to send to.</param>
        public abstract void SendToServer(byte channelId, ArraySegment<byte> segment);
        /// <summary>
        /// Sends to a client.
        /// </summary>
        /// <param name="channelId">Channel to use.</param>
        /// /// <param name="segment">Data to send.</param>
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
        /// <param name="receivedDataArgs"></param>
        public abstract void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs);
        /// <summary>
        /// Called when the server receives data.
        /// </summary>
        public abstract event Action<ServerReceivedDataArgs> OnServerReceivedData;
        /// <summary>
        /// Handles a ServerReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs"></param> //muchlater convert events to a loop to read directly from transport.
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
        /// Sets which address the client will connect to.
        /// </summary>
        /// <param name="address"></param>
        public abstract void SetClientAddress(string address);
        /// <summary>
        /// Sets which address the server will bind to.
        /// </summary>
        /// <param name="address"></param>
        public abstract void SetServerBindAddress(string address);
        /// <summary>
        /// Sets which port to use.
        /// </summary>
        /// <param name="port"></param>
        public abstract void SetPort(ushort port);
        #endregion

        #region Start and stop.
        /// <summary>
        /// Starts the local server or client using configured settings.
        /// </summary>
        /// <param name="server">True to start server.</param>
        public abstract void StartConnection(bool server);
        /// <summary>
        /// Starts the local client.
        /// </summary>
        /// <param name="address">Address to connect to.</param>
        public abstract void StartConnection(string address);
        /// <summary>
        /// Stops the local server or client.
        /// </summary>
        /// <param name="server">True to stop server.</param>
        public abstract void StopConnection(bool server);
        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        public abstract void StopConnection(int connectionId, bool immediately);
        /// <summary>
        /// Stops both client and server.
        /// </summary>
        public abstract void Shutdown();
        #endregion

        #region Channels.
        /// <summary>
        /// Returns how many channels the transport is using.
        /// </summary>
        /// <returns></returns>
        public abstract byte GetChannelCount();
        /// <summary>
        /// Returns which channel to use by default for reliable.
        /// </summary>
        public abstract byte GetDefaultReliableChannel();
        /// <summary>
        /// Returns which channel to use by default for unreliable.
        /// </summary>
        public abstract byte GetDefaultUnreliableChannel();
        /// <summary>
        /// Gets the MTU for a channel. This should take header size into consideration.
        /// For example, if MTU is 1200 and a packet header for this channel is 10 in size, this method should return 1190.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public abstract int GetMTU(byte channel);
        #endregion

    }
}
