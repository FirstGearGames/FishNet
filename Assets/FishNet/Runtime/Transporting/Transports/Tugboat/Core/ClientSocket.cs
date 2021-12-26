using FishNet.Transporting;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FishNet.Tugboat.Client
{
    public class ClientSocket : CommonSocket
    {
        ~ClientSocket()
        {
            StopClient();
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
        private ushort _port;
        /// <summary>
        /// Poll timeout for socket.
        /// </summary>
        private int _pollTime;
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
        /// Client socket manager.
        /// </summary>
        private NetManager _client;
        /// <summary>
        /// True to dequeue Outgoing.
        /// </summary>
        private volatile bool _canDequeueOutgoing;
        /// <summary>
        /// Token to cancel task.
        /// </summary>
        private CancellationTokenSource _taskCancelToken;
        /// <summary>
        /// How long in seconds until client times from server.
        /// </summary>
        private int _timeout;
        /// <summary>
        /// Locks the NetManager to stop it.
        /// </summary>
        private readonly object _stopLock = new object();
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        /// <param name="t"></param>
        internal void Initialize(Transport t, int reliableMTU, int unreliableMTU, int timeout)
        {
            base.Transport = t;

            _timeout = timeout;
            //Set maximum MTU for each channel, and create byte buffer.
            _mtus = new int[2]
            {
                reliableMTU,
                unreliableMTU
            };
        }

        /// <summary>
        /// Threaded operation to process client actions.
        /// </summary>
        private void ThreadedSocket(CancellationToken cancelToken)
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;

            _client = new NetManager(listener);

            //If timeout is not specified then use max value.
            if (_timeout == 0)
                _client.DisconnectTimeout = int.MaxValue;
            //Otherwise convert users seconds to ms.
            else
                _client.DisconnectTimeout = Math.Min(int.MaxValue, (_timeout * 1000));

            _client.Start();
            _client.Connect(_address, _port, string.Empty);

            _localConnectionStates.Enqueue(LocalConnectionStates.Starting);

            while (!cancelToken.IsCancellationRequested)
            {
                DequeueOutgoing();
                _client?.PollEvents();
                Thread.Sleep(_pollTime);
            }

            StopClient();
        }


        /// <summary>
        /// Ends running task without any checks.
        /// </summary>
        private void StopClient()
        {
            if (_taskCancelToken != null && !_taskCancelToken.IsCancellationRequested)
                _taskCancelToken.Cancel();

            StopSocketOnThread();
        }

        /// <summary>
        /// Stops the socket on a new thread.
        /// </summary>
        private void StopSocketOnThread()
        {
            Task t = Task.Run(() =>
            {
                lock (_stopLock)
                { 
                    _client?.Stop();
                    _client = null;
                }

                //If not stopped yet also enqueue stop.
                if (base.GetConnectionState() != LocalConnectionStates.Stopped)
                    _localConnectionStates.Enqueue(LocalConnectionStates.Stopped);
            });
        }

        /// <summary>
        /// Starts the client connection.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="channelsCount"></param>
        /// <param name="pollTime"></param>
        internal bool StartConnection(string address, ushort port, int pollTime)
        {
            if (base.GetConnectionState() != LocalConnectionStates.Stopped)
                return false;

            base.SetConnectionState(LocalConnectionStates.Starting, false);

            //Assign properties.
            _port = port;
            _address = address;
            _pollTime = pollTime;

            ResetQueues();

            _taskCancelToken = new CancellationTokenSource();
            Task t = Task.Run(() => ThreadedSocket(_taskCancelToken.Token), _taskCancelToken.Token);
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
            StopClient();
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
        /// Called when connected from the server.
        /// </summary>
        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            StopClient();
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

            NetPeer peer = null;
            if (_client != null)
                peer = _client.FirstPeer;
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
            LocalConnectionStates localState = base.GetConnectionState();
            if (localState != LocalConnectionStates.Started)
            {
                ResetQueues();
                //If stopped try to kill task.
                if (localState == LocalConnectionStates.Stopped)
                    StopClient();
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
