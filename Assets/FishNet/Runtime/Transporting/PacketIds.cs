namespace FishNet.Transporting
{

    /// <summary>
    /// PacketIds to indicate the type of packet which is being sent or arriving.
    /// </summary>
    public enum PacketId : byte
    {
        Unset = 0,
        Authentication = 1,
        Unused = 2,
        ObjectSpawn = 3,
        ObjectDespawn = 4,
        Event = 5,
        SyncVar = 6,
        ServerRpc = 7,
        ObserversRpc = 8,
        Split = 9,
        TargetRpc = 10,
        ConnectionId = 11,
        OwnershipChange = 12,
        Broadcast = 13,
        SyncObject = 14
    }

}