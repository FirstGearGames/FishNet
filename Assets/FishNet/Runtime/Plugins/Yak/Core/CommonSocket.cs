//PROSTART
using FishNet.Transporting;
using System.Collections.Generic;

namespace FishNet.Transporting.Yak
{

    public abstract class CommonSocket
    {

        #region Public.
        /// <summary>
        /// Current ConnectionState.
        /// </summary>
        private LocalConnectionState _connectionState = LocalConnectionState.Stopped;
        /// <summary>
        /// Returns the current ConnectionState.
        /// </summary>
        /// <returns></returns>
        internal LocalConnectionState GetLocalConnectionState()
        {
            return _connectionState;
        }
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        protected virtual void SetLocalConnectionState(LocalConnectionState connectionState, bool server)
        {
            //If state hasn't changed.
            if (connectionState == _connectionState)
                return;

            _connectionState = connectionState;

            if (server)
                Transport.HandleServerConnectionState(new ServerConnectionStateArgs(connectionState, Transport.Index));
            else
                Transport.HandleClientConnectionState(new ClientConnectionStateArgs(connectionState, Transport.Index));
        }
        #endregion

        #region Protected.
        /// <summary>
        /// Transport controlling this socket.
        /// </summary>
        protected Transport Transport = null;
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal virtual void Initialize(Transport t, CommonSocket socket)
        {
            Transport = t;
        }

        /// <summary>
        /// Clears a queue.
        /// </summary>
        internal void ClearQueue(ref Queue<LocalPacket> queue)
        {
            while (queue.Count > 0)
            {
                LocalPacket lp = queue.Dequeue();
                lp.Dispose();
            }
        }
    }

}
//PROEND