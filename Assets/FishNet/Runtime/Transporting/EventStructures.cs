using System;

namespace FishNet.Transporting
{
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

        public ServerReceivedDataArgs(ArraySegment<byte> data, Channel channel, int connectionId)
        {
            Data = data;
            Channel = channel;
            ConnectionId = connectionId;
        }
    }


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

        public ClientReceivedDataArgs(ArraySegment<byte> data, Channel channel)
        {
            Data = data;
            Channel = channel;
        }
    }




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

        public RemoteConnectionStateArgs(RemoteConnectionStates connectionState, int connectionId)
        {
            ConnectionState = connectionState;
            ConnectionId = connectionId;
        }
    }


    public struct ServerConnectionStateArgs
    {
        /// <summary>
        /// New connection state.
        /// </summary>
        public LocalConnectionStates ConnectionState;

        public ServerConnectionStateArgs(LocalConnectionStates connectionState)
        {
            ConnectionState = connectionState;
        }
    }

    public struct ClientConnectionStateArgs
    {
        /// <summary>
        /// New connection state.
        /// </summary>
        public LocalConnectionStates ConnectionState;

        public ClientConnectionStateArgs(LocalConnectionStates connectionState)
        {
            ConnectionState = connectionState;
        }
    }
}

