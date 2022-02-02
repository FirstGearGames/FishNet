using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FishNet.Tugboat.Server
{
    public class ServerSocket : CommonSocket
    {

        #region Public.
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        internal RemoteConnectionStates GetConnectionState(int connectionId)
        {
            NetPeer peer = GetNetPeer(connectionId, false);
            if (peer == null || peer.ConnectionState != ConnectionState.Connected)
                return RemoteConnectionStates.Stopped;
            else
                return RemoteConnectionStates.Started;
        }
        #endregion

        #region Private.
        #region Configuration.
        /// <summary>
        /// Port used by server.
        /// </summary>
        private ushort _port;
        /// <summary>
        /// Maximum number of allowed clients.
        /// </summary>
        private int _maximumClients;
        /// <summary>
        /// MTU size per packet.
        /// </summary>
        private int _mtu;
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
        private Queue<Packet> _outgoing = new Queue<Packet>();
        /// <summary>
        /// Ids to disconnect next iteration. This ensures data goes through to disconnecting remote connections. This may be removed in a later release.
        /// </summary>
        private ListCache<int> _disconnectingNext = new ListCache<int>();
        /// <summary>
        /// Ids to disconnect immediately.
        /// </summary>
        private ListCache<int> _disconnectingNow = new ListCache<int>();
        /// <summary>
        /// PossibleAttackEvents which need to be handled.
        /// </summary>
        private ConcurrentQueue<int> _possibleAttackEvents = new ConcurrentQueue<int>();
        /// <summary>
        /// ConnectionEvents which need to be handled.
        /// </summary>
        private ConcurrentQueue<RemoteConnectionEvent> _remoteConnectionEvents = new ConcurrentQueue<RemoteConnectionEvent>();
        #endregion
        /// <summary>
        /// Key required to connect.
        /// </summary>
        private string _key = string.Empty;
        /// <summary>
        /// Server socket manager.
        /// </summary>
        private NetManager _server;
        /// <summary>
        /// Locks the NetManager to stop it.
        /// </summary>
        private readonly object _stopLock = new object();
        /// <summary>
        /// Attack response type to use.
        /// </summary>
        private AttackResponseType _attackResponseType;
        #endregion

        ~ServerSocket()
        {
            StopConnection();
        }

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
        /// Threaded operation to process server actions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThreadedSocket()
        {
            EventBasedNetListener listener = new EventBasedNetListener();
            listener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;
            if (_attackResponseType != AttackResponseType.Disabled)
                listener.PossibleAttackEvent += Listener_PossibleAttackEvent;
            listener.PeerConnectedEvent += Listener_PeerConnectedEvent;
            listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;

            _server = new NetManager(listener, null, _attackResponseType);
            _server.MtuOverride = (_mtu + NetConstants.FragmentedHeaderTotalSize);

            bool startResult = _server.Start(_port);

            //If started succcessfully.
            if (startResult)
            {
                _localConnectionStates.Enqueue(LocalConnectionStates.Started);
            }
            //Failed to start.
            else
            {
                if (base.Transport.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Server failed to start. This usually occurs when the specified port is unavailable, be it closed or already in use.");

                StopConnection();
            }
        }

        /// <summary>
        /// Called when the server suspects a client may be performing an attack.
        /// </summary>
        /// <param name="peer"></param>
        private void Listener_PossibleAttackEvent(NetPeer peer)
        {
            _possibleAttackEvents.Enqueue(peer.Id);
        }

        /// <summary>
        /// Stops the socket on a new thread.
        /// </summary>
        private void StopSocketOnThread()
        {
            if (_server == null)
                return;

            Task t = Task.Run(() =>
            {
                lock (_stopLock)
                {
                    _server?.Stop();
                    _server = null;
                }

                //If not stopped yet also enqueue stop.
                if (base.GetConnectionState() != LocalConnectionStates.Stopped)
                    _localConnectionStates.Enqueue(LocalConnectionStates.Stopped);
            });
        }

        /// <summary>
        /// Gets the address of a remote connection Id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns>Returns string.empty if Id is not found.</returns>
        internal string GetConnectionAddress(int connectionId)
        {
            NetPeer peer = GetNetPeer(connectionId, false);
            return peer.EndPoint.Address.ToString();
        }

        /// <summary>
        /// Returns a NetPeer for connectionId.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        private NetPeer GetNetPeer(int connectionId, bool connectedOnly)
        {
            NetPeer peer = null;
            if (_server != null)
            {
                if (connectionId >= 0 || connectionId < _server.ConnectedPeersCount)
                    peer = _server.GetPeerById(connectionId);
                if (connectedOnly && peer != null && peer.ConnectionState != ConnectionState.Connected)
                    peer = null;
            }

            return peer;
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        internal bool StartConnection(ushort port, int maximumClients, AttackResponseType attackResponseType)
        {
            if (base.GetConnectionState() != LocalConnectionStates.Stopped)
                return false;

            base.SetConnectionState(LocalConnectionStates.Starting, true);

            //Assign properties.
            _port = port;
            _maximumClients = maximumClients;
            _attackResponseType = attackResponseType;
            ResetQueues();

            Task t = Task.Run(() => ThreadedSocket());

            return true;
        }

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            if (_server == null || base.GetConnectionState() == LocalConnectionStates.Stopped || base.GetConnectionState() == LocalConnectionStates.Stopping)
                return false;

            _localConnectionStates.Enqueue(LocalConnectionStates.Stopping);
            StopSocketOnThread();
            return true;
        }

        /// <summary>
        /// Stops a remote client disconnecting the client from the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        internal bool StopConnection(int connectionId, bool immediately)
        {
            //Server isn't running.
            if (_server == null || base.GetConnectionState() != LocalConnectionStates.Started)
                return false;

            NetPeer peer = GetNetPeer(connectionId, false);
            if (peer == null)
                return false;

            //Don't disconnect immediately, wait until next command iteration.
            if (!immediately)
            {
                _disconnectingNext.AddValue(connectionId);

            }
            //Disconnect immediately.
            else
            {
                try
                {
                    peer.Disconnect();
                    base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionStates.Stopped, connectionId));
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Resets queues.
        /// </summary>
        private void ResetQueues()
        {
            while (_localConnectionStates.TryDequeue(out _)) ;
            base.ClearPacketQueue(ref _incoming);
            base.ClearPacketQueue(ref _outgoing);
            _disconnectingNext.Reset();
            _disconnectingNow.Reset();
            while (_possibleAttackEvents.TryDequeue(out _)) ;
            while (_remoteConnectionEvents.TryDequeue(out _)) ;
        }


        /// <summary>
        /// Called when a peer disconnects or times out.
        /// </summary>
        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _remoteConnectionEvents.Enqueue(new RemoteConnectionEvent(false, peer.Id));
        }

        /// <summary>
        /// Called when a peer completes connection.
        /// </summary>
        private void Listener_PeerConnectedEvent(NetPeer peer)
        {
            _remoteConnectionEvents.Enqueue(new RemoteConnectionEvent(true, peer.Id));
        }

        /// <summary>
        /// Called when data is received from a peer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Listener_NetworkReceiveEvent(NetPeer fromPeer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            //If over the MTU.
            if (reader.AvailableBytes > _mtu)
            {
                _remoteConnectionEvents.Enqueue(new RemoteConnectionEvent(false, fromPeer.Id));
                fromPeer.Disconnect();
            }
            else
            {
                base.Listener_NetworkReceiveEvent(ref _incoming, fromPeer, reader, deliveryMethod, _mtu);
            }
        }


        /// <summary>
        /// Called when a remote connection request is made.
        /// </summary>
        private void Listener_ConnectionRequestEvent(ConnectionRequest request)
        {
            if (_server == null)
                return;

            //At maximum peers.
            if (_server.ConnectedPeersCount >= _maximumClients)
            {
                request.Reject();
                return;
            }

            request.AcceptIfKey(_key);
        }

        /// <summary>
        /// Dequeues and processes commands.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DequeueDisconnects()
        {
            int count;

            count = _disconnectingNow.Written;
            //If there are disconnect nows.
            if (count > 0)
            {
                List<int> collection = _disconnectingNow.Collection;
                for (int i = 0; i < count; i++)
                    StopConnection(collection[i], true);

                _disconnectingNow.Reset();
            }

            count = _disconnectingNext.Written;
            //If there are disconnect next.
            if (count > 0)
            {
                List<int> collection = _disconnectingNext.Collection;
                for (int i = 0; i < count; i++)
                    _disconnectingNow.AddValue(collection[i]);

                _disconnectingNext.Reset();
            }
        }


        /// <summary>
        /// Dequeues and processes outgoing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DequeueOutgoing()
        {
            if (base.GetConnectionState() != LocalConnectionStates.Started || _server == null)
            {
                //Not started, clear outgoing.
                base.ClearPacketQueue(ref _outgoing);
            }
            else
            {
                int count = _outgoing.Count;
                for (int i = 0; i < count; i++)
                {
                    Packet outgoing = _outgoing.Dequeue();
                    int connectionId = outgoing.ConnectionId;

                    ArraySegment<byte> segment = outgoing.GetArraySegment();
                    DeliveryMethod dm = (outgoing.Channel == (byte)Channel.Reliable) ?
                         DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;

                    //If over the MTU.
                    if (outgoing.Channel == (byte)Channel.Unreliable && segment.Count > _mtu)
                    {
                        if (base.Transport.NetworkManager.CanLog(LoggingType.Warning))
                            Debug.LogWarning($"Server is sending of {segment.Count} length on the unreliable channel, while the MTU is only {_mtu}. The channel has been changed to reliable for this send.");
                        dm = DeliveryMethod.ReliableOrdered;
                    }

                    //Send to all clients.
                    if (connectionId == -1)
                    {
                        _server.SendToAll(segment.Array, segment.Offset, segment.Count, dm);
                    }
                    //Send to one client.
                    else
                    {
                        NetPeer peer = GetNetPeer(connectionId, true);
                        //If peer is found.
                        if (peer != null)
                            peer.Send(segment.Array, segment.Offset, segment.Count, dm);
                    }

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
            DequeueDisconnects();
        }

        /// <summary>
        /// Iterates the Incoming queue.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void IterateIncoming()
        {
            /* Run local connection states first so we can begin
             * to read for data at the start of the frame, as that's
             * where incoming is read. */
            while (_localConnectionStates.TryDequeue(out LocalConnectionStates state))
                base.SetConnectionState(state, true);

            //Not yet started.
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

            _server?.PollEvents(base.Transport.NetworkManager.TimeManager.Tick);

            bool canLogWarning = base.Transport.NetworkManager.CanLog(LoggingType.Warning);
            //Go through attack events first.
            while (_possibleAttackEvents.TryDequeue(out int connectionId))
            {
                if (canLogWarning)
                {
                    string msg = string.Empty;
                    if (_attackResponseType == AttackResponseType.WarnAndKick)
                        msg = " Client will be kicked.";

                    Debug.LogWarning($"ConnectionId {connectionId} may be performing an attack.{msg}");
                }
            }

            //Handle connection and disconnection events.
            while (_remoteConnectionEvents.TryDequeue(out RemoteConnectionEvent connectionEvent))
            {
                RemoteConnectionStates state = (connectionEvent.Connected) ? RemoteConnectionStates.Started : RemoteConnectionStates.Stopped;
                base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(state, connectionEvent.ConnectionId));
            }

            ServerReceivedDataArgs dataArgs = new ServerReceivedDataArgs();
            //Handle packets.
            while (_incoming.TryDequeue(out Packet incoming))
            {
                //Make sure peer is still connected.
                NetPeer peer = GetNetPeer(incoming.ConnectionId, true);
                if (peer != null)
                {
                    dataArgs.Data = incoming.GetArraySegment();
                    dataArgs.Channel = (Channel)incoming.Channel;
                    dataArgs.ConnectionId = incoming.ConnectionId;
                    base.Transport.HandleServerReceivedDataArgs(dataArgs);
                }

                incoming.Dispose();
            }

        }

        /// <summary>
        /// Sends a packet to a single, or all clients.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            Send(ref _outgoing, channelId, segment, connectionId, _mtu);
        }

        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server. If the transport does not support this method the value -1 is returned.
        /// </summary>
        /// <returns></returns>
        internal int GetMaximumClients()
        {
            return _maximumClients;
        }
    }
}
