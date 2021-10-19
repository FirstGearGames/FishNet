using FishNet.Broadcast;

namespace FishNet.Managing.Timing.Broadcast
{
    public struct AddBufferedBroadcast : IBroadcast { }
    public struct TimingAdjustmentBroadcast : IBroadcast
    {
        public uint Tick;
        public sbyte Step;
    }

}