namespace FishNet.Transporting
{

    /// <summary>
    /// States the local connection can be in.
    /// </summary>
    public enum LocalConnectionStates : byte
    {
        Stopped = 0,
        Starting = 1,
        Started = 2,
        Stopping = 3
    }

    /// <summary>
    /// States a remote client can be in.
    /// </summary>
    public enum RemoteConnectionStates : byte
    {
        Stopped = 0,
        Started = 2,
    }


}