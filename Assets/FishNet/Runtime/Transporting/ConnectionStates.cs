namespace FishNet.Transporting
{

    /// <summary>
    /// States the local connection can be in.
    /// </summary>
    public enum LocalConnectionState : byte
    {
        /// <summary>
        /// Connection is fully stopped.
        /// </summary>
        Stopped = 0,
        /// <summary>
        /// Connection is starting but not yet established.
        /// </summary>
        Starting = 1,
        /// <summary>
        /// Connection is established.
        /// </summary>
        Started = 2,
        /// <summary>
        /// Connection is stopping.
        /// </summary>
        Stopping = 3
    }

    /// <summary>
    /// States a remote client can be in.
    /// </summary>
    public enum RemoteConnectionState : byte
    {
        /// <summary>
        /// Connection is fully stopped.
        /// </summary>
        Stopped = 0,
        /// <summary>
        /// Connection is established.
        /// </summary>
        Started = 2,
    }


}