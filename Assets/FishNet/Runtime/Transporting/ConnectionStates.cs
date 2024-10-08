namespace FishNet.Transporting
{

    /// <summary>
    /// States the local connection can be in.
    /// </summary>
    public enum LocalConnectionState : int
    {
        /// <summary>
        /// Connection is fully stopped.
        /// </summary>
        Stopped = (1 << 3) | (1 << 4),
        /// <summary>
        /// Connection is stopping.
        /// </summary>
        Stopping = 1,
        /// <summary>
        /// Connection is starting but not yet established.
        /// </summary>
        Starting = 2,
        /// <summary>
        /// Connection is established.
        /// </summary>
        Started = 4,
        
        StoppedError = 8,
        StoppedClosed = 16,
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