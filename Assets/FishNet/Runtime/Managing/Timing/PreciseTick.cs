using FishNet.Serializing;
using GameKit.Utilities;

namespace FishNet.Managing.Timing
{

    public struct PreciseTick
    {
        /// <summary>
        /// The current tick.
        /// </summary>
        public uint Tick;
        /// <summary>
        /// Percentage into the next tick.
        /// </summary>
        public double Percent;

        public PreciseTick(uint tick, double percent)
        {
            Tick = tick;
            Percent = percent;
        }

        /// <summary>
        /// Prints PreciseTick information as a string.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"Tick {Tick}, Percent {Percent.ToString("000")}";
    }

    public static class PreciseTickSerializer
    {
        public static void WritePreciseTick(this Writer writer, PreciseTick value)
        {
            writer.WriteTickUnpacked(value.Tick);
            /* No reason percent should exist beyond these values, but better to be safe.
             * There is also no double clamp in Unity so... */
            double percent = Maths.ClampDouble(value.Percent, 0d, 1f);
            byte percentByte = (byte)(percent * 100);
            writer.WriteByte(percentByte);
        }

        public static PreciseTick ReadPreciseTick(this Reader reader)
        {
            uint tick = reader.ReadTickUnpacked();
            byte percentByte = reader.ReadByte();
            double percent = Maths.ClampDouble((percentByte / 100f), 0d, 1d);
            return new PreciseTick(tick, percent);
        }
    }
}