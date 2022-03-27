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
        public static void WriteUpdateTicksBroadcast(this Writer writer, UpdateTicksBroadcast value)
        {

        }
        public static UpdateTicksBroadcast ReadUpdateTicksBroadcast(this Reader reader)
        {
            return new UpdateTicksBroadcast();
        }

        public static void WriteSynchronizeTickBroadcast(this Writer writer, SynchronizeTickBroadcast value)
        {
            writer.WriteUInt32(value.Tick, AutoPackType.Unpacked);
        }
        public static SynchronizeTickBroadcast ReadSynchronizeTickBroadcast(this Reader reader)
        {
            return new SynchronizeTickBroadcast()
            {
                Tick = reader.ReadUInt32(AutoPackType.Unpacked)
            };
        }

    }
}
