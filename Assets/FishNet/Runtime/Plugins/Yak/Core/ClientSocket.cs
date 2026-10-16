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
        private Queue<LocalPacket> _incoming = new();
        #endregion

        // PROSTART
        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal override void Initialize(Transport t, CommonSocket socket)
        {
            base.Initialize(t, socket);
            _server = (ServerSocket)socket;
        }
        // PROEND

        /// <summary>
        /// Starts the client connection.
        /// </summary>
        internal bool StartConnection()
        {
            // PROSTART
            // Already starting/started, or stopping.
            if (GetLocalConnectionState() != LocalConnectionState.Stopped)
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
            //PROEND
            return true;
        }

        

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            
            return true;
        }

        

        

        

        #region Local server.
        
        #endregion
    }
}