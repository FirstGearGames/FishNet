using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ENet;
using FishNet.Transporting;

namespace Fluidity.Server
{
    public class ServerSocket : CommonSocket
    {
        ~ServerSocket()
        {
            _stopThread = true;
        }

        #region Public.
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        internal RemoteConnectionStates GetConnectionState(int connectionId)
        {
            //Remote clients can only have Started or Stopped states since we cannot know in between.
            if (_connectedClients.Contains(connectionId))
                return RemoteConnectionStates.Started;
            else
                return RemoteConnectionStates.Stopped;
        }
        #endregion

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
        /// Maximum number of allowed peers.
        /// </summary>
        private int _maximumPeers = 0;
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
        private ConcurrentQueue<IncomingPacket> _incoming = new ConcurrentQueue<IncomingPacket>();
        /// <summary>
        /// Outbound messages which need to be handled.
        /// </summary>
        private ConcurrentQueue<OutgoingPacket> _outgoing = new ConcurrentQueue<OutgoingPacket>();
        /// <summary>
        /// Commands which need to be handled.
        /// </summary>
        private ConcurrentQueue<CommandPacket> _commands = new ConcurrentQueue<CommandPacket>();
        /// <summary>
        /// ConnectionEvents which need to be handled.
        /// </summary>
        private ConcurrentQueue<ConnectionEvent> _connectionEvents = new ConcurrentQueue<ConnectionEvent>();
        #endregion
        /// <summary>
        /// True to stop the socket thread.
        /// </summary>
        private volatile bool _stopThread = false;
        /// <summary>
        /// True to dequeue Outgoing.
        /// </summary>
        private volatile bool _dequeueOutgoing = false;
        /// <summary>
        /// Peers cache for ENet. Entries may not be set.
        /// </summary>
        private Peer[] _peers;
        /// <summary>
        /// Remote clients which are connected.
        /// </summary>
        private HashSet<int> _connectedClients = new HashSet<int>();
        /// <summary>
        /// Buffer used to temporarily hold incoming data.
        /// </summary>
        private byte[] _incomingBuffer;
        /// <summary>
        /// Thread used for socket.
        /// </summary>
        private Thread _thread;
        /// <summary>
        /// Ids to disconnect next iteration. This is to solve an enet but where data doesn't send because enet disconnects the connection too quickly even when using DisconnectLater.
        /// </summary>
        private List<int> _disconnectsNextIteration = new List<int>();
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        /// <param name="t"></param>
        internal void Initialize(Transport t, ChannelData[] channelData)
        {
            base.Transport = t;

            //Set maximum MTU for each channel, and create byte buffer.
            int largestMtu = 0;
            _mtus = new int[channelData.Length];
            for (int i = 0; i < channelData.Length; i++)
            {
                _mtus[i] = channelData[i].MaximumTransmissionUnit;
                largestMtu = Math.Max(largestMtu, _mtus[i]);
            }
            _incomingBuffer = new byte[largestMtu];
        }

        /// <summary>
        /// Gets the address of a remote connection Id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns>Returns string.empty if Id is not found.</returns>
        internal string GetConnectionAddress(int connectionId)
        {
            Peer p = _peers[connectionId];
            return (p.IsSet) ? p.IP : string.Empty;
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="maximumPeers"></param>
        /// <param name="channelsCount"></param>
        /// <param name="pollTime"></param>
        internal bool StartConnection(string address, ushort port, int maximumPeers, byte channelsCount, int pollTime)
        {
            if (base.GetConnectionState() != LocalConnectionStates.Stopped || (_thread != null && _thread.IsAlive))
                return false;

            base.SetLocalConnectionState(LocalConnectionStates.Starting);

            //Assign properties.
            _address = address;
            _pollTime = port;
            _port = port;
            _maximumPeers = maximumPeers;
            _channelsCount = channelsCount;
            _pollTime = pollTime;

            ResetValues();
            _peers = new Peer[_maximumPeers];

            _stopThread = false;
            _thread = new Thread(ThreadedSocket);
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

            base.SetLocalConnectionState(LocalConnectionStates.Stopping);
            _stopThread = true;
            return true;
        }

        /// <summary>
        /// Stops a remote client disconnecting the client from the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        internal bool StopConnection(int connectionId, bool immediately)
        {
            if (!_peers[connectionId].IsSet)
                return false;

            //Don't disconnect immediately, wait until next command iteration.
            if (!immediately)
            {

                if (base.GetConnectionState() == LocalConnectionStates.Stopped)
                    return false;

                CommandPacket command = new CommandPacket
                {
                    Type = CommandTypes.DisconnectPeerNextIteration,
                    ConnectionId = connectionId
                };

                _commands.Enqueue(command);
            }
            //Disconnect immediately.
            else
            {
                try
                {
                    if (connectionId < 0)
                        return false;

                    _peers[connectionId].DisconnectLater(10);
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
        /// Resets values as though this is a new instance.
        /// </summary>
        private void ResetValues()
        {
            while (_localConnectionStates.TryDequeue(out _)) ;
            while (_incoming.TryDequeue(out _)) ;
            while (_outgoing.TryDequeue(out _)) ;
            while (_commands.TryDequeue(out _)) ;
            while (_connectionEvents.TryDequeue(out _)) ;
            _peers = new Peer[0];
            _connectedClients.Clear();
        }


        /// <summary>
        /// Threaded operation to process server actions.
        /// </summary>
        private void ThreadedSocket()
        {
            Address address = new Address();
            Host socket;
            ENet.Event enetEvent;

            // Configure the server address.
            address.SetHost(_address);
            address.Port = _port;

            using (socket = new Host())
            {
                // Create the server object.
                socket.Create(address, _maximumPeers, _channelsCount);
                _localConnectionStates.Enqueue(LocalConnectionStates.Started);

                //Loop long as the server is running.
                while (!_stopThread)
                {
                    //Packets to be sent out.
                    DequeueOutgoing(socket);
                    //Actions issued to socket locally.
                    DequeueCommands();
                    //Check for events until there are none.
                    while (true)
                    {
                        // Any events happening?
                        if (socket.CheckEvents(out enetEvent) <= 0)
                        {
                            // If service time is met, break out of it.
                            if (socket.Service(_pollTime, out enetEvent) <= 0)
                                break;
                        }

                        HandleENetEvent(ref enetEvent);
                    }
                }

                /* Thread is ending. */
                socket.Flush();
                for (int i = 0; i < _peers.Length; i++)
                    StopConnection(i, true);
            }

            _localConnectionStates.Enqueue(LocalConnectionStates.Stopped);
        }

        /// <summary>
        /// Dequeues and processes commands.
        /// </summary>
        private void DequeueCommands()
        {
            int disconnectNextIterationCount = 0;
            while (_commands.TryDequeue(out CommandPacket command))
            {
                if (command.Type == CommandTypes.DisconnectPeerNextIteration)
                {
                    if (_disconnectsNextIteration.Count <= disconnectNextIterationCount)
                        _disconnectsNextIteration.Add(command.ConnectionId);
                    else
                        _disconnectsNextIteration[disconnectNextIterationCount] = command.ConnectionId;

                    disconnectNextIterationCount++;
                }
                else if (command.Type == CommandTypes.DisconnectPeerNow)
                {
                    StopConnection(command.ConnectionId, true);
                }
            }

            //Enqueue disconnects for next iteration. This is to fix enet ug as described on _disconnectNextIteration.
            for (int i = 0; i < disconnectNextIterationCount; i++)
                _commands.Enqueue(
                    new CommandPacket()
                    {
                        ConnectionId = _disconnectsNextIteration[i],
                        Type = CommandTypes.DisconnectPeerNow
                    });
        }

        /// <summary>
        /// Dequeues and processes outgoing.
        /// </summary>
        private void DequeueOutgoing(Host socket)
        {
            //Not allowed to send outgoing yet.
            if (!_dequeueOutgoing)
                return;

            while (_outgoing.TryDequeue(out OutgoingPacket op))
            {
                int peerId = op.ConnectionId;
                //Send to all clients.
                if (peerId == -1)
                {
                    //Broadcast is a built in function of enet to send to all clients.                    
                    socket.Broadcast(op.Channel, ref op.Packet);
                }
                //Send to one client.
                else
                {
                    //If within bounds.
                    if (peerId >= 0 && peerId < _peers.Length && _peers[peerId].IsSet)
                    {
                        //Send returns a potential error code. If returned value is less than 0, than an error has occurred.
                        _peers[op.ConnectionId].Send(op.Channel, ref op.Packet);
                    }
                }

                //Clean up enet packet.
                op.Packet.Dispose();
            }

            _dequeueOutgoing = false;
        }

        /// <summary>
        /// Handles an event received from ENet.
        /// </summary>
        private void HandleENetEvent(ref ENet.Event enetEvent)
        {
            switch (enetEvent.Type)
            {
                // Idle.
                case ENet.EventType.None:
                default:
                    break;

                // Connection Event.
                case ENet.EventType.Connect:
                    _connectionEvents.Enqueue(new ConnectionEvent
                    {
                        Connected = true,
                        ConnectionId = (int)enetEvent.Peer.ID
                    });

                    // Assign a reference to the Peer.
                    _peers[enetEvent.Peer.ID] = enetEvent.Peer;
                    break;
                // Disconnect/Timeout.
                case ENet.EventType.Disconnect:
                case ENet.EventType.Timeout:
                    _connectionEvents.Enqueue(new ConnectionEvent
                    {
                        Connected = false,
                        ConnectionId = (int)enetEvent.Peer.ID
                    });

                    // Reset the peer array's entry for that peer.
                    _peers[enetEvent.Peer.ID] = default;
                    break;

                /* Packet was received. */
                case ENet.EventType.Receive:
                    EnqueueIncomingPacket(enetEvent);
                    break;
            }
        }

        /// <summary>
        /// Enqueues an incoming packet for processing.
        /// </summary>
        /// <param name="enetEvent"></param>
        /// <param name="maxPacketSize"></param>
        private void EnqueueIncomingPacket(ENet.Event enetEvent)
        {
            int length = enetEvent.Packet.Length;
            /* Kickable offenses.
             * Packet command but not set.
             * Packet has no length.
             * Packet over MTU. */
            if (!enetEvent.Packet.IsSet || length <= 0 || length > _mtus[enetEvent.ChannelID])
            {
                StopConnection((int)enetEvent.Peer.ID, true);
                enetEvent.Packet.Dispose();
                return;
            }
            //Packet exist and isn't too large.
            else
            {
                // Grab a fresh struct.
                IncomingPacket incomingQueuePacket = new IncomingPacket
                {
                    Channel = enetEvent.ChannelID,
                    ConnectionId = (int)enetEvent.Peer.ID,
                    Packet = enetEvent.Packet
                };

                _incoming.Enqueue(incomingQueuePacket);
            }
        }

        /// <summary>
        /// Allows for Outgoing queue to be iterated.
        /// </summary>
        internal void IterateOutgoing()
        {
            _dequeueOutgoing = true;
        }

        /// <summary>
        /// Iterates the Incoming queue.
        /// </summary>
        /// <param name="transport"></param>
        internal void IterateIncoming()
        {
            /* Run local connection states first so we can begin
             * to read for data at the start of the frame, as that's
             * where incoming is read. */
            while (_localConnectionStates.TryDequeue(out LocalConnectionStates state))
                base.SetLocalConnectionState(state);

            //Not yet started.
            if (base.GetConnectionState() != LocalConnectionStates.Started)
                return;

            //Handle connection and disconnection events.
            while (_connectionEvents.TryDequeue(out ConnectionEvent connectionEvent))
            {
                //Out of bounds.
                if (connectionEvent.ConnectionId < 0 || connectionEvent.ConnectionId >= _peers.Length)
                    continue;

                //Disconnected event.
                if (connectionEvent.Connected)
                {
                    _connectedClients.Add(connectionEvent.ConnectionId);
                    base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionStates.Started, connectionEvent.ConnectionId));
                }
                else
                {
                    _connectedClients.Remove(connectionEvent.ConnectionId);
                    base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionStates.Stopped, connectionEvent.ConnectionId));
                }
            }

            ServerReceivedDataArgs dataArgs = new ServerReceivedDataArgs();
            //Handle packets.
            while (_incoming.TryDequeue(out IncomingPacket incoming))
            {
                //Out of bounds.
                if (incoming.ConnectionId < 0 || incoming.ConnectionId >= _peers.Length)
                    continue;
                //Not a set peer.
                if (!_peers[incoming.ConnectionId].IsSet)
                    continue;

                //Copy to byte buffer and make arraysegment out of it.
                incoming.Packet.CopyTo(_incomingBuffer, 0);
                ArraySegment<byte> data = new ArraySegment<byte>(_incomingBuffer, 0, incoming.Packet.Length);
                //Tell generic transport to handle packet.
                dataArgs.Data = data;
                dataArgs.Channel = (Channel)incoming.Channel;
                dataArgs.ConnectionId = (int)incoming.ConnectionId;
                base.Transport.HandleServerReceivedDataArgs(dataArgs);
                //Cleanup enet packet.
                incoming.Packet.Dispose();
            }

        }

        /// <summary>
        /// Sends a packet to a single, or all clients.
        /// </summary>
        /// <param name="connectionId">Client to send packet to. Use -1 to send to all clients.</param>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, ChannelData[] channels, int connectionId)
        {
            //Out of bounds for channels.
            if (channelId < 0 || channelId >= channels.Length)
                return;
            //Server isn't active.
            if (base.GetConnectionState() != LocalConnectionStates.Started)
                return;

            Packet enetPacket = default;
            PacketFlags flags = (PacketFlags)channels[channelId].ChannelType;
            // Create the packet. //why is count + offset the length?
            enetPacket.Create(segment.Array, segment.Offset, segment.Offset + segment.Count, flags);

            OutgoingPacket outgoing = new OutgoingPacket
            {
                Channel = channelId,
                ConnectionId = connectionId,
                Packet = enetPacket
            };
            _outgoing.Enqueue(outgoing);
        }


    }
}
