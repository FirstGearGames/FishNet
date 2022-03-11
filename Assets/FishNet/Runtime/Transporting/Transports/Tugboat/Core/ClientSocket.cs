using FishNet.Managing.Logging;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace FishNet.Transporting.Tugboat.Client
{
    public class ClientSocket : CommonSocket
    {
        ~ClientSocket()
        {
            StopConnection();
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
        /// MTU sizes for each channel.
        /// </summary>
        private int _mtu;
        #endregion
        #region Queues.
        /// <summary>
        /// Changes to the sockets local connection state.
        /// </summary>
        private Queue<LocalConnectionStates> _localConnectionStates = new Queue<LocalConnectionStates>();
        /// <summary>
        /// Inbound messages which need to be handled.
        /// </summary>
        private Queue<Packet> _incoming = new Queue<Packet>();
        /// <summary>
        /// Outbound messages which need to be handled.
        /// </summary>
        private Queue<Packet> _outgoing = new Queue<Packet>();
        #endregion
        /// <summary>
        /// Client socket manager.
        /// </summary>
        private NetManager _client;
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
        internal void Initialize(Transport t, int unreliableMTU)
        {
            base.Transport = t;
            _mtu = unreliableMTU;
        }

        /// <summary>
        /// Updates the Timeout value as seconds.
        /// </summary>
        internal void UpdateTimeout(int timeout)
        {
            _timeout = timeout;
            base.UpdateTimeout(_client, timeout);
        }

        /// <summary>
        /// Threaded operation to process client actions.
        /// </summary>
        private void ThreadedSocket()
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;

            _client = new NetManager(listener);
            _client.MtuOverride = (_mtu + NetConstants.FragmentedHeaderTotalSize);

            UpdateTimeout(_timeout);

            _localConnectionStates.Enqueue(LocalConnectionStates.Starting);
            _client.Start();
            _client.Connect(_address, _port, string.Empty);
        }


        /// <summary>
        /// Stops the socket on a new thread.
        /// </summary>
        private void StopSocketOnThread()
        {
            if (_client == null)
                return;

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
        internal bool StartConnection(string address, ushort port)
        {
            if (base.GetConnectionState() != LocalConnectionStates.Stopped)
                return false;

            base.SetConnectionState(LocalConnectionStates.Starting, false);

            //Assign properties.
            _port = port;
            _address = address;

            ResetQueues();
            Task t = Task.Run(() => ThreadedSocket());

            return true;
        }


        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection(DisconnectInfo? info = null)
        {
            if (base.GetConnectionState() == LocalConnectionStates.Stopped || base.GetConnectionState() == LocalConnectionStates.Stopping)
                return false;

            if (info != null && base.Transport.NetworkManager.CanLog(LoggingType.Common))
                Debug.Log($"Local client disconnect reason: {info.Value.Reason}.");

            base.SetConnectionState(LocalConnectionStates.Stopping, false);
            StopSocketOnThread();
            return true;
        }

        /// <summary>
        /// Resets queues.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetQueues()
        {
            _localConnectionStates.Clear();
            base.ClearPacketQueue(ref _incoming);
            base.ClearPacketQueue(ref _outgoing);
        }


        /// <summary>
        /// Called when disconnected from the server.
        /// </summary>
        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            StopConnection(disconnectInfo);
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
        private void Listener_NetworkReceiveEvent(NetPeer fromPeer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            base.Listener_NetworkReceiveEvent(_incoming, fromPeer, reader, deliveryMethod, _mtu);
        }

        /// <summary>
        /// Dequeues and processes outgoing.
        /// </summary>
        private void DequeueOutgoing()
        {
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
                int count = _outgoing.Count;
                for (int i = 0; i < count; i++)
                {
                    Packet outgoing = _outgoing.Dequeue();

                    ArraySegment<byte> segment = outgoing.GetArraySegment();
                    DeliveryMethod dm = (outgoing.Channel == (byte)Channel.Reliable) ?
                         DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;

                    //If over the MTU.
                    if (outgoing.Channel == (byte)Channel.Unreliable && segment.Count > _mtu)
                    {
                        if (base.Transport.NetworkManager.CanLog(LoggingType.Warning))
                            Debug.LogWarning($"Client is sending of {segment.Count} length on the unreliable channel, while the MTU is only {_mtu}. The channel has been changed to reliable for this send.");
                        dm = DeliveryMethod.ReliableOrdered;
                    }

                    peer.Send(segment.Array, segment.Offset, segment.Count, dm);

                    outgoing.Dispose();
                }
            }
        }

        /// <summary>
        /// Allows for Outgoing queue to be iterated.
        /// </summary>
        internal void IterateOutgoing()
        {
            DequeueOutgoing();
        }

        /// <summary>
        /// Iterates the Incoming queue.
        /// </summary>
        internal void IterateIncoming()
        {
            _client?.PollEvents();

            /* Run local connection states first so we can begin
            * to read for data at the start of the frame, as that's
            * where incoming is read. */
            while (_localConnectionStates.Count > 0)
                base.SetConnectionState(_localConnectionStates.Dequeue(), false);

            //Not yet started, cannot continue.
            LocalConnectionStates localState = base.GetConnectionState();
            if (localState != LocalConnectionStates.Started)
            {
                ResetQueues();
                //If stopped try to kill task.
                if (localState == LocalConnectionStates.Stopped)
                {
                    StopSocketOnThread();
                    return;
                }
            }

            /* Incoming. */
            while (_incoming.Count > 0)
            {
                Packet incoming = _incoming.Dequeue();
                ClientReceivedDataArgs dataArgs = new ClientReceivedDataArgs(
                    incoming.GetArraySegment(),
                    (Channel)incoming.Channel, base.Transport.Index);
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

            base.Send(ref _outgoing, channelId, segment, -1, _mtu);
        }


    }
}
