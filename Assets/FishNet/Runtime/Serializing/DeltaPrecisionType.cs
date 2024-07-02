namespace FishNet.Serializing
{

    [System.Flags]
    internal enum DeltaPrecisionType : byte
    {
        /// <summary>
        /// Indicates value could not be packed.
        /// </summary>
        Unpacked = 0,
        /// <summary>
        /// When set this indicates the new value is larger than the previous.
        /// When not set, indicates new value is smaller than the previous.
        /// </summary>
        NextValueIsLarger = 1,
        /// <summary>
        /// Data is written as a byte.
        /// </summary>
        UInt8 = 2,
        /// <summary>
        /// Data is written as a ushort.
        /// </summary>
        UInt16 = 4,
        /// <summary>
        /// Data is written as a uint.
        /// </summary>
        UInt32 = 8,
        /// <summary>
        /// Data is written as a ulong.
        /// </summary>
        UInt64 = 16,
        /// <summary>
        /// data is written as two ulong.
        /// </summary>
        UInt128 = 32,
    }

    internal static class DeltaPrecisionTypeExtensions
    {
        public static bool FastContains(this DeltaPrecisionType whole, DeltaPrecisionType part) => (whole & part) == part;
    }

}