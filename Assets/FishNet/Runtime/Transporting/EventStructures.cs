using System;

namespace FishNet.Transporting
{
    /// <summary>
    /// Container about data received on the server.
    /// </summary>
    public struct ServerReceivedDataArgs
    {
        /// <summary>
        /// Data received.
        /// </summary>
        public ArraySegment<byte> Data;
        /// <summary>
        /// Channel data was received on.
        /// </summary>
        public Channel Channel;
        /// <summary>
        /// ConnectionId from which client sent data, if data was received on the server.
        /// </summary>
        public int ConnectionId;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">Data received.</param>
        /// <param name="channel">Channel data came on.</param>
        /// <param name="connectionId">ConnectionId which sent the data.</param>
        public ServerReceivedDataArgs(ArraySegment<byte> data, Channel channel, int connectionId)
        {
            Data = data;
            Channel = channel;
            ConnectionId = connectionId;
        }
    }


    /// <summary>
    /// Container about data received on the local client.
    /// </summary>
    public struct ClientReceivedDataArgs
    {
        /// <summary>
        /// Data received.
        /// </summary>
        public ArraySegment<byte> Data;
        /// <summary>
        /// Channel data was received on.
        /// </summary>
        public Channel Channel;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data">Data received.</param>
        /// <param name="channel">Channel data came on.</param>
        public ClientReceivedDataArgs(ArraySegment<byte> data, Channel channel)
        {
            Data = data;
            Channel = channel;
        }
    }



    /// <summary>
    /// Container about a connection state change for a client.
    /// </summary>
    public struct RemoteConnectionStateArgs
    {
        /// <summary>
        /// New connection state.
        /// </summary>
        public RemoteConnectionStates ConnectionState;
        /// <summary>
        /// ConnectionId for which client the state changed. Will be -1 if ConnectionState was for the local server.
        /// </summary>
        public int ConnectionId;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionState">New connection state.</param>
        /// <param name="connectionId">ConnectionId which state is changing for.</param>

        public RemoteConnectionStateArgs(RemoteConnectionStates connectionState, int connectionId)
        {
            ConnectionState = connectionState;
            ConnectionId = connectionId;
        }
    }

    /// <summary>
    /// Container about a connection state change for the server.
    /// </summary>
    public struct ServerConnectionStateArgs
    {
        /// <summary>
        /// New connection state.
        /// </summary>
        public LocalConnectionStates ConnectionState;

        /// <summary>
        /// New connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        public ServerConnectionStateArgs(LocalConnectionStates connectionState)
        {
            ConnectionState = connectionState;
        }
    }

    /// <summary>
    /// Container about a connection state change for the local client.
    /// </summary>
    public struct ClientConnectionStateArgs
    {
        /// <summary>
        /// New connection state.
        /// </summary>
        public LocalConnectionStates ConnectionState;

        /// <summary>
        /// New connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        public ClientConnectionStateArgs(LocalConnectionStates connectionState)
        {
            ConnectionState = connectionState;
        }
    }
}

