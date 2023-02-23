//PROSTART
using FishNet.Transporting.Yak.Server;
using System;
using System.Collections.Generic;

namespace FishNet.Transporting.Yak.Client
{
    /// <summary>
    /// Creates a fake client connection to interact with the ServerSocket.
    /// </summary>
    public class ClientSocket : CommonSocket
    {
        #region Private.
        /// <summary>
        /// Socket for the server.
        /// </summary>
        private ServerSocket _server;
        /// <summary>
        /// Incomimg data.
        /// </summary>
        private Queue<LocalPacket> _incoming = new Queue<LocalPacket>();
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal override void Initialize(Transport t, CommonSocket socket)
        {
            base.Initialize(t, socket);
            _server = (ServerSocket)socket;
        }

        /// <summary>
        /// Starts the client connection.
        /// </summary>
        internal bool StartConnection()
        {
            //Already starting/started, or stopping.
            if (base.GetLocalConnectionState() != LocalConnectionState.Stopped)
                return false;

            SetLocalConnectionState(LocalConnectionState.Starting, false);
            /* Certain conditions need the client state to change as well.
             * Such as, if the server state is stopping then the client should
             * also be stopping, rather than starting. Or if the server state
             * is already started then client should immediately be set started
             * rather than waiting for server started callback. */
            LocalConnectionState serverState = _server.GetLocalConnectionState();
            if (serverState == LocalConnectionState.Stopping || serverState == LocalConnectionState.Started)
                OnLocalServerConnectionState(_server.GetLocalConnectionState());

            return true;
        }

        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        protected override void SetLocalConnectionState(LocalConnectionState connectionState, bool server)
        {
            base.SetLocalConnectionState(connectionState, server);
            if (connectionState == LocalConnectionState.Started || connectionState == LocalConnectionState.Stopped)
                _server.OnLocalClientConnectionState(connectionState);
        }

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            if (base.GetLocalConnectionState() == LocalConnectionState.Stopped || base.GetLocalConnectionState() == LocalConnectionState.Stopping)
                return false;

            base.ClearQueue(ref _incoming);
            //Immediately set stopped since no real connection exists.
            SetLocalConnectionState(LocalConnectionState.Stopping, false);
            SetLocalConnectionState(LocalConnectionState.Stopped, false);

            return true;
        }

        /// <summary>
        /// Iterations data received.
        /// </summary>
        internal void IterateIncoming()
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            while (_incoming.Count > 0)
            {
                LocalPacket packet = _incoming.Dequeue();
                ArraySegment<byte> segment = new ArraySegment<byte>(packet.Data, 0, packet.Length);
                ClientReceivedDataArgs dataArgs = new ClientReceivedDataArgs(segment, (Channel)packet.Channel, base.Transport.Index);
                base.Transport.HandleClientReceivedDataArgs(dataArgs);
                packet.Dispose();
            }
        }

        /// <summary>
        /// Called when the server sends the local client data.
        /// </summary>
        internal void ReceivedFromLocalServer(LocalPacket packet)
        {
            _incoming.Enqueue(packet);
        }

        /// <summary>
        /// Queues data to be sent to server.
        /// </summary>
        internal void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;
            if (_server.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            LocalPacket packet = new LocalPacket(segment, channelId);
            _server.ReceivedFromLocalClient(packet);
        }

        #region Local server.
        /// <summary>
        /// Called when the local server starts or stops.
        /// </summary>
        internal void OnLocalServerConnectionState(LocalConnectionState state)
        {
            //Server started.
            if (state == LocalConnectionState.Started &&
                base.GetLocalConnectionState() == LocalConnectionState.Starting)
            {
                SetLocalConnectionState(LocalConnectionState.Started, false);
            }
            //Server not started.
            else
            {
                //If stopped or stopping then disconnect client if also not stopped or stopping.
                if ((state == LocalConnectionState.Stopping || state == LocalConnectionState.Stopped) &&
                    (base.GetLocalConnectionState() == LocalConnectionState.Started ||
                    base.GetLocalConnectionState() == LocalConnectionState.Starting)
                    )
                {
                    SetLocalConnectionState(LocalConnectionState.Stopping, false);
                    SetLocalConnectionState(LocalConnectionState.Stopped, false);
                }
            }
        }
        #endregion


    }
}
//PROEND