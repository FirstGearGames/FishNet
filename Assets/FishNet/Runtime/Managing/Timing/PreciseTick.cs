using FishNet.Serializing;
using GameKit.Dependencies.Utilities;

namespace FishNet.Managing.Timing
{

    public struct PreciseTick
    {
        /// <summary>
        /// The current tick.
        /// </summary>
        public uint Tick;
        /// <summary>
        /// Percentage of the tick returned between 0d and 1d.
        /// </summary>
        public double PercentAsDouble;
        /// <summary>
        /// Percentage of the tick returned between 0 and 100.
        /// </summary>
        public byte PercentAsByte;

        /// <summary>
        /// Creates a precise tick where the percentage is a byte between 0 and 100.
        /// </summary>
        public PreciseTick(uint tick, byte percentAsByte)
        {
            Tick = tick;
            PercentAsByte = percentAsByte;
            PercentAsDouble = (percentAsByte / 100d);
        }

        /// <summary>
        /// Creates a precise tick where the percentage is a double between 0d and 1d.
        /// </summary>
        public PreciseTick(uint tick, double percent)
        {
            Tick = tick;
            PercentAsByte = (byte)(percent * 100d);
            PercentAsDouble = percent;
        }

        /// <summary>
        /// Prints PreciseTick information as a string.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"Tick {Tick}, Percent {PercentAsByte.ToString("000")}";
    }

    public static class PreciseTickSerializer
    {
        public static void WritePreciseTick(this Writer writer, PreciseTick value)
        {
            writer.WriteTickUnpacked(value.Tick);
            writer.WriteUInt8Unpacked(value.PercentAsByte);
        }

        public static PreciseTick ReadPreciseTick(this Reader reader)
        {
            uint tick = reader.ReadTickUnpacked();
            byte percentByte = reader.ReadUInt8Unpacked();
            return new PreciseTick(tick, percentByte);
        }
    }
}