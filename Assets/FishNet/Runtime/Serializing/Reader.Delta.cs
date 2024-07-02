using System;
using FishNet.CodeGenerating;
using System.Runtime.CompilerServices;
using FishNet.Managing;

namespace FishNet.Serializing
{
    ///* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
    ///* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
    ///* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
    public partial class Reader
    {
        internal double DOUBLE_ACCURACY => Writer.DOUBLE_ACCURACY;
        internal decimal DECIMAL_ACCURACY => Writer.DECIMAL_ACCURACY;

        [DefaultDeltaReader]
        public bool ReadDeltaBoolean(bool valueA) => ReadBoolean();


        #region Whole values.
        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadDeltaInt8(sbyte valueA) => (sbyte)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadDeltaUInt8(byte valueA) => (byte)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadDeltaInt16(short valueA) => (short)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadDeltaUInt16(ushort valueA) => (ushort)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadDeltaInt32(int valueA) => (int)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadDeltaUInt32(uint valueA) => (uint)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Returns a new result by reading and applying a difference to a value.
        /// </summary>
        [DefaultDeltaReader]
        private long ReadDifference8_16_32(long valueA)
        {
            long diff = ReadSignedPackedWhole();
            return (valueA + diff);
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadDeltaInt64(long valueA) => (long)ReadDeltaUInt64((ulong)valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadDeltaUInt64(ulong valueA)
        {
            bool bLargerThanA = ReadBoolean();
            ulong diff = ReadUnsignedPackedWhole();

            return (bLargerThanA) ? (valueA + diff) : (valueA - diff);
        }
        #endregion


        #region Precision values.
        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public float ReadDeltaSingle(float valueA)
        {
            DeltaPrecisionType dpt = (DeltaPrecisionType)ReadUInt8Unpacked();

            float diff = 0;
            if (dpt.FastContains(DeltaPrecisionType.UInt8))
                diff = (ReadUInt8Unpacked() / (float)DOUBLE_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.UInt16))
                diff = (ReadUInt16Unpacked() / (float)DOUBLE_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.Unpacked))
                diff = ReadSingleUnpacked();
            else
                NetworkManager.LogError($"Unhandled precision type of {dpt}.");

            bool bLargerThanA = dpt.FastContains(DeltaPrecisionType.NextValueIsLarger);
            return (bLargerThanA) ? (valueA + diff) : (valueA - diff);
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public double ReadDeltaDouble(double valueA)
        {
            DeltaPrecisionType dpt = (DeltaPrecisionType)ReadUInt8Unpacked();

            double diff = 0;
            if (dpt.FastContains(DeltaPrecisionType.UInt8))
                diff = (ReadUInt8Unpacked() / DOUBLE_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.UInt16))
                diff = (ReadUInt16Unpacked() / DOUBLE_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.UInt32))
                diff = (ReadUInt32Unpacked() / DOUBLE_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.Unpacked))
                diff = ReadDoubleUnpacked();
            else
                NetworkManager.LogError($"Unhandled precision type of {dpt}.");

            bool bLargerThanA = dpt.FastContains(DeltaPrecisionType.NextValueIsLarger);
            return (bLargerThanA) ? (valueA + diff) : (valueA - diff);
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public decimal ReadDeltaDecimal(decimal valueA)
        {
            DeltaPrecisionType dpt = (DeltaPrecisionType)ReadUInt8Unpacked();

            decimal diff = 0;
            if (dpt.FastContains(DeltaPrecisionType.UInt8))
                diff = (ReadUInt8Unpacked() / DECIMAL_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.UInt16))
                diff = (ReadUInt16Unpacked() / DECIMAL_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.UInt32))
                diff = (ReadUInt32Unpacked() / DECIMAL_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.UInt64))
                diff = (ReadUInt64Unpacked() / DECIMAL_ACCURACY);
            else if (dpt.FastContains(DeltaPrecisionType.Unpacked))
                diff = ReadDecimalUnpacked();
            else
                NetworkManager.LogError($"Unhandled precision type of {dpt}.");

            bool bLargerThanA = dpt.FastContains(DeltaPrecisionType.NextValueIsLarger);
            return (bLargerThanA) ? (valueA + diff) : (valueA - diff);
        }
        #endregion

    }

}