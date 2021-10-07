using FishNet.Managing;
using FishNet.Transporting;

namespace Fluidity
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

    }

}