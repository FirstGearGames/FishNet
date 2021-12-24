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
        public static void WriteAddBufferedBroadcast(this Writer writer, AddBufferedBroadcast value)
        {

        }
        public static AddBufferedBroadcast ReadAddBufferedBroadcast(this Reader reader)
        {
            return new AddBufferedBroadcast();
        }
        public static void Write___TimingAdjustmentBroadcast(this Writer writer, TimingAdjustmentBroadcast value)
        {
            writer.WriteUInt32(value.Tick, AutoPackType.Unpacked);
            writer.WriteSByte(value.Step);
        }

        public static TimingAdjustmentBroadcast Read___TimingAdjustmentBroadcast(this Reader reader)
        {
            return new TimingAdjustmentBroadcast()
            {
                Tick = reader.ReadUInt32(AutoPackType.Unpacked),
                Step = reader.ReadSByte()
            };
        }


    }
}
