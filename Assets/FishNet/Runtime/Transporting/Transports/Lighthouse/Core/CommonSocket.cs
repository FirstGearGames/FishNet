using FishNet.Transporting;
using FishNet.Utility.Performance;
using LiteNetLib;
using System;
using System.Collections.Concurrent;

namespace FishNet.Lighthouse
{

    public abstract class CommonSocket
    {

        #region Public.
        /// <summary>
        /// Current ConnectionState.
        /// </summary>
        private LocalConnectionStates _connectionState = LocalConnectionStates.Stopped;
        /// <summary>
        /// Returns the current ConnectionState.
        /// </summary>
        /// <returns></returns>
        internal LocalConnectionStates GetConnectionState()
        {
            return _connectionState;
        }
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        protected void SetConnectionState(LocalConnectionStates connectionState, bool asServer)
        {
            //If state hasn't changed.
            if (connectionState == _connectionState)
                return;

            _connectionState = connectionState;
            if (asServer)
                Transport.HandleServerConnectionState(new ServerConnectionStateArgs(connectionState));
            else
                Transport.HandleClientConnectionState(new ClientConnectionStateArgs(connectionState));
        }
        #endregion

        #region Protected.
        /// <summary>
        /// Transport controlling this socket.
        /// </summary>
        protected Transport Transport = null;
        #endregion


        /// <summary>
        /// Sends data to connectionId.
        /// </summary>
        internal void Send(ref ConcurrentQueue<Packet> queue, byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (GetConnectionState() != LocalConnectionStates.Started)
                return;

            //ConnectionId isn't used from client to server.
            Packet outgoing = new Packet(connectionId, segment, channelId);
            queue.Enqueue(outgoing);
        }

        /// <summary>
        /// Clears a queue using Packet type.
        /// </summary>
        /// <param name="queue"></param>
        internal void ClearPacketQueue(ref ConcurrentQueue<Packet> queue)
        {
            while (queue.TryDequeue(out Packet p))
                p.Dispose();
        }

        /// <summary>
        /// Called when data is received.
        /// </summary>
        internal virtual void Listener_NetworkReceiveEvent(ref ConcurrentQueue<Packet> queue,  NetPeer fromPeer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            //Set buffer.
            int dataLen = reader.AvailableBytes;
            byte[] data = ByteArrayPool.Retrieve(dataLen, true);
            reader.GetBytes(data, dataLen);
            //Id.
            int id = fromPeer.Id;
            //Channel.
            byte channel = (deliveryMethod == DeliveryMethod.Unreliable) ?
                (byte)Channel.Unreliable : (byte)Channel.Reliable;
            //Add to packets.
            Packet packet = new Packet(id, data, dataLen, channel);
            queue.Enqueue(packet);
            //Recycle reader.
            reader.Recycle();
        }

    }

}