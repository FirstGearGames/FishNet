using FishNet.Broadcast;

namespace FishNet.Managing.Timing.Broadcast
{
    public struct SynchronizeTickBroadcast : IBroadcast
    {
        public uint Tick;
    }
}