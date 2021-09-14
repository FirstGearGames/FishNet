using FishNet.Managing.Timing.Broadcast;
using FishNet.Serializing;
using System.Runtime.InteropServices;

namespace FishNet.Runtime
{
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
    public static class TimeManagerReaderWriters
    {
        public static void Write___StepChangeBroadcast(this PooledWriter writer, StepChangeBroadcast value)
        {
            writer.WriteSByte(value.Step);
        }

        public static StepChangeBroadcast Read___StepChangeBroadcast(this PooledReader reader)
        {
            return new StepChangeBroadcast()
            {
                Step = reader.ReadSByte()
            };
        }


    }
}
