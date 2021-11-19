using FishNet.Transporting;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FishNet.Lighthouse.Client
{
    public class ClientSocket : CommonSocket
    {
        ~ClientSocket()
        {
            _stopThread = true;
        }

        #region Private.
        #region Configuration.
        /// <summary>
        /// Address to bind server to.
        /// </summary>
        private string _address = string.Empty;
        /// <summary>
        /// Port used by server.
        /// </summary>
        private ushort _port = 0;
        /// <summary>
        /// Number of configured channels.
        /// </summary>
        private int _channelsCount = 0;
        /// <summary>
        /// Poll timeout for socket.
        /// </summary>
        private int _pollTime = 0;
        /// <summary>
        /// MTU sizes for each channel.
        /// </summary>
        private int[] _mtus = new int[0];
        #endregion
        #region Queues.
        /// <summary>
        /// Changes to the sockets local connection state.
        /// </summary>
        private ConcurrentQueue<LocalConnectionStates> _localConnectionStates = new ConcurrentQueue<LocalConnectionStates>();
        /// <summary>
        /// Inbound messages which need to be handled.
        /// </summary>
        private ConcurrentQueue<Packet> _incoming = new ConcurrentQueue<Packet>();
        /// <summary>
        /// Outbound messages which need to be handled.
        /// </summary>
        private ConcurrentQueue<Packet> _outgoing = new ConcurrentQueue<Packet>();
        #endregion
        /// <summary>
        /// NetPeer for the server connection.
        /// </summary>
        private NetPeer _serverPeer = null;
        /// <summary>
        /// True to stop the socket thread.
        /// </summary>
        private volatile bool _stopThread = false;
        /// <summary>
        /// True to dequeue Outgoing.
        /// </summary>
        private volatile bool _canDequeueOutgoing = false;
        /// <summary>
        /// Buffer used to temporarily hold incoming data.
        /// </summary>
        private byte[] _incomingBuffer;
        /// <summary>
        /// Thread used for socket.
        /// </summary>
        private Thread _thread;
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        /// <param name="t"></param>
        internal void Initialize(Transport t, int reliableMTU, int unreliableMTU)
        {
            base.Transport = t;

            //Set maximum MTU for each channel, and create byte buffer.
            _mtus = new int[2]
            {
                reliableMTU,
                unreliableMTU
            };
            int largestMTU = Math.Max(reliableMTU, unreliableMTU);
            _incomingBuffer = new byte[largestMTU];
        }

        /// <summary>
        /// Starts the client connection.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="channelsCount"></param>
        /// <param name="pollTime"></param>
        internal bool StartConnection(string address, ushort port, byte channelsCount, int pollTime)
        {
            if (base.GetConnectionState() != LocalConnectionStates.Stopped || (_thread != null && _thread.IsAlive))
                return false;

            base.SetConnectionState(LocalConnectionStates.Starting, false);

            //Assign properties.
            _port = port;
            _address = address;
            _channelsCount = channelsCount;
            _pollTime = pollTime;

            ResetQueues();

            _stopThread = false;
            _thread = new Thread(ThreadSocket);
            _thread.Start();
            return true;
        }


        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            if (base.GetConnectionState() == LocalConnectionStates.Stopped || base.GetConnectionState() == LocalConnectionStates.Stopping)
                return false;

            base.SetConnectionState(LocalConnectionStates.Stopping, false);
            _stopThread = true;
            return true;
        }

        /// <summary>
        /// Resets queues.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetQueues()
        {
            while (_localConnectionStates.TryDequeue(out _)) ;
            base.ClearPacketQueue(ref _incoming);
            base.ClearPacketQueue(ref _outgoing);
        }

        /// <summary>
        /// Handles processing the socket on a new thread.
        /// </summary>
        private void ThreadSocket()
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;

            NetManager clientManager = new NetManager(listener);
            clientManager.Start();
            clientManager.Connect(_address, _port, string.Empty);

            _localConnectionStates.Enqueue(LocalConnectionStates.Starting);

            while (!_stopThread)
            {
                DequeueOutgoing();
                clientManager.PollEvents();
                Thread.Sleep(1);
            }

            clientManager.Stop(true);
            _localConnectionStates.Enqueue(LocalConnectionStates.Stopped);
        }

        /// <summary>
        /// Called when connected from the server.
        /// </summary>
        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _localConnectionStates.Enqueue(LocalConnectionStates.Stopped);
        }

        /// <summary>
        /// Called when connected to the server.
        /// </summary>
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            _localConnectionStates.Enqueue(LocalConnectionStates.Started);
        }

        /// <summary>
        /// Called when data is received from a peer.
        /// </summary>
        private void Listener_NetworkReceiveEvent(NetPeer fromPeer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            base.Listener_NetworkReceiveEvent(ref _incoming, fromPeer, reader, deliveryMethod);
        }

        /// <summary>
        /// Dequeues and processes outgoing.
        /// </summary>
        private void DequeueOutgoing()
        {
            if (!_canDequeueOutgoing)
                return;

            NetPeer peer = _serverPeer;
            //Server connection hasn't been made.
            if (peer == null)
            {
                /* Only dequeue outgoing because other queues might have
                * relevant information, such as the local connection queue. */
                base.ClearPacketQueue(ref _outgoing);
            }
            else
            {
                while (_outgoing.TryDequeue(out Packet outgoing))
                {
                    ArraySegment<byte> segment = outgoing.GetArraySegment();
                    DeliveryMethod dm = (outgoing.Channel == (byte)Channel.Reliable) ?
                         DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;
                    peer.Send(segment.Array, segment.Offset, segment.Count, dm);

                    outgoing.Dispose();
                }
            }

            _canDequeueOutgoing = false;
        }

        /// <summary>
        /// Allows for Outgoing queue to be iterated.
        /// </summary>
        internal void IterateOutgoing()
        {
            _canDequeueOutgoing = true;
        }

        /// <summary>
        /// Iterates the Incoming queue.
        /// </summary>
        internal void IterateIncoming()
        {
            /* Run local connection states first so we can begin
            * to read for data at the start of the frame, as that's
            * where incoming is read. */
            while (_localConnectionStates.TryDequeue(out LocalConnectionStates state))
                base.SetConnectionState(state, false);

            //Not yet started, cannot continue.
            if (base.GetConnectionState() != LocalConnectionStates.Started)
            {
                ResetQueues();
                return;
            }

            /* Incoming. */
            ClientReceivedDataArgs dataArgs = new ClientReceivedDataArgs();
            while (_incoming.TryDequeue(out Packet incoming))
            {                
                dataArgs.Data = incoming.GetArraySegment();
                dataArgs.Channel = (Channel)incoming.Channel;
                base.Transport.HandleClientReceivedDataArgs(dataArgs);
                //Dispose of packet.
                incoming.Dispose();
            }
        }

        /// <summary>
        /// Sends a packet to the server.
        /// </summary>
        internal void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            //Not started, cannot send.
            if (base.GetConnectionState() != LocalConnectionStates.Started)
                return;

            base.Send(ref _outgoing, channelId, segment, -1);
        }


    }
}
