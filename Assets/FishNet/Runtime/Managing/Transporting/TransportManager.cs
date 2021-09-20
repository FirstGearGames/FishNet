using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Transporting
{

    public class TransportManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called before IterateOutgoing has started.
        /// </summary>
        internal event Action OnIterateOutgoingStart;
        /// <summary>
        /// Called after IterateOutgoing has completed.
        /// </summary>
        internal event Action OnIterateOutgoingEnd;
        /// <summary>
        /// Called before IterateIncoming has started. True for on server, false for on client.
        /// </summary>
        internal event Action<bool> OnIterateIncomingStart;
        /// <summary>
        /// Called after IterateIncoming has completed. True for on server, false for on client.
        /// </summary>
        internal event Action<bool> OnIterateIncomingEnd;
        /// <summary>
        /// Transport for server and client.
        /// </summary>
        [Tooltip("Transport for server and client.")]
        public Transport Transport;
        #endregion

        #region Private.
        /// <summary>
        /// NetworkConnections on the server which have to send data to clients.
        /// </summary>
        private List<NetworkConnection> _dirtyToClients = new List<NetworkConnection>();
        /// <summary>
        /// PacketBundles to send to the server.
        /// </summary>
        private List<PacketBundle> _toServerBundles = new List<PacketBundle>();
        /// <summary>
        /// NetworkManager handling this TransportManager.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Outgoing packets which have been split due to not exceeding the transport mtu. These packets will be forced to default reliable.
        /// </summary>
        private PacketBundle _outgoingSplit = null;
        #endregion

        /// <summary>
        /// Sets a connection from server to client dirty.
        /// </summary>
        /// <param name="conn"></param>
        internal void ServerDirty(NetworkConnection conn)
        {
            _dirtyToClients.Add(conn);
        }

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            /* If transport isn't specified then add default
             * transport. */
            if (Transport == null)
                Transport = gameObject.AddComponent<Fluidity.Fluidity>();            

            InitializeOutgoingSplits();
            InitializeToServerBundles();
        }

        /// <summary>
        /// Initializes outgoing splits for each channel.
        /// </summary>
        private void InitializeOutgoingSplits()
        {
            _outgoingSplit = new PacketBundle(Transport.GetMTU((byte)Channel.Reliable));
        }

        /// <summary>
        /// Initializes ToServerBundles for use.
        /// </summary>
        private void InitializeToServerBundles()
        {
            int channels = Transport.GetChannelCount();
            for (byte i = 0; i < channels; i++)
            {
                int mtu = Transport.GetMTU(i);
                _toServerBundles.Add(new PacketBundle(mtu));
            }
        }

        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="connection">Connection to send to. Use null for all clients.</param>
        /// <param name="splitLargeIntoReliable">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, NetworkConnection connection, bool splitLargeIntoReliable = true)
        {
            if (connection == null)
                return;

            //Needs to be split.
            if (splitLargeIntoReliable && SplitOutgoing(channelId, ref segment))
            {
                byte reliableChannelId = (byte)Channel.Reliable;
                for (int i = 0; i < _outgoingSplit.WrittenBuffers; i++)
                {
                    ByteBuffer sb = _outgoingSplit.GetBuffer(i);
                    if (sb != null && sb.Length > 0)
                    {
                        ArraySegment<byte> splitSegment = new ArraySegment<byte>(sb.Data, 0, sb.Length);
                        connection.SendToClient(reliableChannelId, splitSegment);
                    }
                }
                //Reset outgoingSplits just to be sure they aren't accidentally used again.
                _outgoingSplit.Reset();
            }
            //Doesn't need to be split.
            else
            {
                connection.SendToClient(channelId, segment);
            }
        }

        /// <summary>
        /// Sends data to observers.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <param name="observers"></param>
        /// <param name="splitLargeIntoReliable">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        internal void SendToClients(byte channelId, ArraySegment<byte> segment, HashSet<NetworkConnection> observers, bool splitLargeIntoReliable = true)
        {
            foreach (NetworkConnection conn in observers)
                SendToClient(channelId, segment, conn, splitLargeIntoReliable);
        }

        /// <summary>
        /// Sends data to all clients if networkObject has no observers, otherwise sends to observers.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="nob">NetworkObject being used to send data.</param>
        /// <param name="splitLargeIntoReliable">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        internal void SendToClients(byte channelId, ArraySegment<byte> segment, NetworkObject networkObject, bool splitLargeIntoReliable = true)
        {
            //if (networkObject.NetworkObserver == null)
            //SendToClients(channelId, segment, splitLargeIntoReliable);
            //else
            SendToClients(channelId, segment, networkObject.Observers, splitLargeIntoReliable);
        }


        /// <summary>
        /// Sends data to all clients.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        /// <param name="splitLargeIntoReliable">True to split large packets which exceed MTU and send them in order on the reliable channel.</param>
        internal void SendToClients(byte channelId, ArraySegment<byte> segment, bool splitLargeIntoReliable = true)
        {
            /* To ensure proper order everything must be tossed into each
             * NetworkConnection rather than batch send. This is because there
             * is no way to know if batch send must iterate before or
             * after connection sends. By sending to each connection order
             * is maintained. */
            foreach (NetworkConnection conn in _networkManager.ServerManager.Clients.Values)
                SendToClient(channelId, segment, conn, splitLargeIntoReliable);
        }


        /// <summary>
        /// Sends data to the server.
        /// </summary>
        /// <param name="channelId">Channel to send on.</param>
        /// <param name="segment">Data to send.</param>
        internal void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (channelId >= _toServerBundles.Count)
                throw new ArgumentException($"Channel {channelId} is out of bounds.");

            //Doesn't need to be split.
            if (!SplitOutgoing(channelId, ref segment))
            {
                _toServerBundles[channelId].Write(segment);
            }
            //Needs to be split.
            else
            {
                for (int i = 0; i < _outgoingSplit.WrittenBuffers; i++)
                {
                    ByteBuffer sb = _outgoingSplit.GetBuffer(i);
                    if (sb != null && sb.Length > 0)
                    {
                        ArraySegment<byte> splitSegment = new ArraySegment<byte>(sb.Data, 0, sb.Length);
                        _toServerBundles[channelId].Write(splitSegment);
                    }
                }
                //Reset outgoingSplits just to be sure they aren't accidentally used again.
                _outgoingSplit.Reset();
            }
        }

        #region Splitting.
        /// <summary>
        /// Splits outgoing data which is too large to fit into the transport.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <returns>True if data must be split.</returns>
        private bool SplitOutgoing(byte channelId, ref ArraySegment<byte> segment)
        {
            int mtu = Transport.GetMTU(channelId);

            //Doesn't need to be split.
            if (segment.Count <= mtu)
                return false;

            /* Number of bytes to reserve for each buffer.
            * I reserve bytes because I need to insert this
            * data after the buffer is built. I cannot do it before
            * because specifically the buffer count is not
            * known until all the data is built. 
            * 1 for the packet id.
            * 4 for the tick.
            * 2 for the buffer count. */
            int reserve = 7;
            _outgoingSplit.Reset(mtu, reserve);
            //Position to read from for passed in segment.
            int segmentPosition = 0;
            //Maximum size which can be written to each sharedBuffer within splits.
            int maximumSegment = (mtu - reserve);
            while (segmentPosition < segment.Count)
            {
                int writeCount = Math.Min((segment.Count - segmentPosition), maximumSegment);
                //Debug.Log(segment.Count + ", write count " + writeCount);
                _outgoingSplit.Write(new ArraySegment<byte>(segment.Array, segmentPosition, writeCount));
                segmentPosition += writeCount;
            }

            //Fill in the reserved with split headers.
            using (PooledWriter headerWriter = WriterPool.GetWriter())
            {
                ushort bufferCount = (ushort)_outgoingSplit.WrittenBuffers;
                //Generate header which will be written to each buffer.
                headerWriter.Reset();
                /* Server tick which spawn messages originated.
                * Intentionally not packed because spawn message count
                * is unknown and tick will likely exceed packing
                * fairly quickly into the game. */
                headerWriter.WriteByte((byte)PacketId.Split);
                headerWriter.WriteUInt32(_networkManager.TimeManager.Tick, AutoPackType.Unpacked);
                headerWriter.WriteUInt16(bufferCount);
                //Sanity check, to ensure I don't make mistakes.
                if (headerWriter.Length != reserve)
                {
                    Debug.LogError($"Writer is of length {headerWriter.Length} when it's expected to be {reserve}. Data will be corrupted; split has failed.");
                    return false;
                }

                //Iterate all buffers attaching header.
                for (int i = 0; i < _outgoingSplit.WrittenBuffers; i++)
                {
                    //Copy header to shared buffer.
                    Buffer.BlockCopy(headerWriter.GetBuffer(), 0,
                        _outgoingSplit.GetBuffer(i).Data,
                        0, reserve);
                }
            }

            return true;
        }
        #endregion

        /// <summary>
        /// Processes data received by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public void IterateIncoming(bool server)
        {
            OnIterateIncomingStart?.Invoke(server);
            Transport.IterateIncoming(server);
            OnIterateIncomingEnd?.Invoke(server);
        }

        /// <summary>
        /// Processes data to be sent by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public void IterateOutgoing(bool server)
        {
            OnIterateOutgoingStart?.Invoke();
            int channelCount = Transport.GetChannelCount();
            /* If sending from the server. */
            if (server)
            {
                long sentBytes = 0;
                uint sentPackets = 0;
                //Run through all dirty connections to send data to.
                foreach (NetworkConnection conn in _dirtyToClients)
                {
                    //Get packets for every channel.
                    for (byte channel = 0; channel < channelCount; channel++)
                    {
                        if (conn == null)
                            continue;

                        if (conn.GetPacketBundle(channel, out PacketBundle pb))
                        {
                            for (int i = 0; i < pb.WrittenBuffers; i++)
                            {
                                ByteBuffer sb = pb.GetBuffer(i);
                                //Length should always be more than 0 but check to be safe.
                                if (sb != null && sb.Length > 0)
                                {
                                    ArraySegment<byte> segment = new ArraySegment<byte>(sb.Data, 0, sb.Length);
                                    sentBytes += segment.Count;
                                    sentPackets++;
                                    Transport.SendToClient(channel, segment, conn.ClientId);
                                }
                            }

                            pb.Reset();
                        }
                    }

                    if (conn.Disconnecting)
                    {
                        Transport.StopConnection(conn.ClientId, false);
                        conn.UnsetDisconnecting();
                    }
                    conn.ResetServerDirty();
                }

                _dirtyToClients.Clear();
            }
            /* If sending from the client. */
            else
            {
                for (byte channel = 0; channel < channelCount; channel++)
                {
                    if (PacketBundle.GetPacketBundle(channel, _toServerBundles, out PacketBundle pb))
                    {
                        for (int i = 0; i < pb.WrittenBuffers; i++)
                        {
                            ByteBuffer sb = pb.GetBuffer(i);
                            //Length should always be more than 0 but check to be safe.
                            if (sb != null && sb.Length > 0)
                            {
                                ArraySegment<byte> segment = new ArraySegment<byte>(sb.Data, 0, sb.Length);
                                Transport.SendToServer(channel, segment);
                            }
                        }

                        pb.Reset();
                    }
                }
            }

            Transport.IterateOutgoing(server);
            OnIterateOutgoingEnd?.Invoke();
        }


        #region Editor.
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Transport == null)
                Transport = GetComponent<Transport>();
        }
#endif
        #endregion

    }


}