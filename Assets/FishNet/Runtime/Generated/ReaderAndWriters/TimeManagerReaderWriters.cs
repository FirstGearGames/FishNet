using FishNet.Documenting;
using FishNet.Managing.Timing.Broadcast;
using FishNet.Serializing;
using System.Runtime.InteropServices;

namespace FishNet.Runtime
{
    [APIExclude]
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
    public static class TimeManagerReaderWriters
    {
        public static void WriteAX34(this PooledWriter writer, AddBufferedBroadcast value)
        {

        }
        public static AddBufferedBroadcast ReadJHD3h(this PooledReader reader)
        {
            return new AddBufferedBroadcast();
        }
        public static void Write___TimingAdjustmentBroadcast(this PooledWriter writer, TimingAdjustmentBroadcast value)
        {
            writer.WriteUInt32(value.Tick, AutoPackType.Unpacked);
            writer.WriteSByte(value.Step);
        }

        public static TimingAdjustmentBroadcast Read___TimingAdjustmentBroadcast(this PooledReader reader)
        {
            return new TimingAdjustmentBroadcast()
            {
                Tick = reader.ReadUInt32(AutoPackType.Unpacked),
                Step = reader.ReadSByte()
            };
        }


    }
}
