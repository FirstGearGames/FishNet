using FishNet.Managing.Timing.Broadcast;
using FishNet.Serializing;
using System.Runtime.InteropServices;

namespace FishNet.Runtime
{
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
    public static class TimeManagerReaderWriters
    {
        public static void Write___TickSyncBroadcast(this PooledWriter writer, TickSyncBroadcast value)
        {
            writer.WriteUInt32(value.Tick, AutoPackType.Packed);            
        }

        public static TickSyncBroadcast Read___TickSyncBroadcast(this PooledReader reader)
        {
            return new TickSyncBroadcast()
            {
                Tick = reader.ReadUInt32(AutoPackType.Packed)
            };
        }


    }
}
