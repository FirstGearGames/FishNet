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
        /// Index of the transport that is for.
        /// This is primarily used when supporting multiple transports.
        /// </summary>
        public int TransportIndex;

        [Obsolete("Use ServerReceivedDataArgs(data, channel, connectionid, transportIndex) instead.")] //Remove on 2022/06/01 in favor of AllowStacking.
        public ServerReceivedDataArgs(ArraySegment<byte> data, Channel channel, int connectionId)
        {
            Data = data;
            Channel = channel;
            ConnectionId = connectionId;
            TransportIndex = 0;
        }
        public ServerReceivedDataArgs(ArraySegment<byte> data, Channel channel, int connectionId, int transportIndex)
        {
            Data = data;
            Channel = channel;
            ConnectionId = connectionId;
            TransportIndex = transportIndex;
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
        /// Index of the transport that is for.
        /// This is primarily used when supporting multiple transports.
        /// </summary>
        public int TransportIndex;

        [Obsolete("Use ClientReceivedDataArgs(data, channel, transportIndex) instead.")] //Remove on 2022/06/01 in favor of AllowStacking.
        public ClientReceivedDataArgs(ArraySegment<byte> data, Channel channel)
        {
            Data = data;
            Channel = channel;
            TransportIndex = 0;
        }
        public ClientReceivedDataArgs(ArraySegment<byte> data, Channel channel, int transportIndex)
        {
            Data = data;
            Channel = channel;
            TransportIndex = transportIndex;
        }
    }



    /// <summary>
    /// Container about a connection state change for a client.
    /// </summary>
    public struct RemoteConnectionStateArgs
    {
        /// <summary>
        /// Index of the transport that is for.
        /// This is primarily used when supporting multiple transports.
        /// </summary>
        public int TransportIndex;
        /// <summary>
        /// New connection state.
        /// </summary>
        public RemoteConnectionStates ConnectionState;
        /// <summary>
        /// ConnectionId for which client the state changed. Will be -1 if ConnectionState was for the local server.
        /// </summary>
        public int ConnectionId;

        [Obsolete("Use RemoteConnectionStateArgs(connectionState, connectionId, transportIndex) instead.")] //Remove on 2022/06/01 in favor of AllowStacking.
        public RemoteConnectionStateArgs(RemoteConnectionStates connectionState, int connectionId)
        {
            ConnectionState = connectionState;
            ConnectionId = connectionId;
            TransportIndex = 0;
        }
        public RemoteConnectionStateArgs(RemoteConnectionStates connectionState, int connectionId, int transportIndex)
        {
            ConnectionState = connectionState;
            ConnectionId = connectionId;
            TransportIndex = transportIndex;
        }
    }

    /// <summary>
    /// Container about a connection state change for the server.
    /// </summary>
    public struct ServerConnectionStateArgs
    {
        /// <summary>
        /// Index of the transport that is for.
        /// This is primarily used when supporting multiple transports.
        /// </summary>
        public int TransportIndex;
        /// <summary>
        /// New connection state.
        /// </summary>
        public LocalConnectionStates ConnectionState;

        [Obsolete("Use ServerConnectionStateArgs(connectionState, transportIndex) instead.")] //Remove on 2022/06/01 in favor of AllowStacking.
        public ServerConnectionStateArgs(LocalConnectionStates connectionState)
        {
            ConnectionState = connectionState;
            TransportIndex = 0;
        }
        public ServerConnectionStateArgs(LocalConnectionStates connectionState, int transportIndex)
        {            
            ConnectionState = connectionState;
            TransportIndex = transportIndex;
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
        /// Index of the transport that is for.
        /// This is primarily used when supporting multiple transports.
        /// </summary>
        public int TransportIndex;

        [Obsolete("Use ClientConnectionStateArgs(transportIndex, connectionState) instead.")] //Remove on 2022/06/01 in favor of AllowStacking.
        public ClientConnectionStateArgs(LocalConnectionStates connectionState) //Remove on 2022/06/01 in favor of AllowStacking.
        {
            ConnectionState = connectionState;
            TransportIndex = 0;
        }
        public ClientConnectionStateArgs(LocalConnectionStates connectionState, int transportIndex)
        {            
            ConnectionState = connectionState;
            TransportIndex = transportIndex;
        }
    }
}

