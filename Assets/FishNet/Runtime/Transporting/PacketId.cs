using FishNet.Documenting;

namespace FishNet.Transporting
{

    /// <summary>
    /// PacketIds to indicate the type of packet which is being sent or arriving.
    /// </summary>
    [APIExclude]
    public enum PacketId : ushort
    {
        Unset = 0,
        Authenticated = 1,
        Split = 2,
        ObjectSpawn = 3,
        ObjectDespawn = 4,
        PredictedSpawnResult = 5,
        SyncType = 7,
        ServerRpc = 8,
        ObserversRpc = 9,
        TargetRpc = 10,
        OwnershipChange = 11,
        Broadcast = 12,
        UNUSED = 13,
        PingPong = 14,
        Replicate = 15,
        Reconcile = 16,
        Disconnect = 17,
        TimingUpdate = 18,
        NetworkLODUpdate = 19,
        StateUpdate = 20,
        Version = 21,
    }

}