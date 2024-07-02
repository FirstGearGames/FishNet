using System;
using FishNet.CodeGenerating;
using System.Runtime.CompilerServices;
using FishNet.Managing;

namespace FishNet.Serializing
{
    ///* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
    ///* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
    ///* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
    public partial class Writer
    {
        private const double LARGEST_DELTA_PRECISION_UINT8 = ((double)byte.MaxValue / DOUBLE_ACCURACY);
        private const double LARGEST_DELTA_PRECISION_UINT16 = ((double)ushort.MaxValue / DOUBLE_ACCURACY);
        private const double LARGEST_DELTA_PRECISION_UINT32 = ((double)uint.MaxValue / DOUBLE_ACCURACY);
        private const double LARGEST_DELTA_PRECISION_UINT64 = ((double)ulong.MaxValue / DOUBLE_ACCURACY);
        internal const double DOUBLE_ACCURACY = 1000d;
        internal const decimal DECIMAL_ACCURACY = 1000m;

        [DefaultDeltaWriter]
        public bool WriteDeltaBoolean(bool valueA, bool valueB)
        {
            if (valueA == valueB) return false;

            WriteBoolean(valueB);

            return true;
        }


        #region Whole values.
        [DefaultDeltaWriter]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteDeltaInt8(sbyte valueA, sbyte valueB) => WriteDifference8_16_32((long)valueA, (long)valueB);

        [DefaultDeltaWriter]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteDeltaUInt8(byte valueA, byte valueB) => WriteDifference8_16_32((long)valueA, (long)valueB);

        [DefaultDeltaWriter]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteDeltaInt16(short valueA, short valueB) => WriteDifference8_16_32((long)valueA, (long)valueB);

        [DefaultDeltaWriter]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteDeltaUInt16(ushort valueA, ushort valueB) => WriteDifference8_16_32((long)valueA, (long)valueB);

        [DefaultDeltaWriter]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteDeltaInt32(int valueA, int valueB) => WriteDifference8_16_32((long)valueA, (long)valueB);

        [DefaultDeltaWriter]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteDeltaUInt32(uint valueA, uint valueB) => WriteDifference8_16_32((long)valueA, (long)valueB);

        /// <summary>
        /// Writes the difference between two values for signed and unsigned shorts and ints.
        /// </summary>
        private bool WriteDifference8_16_32(long valueA, long valueB)
        {
            if (valueA == valueB) return false;

            long next = ((long)valueB - (long)valueA);
            WriteSignedPackedWhole(next);

            return true;
        }

        [DefaultDeltaWriter]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteDeltaInt64(long valueA, long valueB) => WriteDeltaUInt64((ulong)valueA, (ulong)valueB);

        [DefaultDeltaWriter]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WriteDeltaUInt64(ulong valueA, ulong valueB)
        {
            if (valueA == valueB) return false;

            bool bLargerThanA = (valueB > valueA);
            ulong next = (bLargerThanA) ? (valueB - valueA) : (valueA - valueB);

            WriteBoolean(bLargerThanA);
            WriteUnsignedPackedWhole(next);

            return true;
        }
        #endregion


        #region Precision values.
        private DeltaPrecisionType GetDeltaPrecisionPack(decimal positiveValue)
        {
            return positiveValue switch
            {
                < (decimal)LARGEST_DELTA_PRECISION_UINT8 => DeltaPrecisionType.UInt8,
                < (decimal)LARGEST_DELTA_PRECISION_UINT16 => DeltaPrecisionType.UInt16,
                < (decimal)LARGEST_DELTA_PRECISION_UINT32 => DeltaPrecisionType.UInt32,
                < (decimal)LARGEST_DELTA_PRECISION_UINT64 => DeltaPrecisionType.UInt64,
                _ => DeltaPrecisionType.Unpacked,
            };
        }

        private DeltaPrecisionType GetDeltaPrecisionPack(double positiveValue)
        {
            return positiveValue switch
            {
                < LARGEST_DELTA_PRECISION_UINT8 => DeltaPrecisionType.UInt8,
                < LARGEST_DELTA_PRECISION_UINT16 => DeltaPrecisionType.UInt16,
                < LARGEST_DELTA_PRECISION_UINT32 => DeltaPrecisionType.UInt32,
                _ => DeltaPrecisionType.Unpacked,
            };
        }

        private DeltaPrecisionType GetDeltaPrecisionPack(float positiveValue)
        {
            return positiveValue switch
            {
                < (float)LARGEST_DELTA_PRECISION_UINT8 => DeltaPrecisionType.UInt8,
                < (float)LARGEST_DELTA_PRECISION_UINT16 => DeltaPrecisionType.UInt16,
                _ => DeltaPrecisionType.Unpacked,
            };
        }

        [DefaultDeltaWriter]
        public bool WriteDeltaSingle(float valueA, float valueB)
        {
            if (valueA == valueB) return false;

            bool bLargerThanA = (valueB > valueA);
            float difference = (bLargerThanA) ? (valueB - valueA) : (valueA - valueB);
            DeltaPrecisionType dpt = GetDeltaPrecisionPack(difference);

            if (bLargerThanA)
                dpt |= DeltaPrecisionType.NextValueIsLarger;

            WriteSingleDeltaPrecision(dpt, difference);
            return true;
        }

        [DefaultDeltaWriter]
        public bool WriteDeltaDouble(double valueA, double valueB)
        {
            if (valueA == valueB) return false;

            bool bLargerThanA = (valueB > valueA);
            double difference = (bLargerThanA) ? (valueB - valueA) : (valueA - valueB);
            DeltaPrecisionType dpt = GetDeltaPrecisionPack(difference);

            if (bLargerThanA)
                dpt |= DeltaPrecisionType.NextValueIsLarger;

            WriteDoubleDeltaPrecision(dpt, difference);
            return true;
        }

        [DefaultDeltaWriter]
        public bool WriteDeltaDecimal(decimal valueA, decimal valueB)
        {
            if (valueA == valueB) return false;

            bool bLargerThanA = (valueB > valueA);
            decimal difference = (bLargerThanA) ? (valueB - valueA) : (valueA - valueB);
            DeltaPrecisionType dpt = GetDeltaPrecisionPack(difference);

            if (bLargerThanA)
                dpt |= DeltaPrecisionType.NextValueIsLarger;

            WriteDecimalDeltaPrecision(dpt, difference);
            return true;
        }

        private void WriteSingleDeltaPrecision(DeltaPrecisionType dpt, float positiveValue)
        {
            WriteUInt8Unpacked((byte)dpt);

            if (dpt.FastContains(DeltaPrecisionType.UInt8))
                WriteUInt8Unpacked((byte)Math.Floor(positiveValue * DOUBLE_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.UInt16))
                WriteUInt16Unpacked((ushort)Math.Floor(positiveValue * DOUBLE_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.Unpacked))
                WriteSingleUnpacked(positiveValue);
            else
                NetworkManagerExtensions.LogError($"Unhandled precision type of {dpt}.");
        }

        private void WriteDoubleDeltaPrecision(DeltaPrecisionType dpt, double positiveValue)
        {
            WriteUInt8Unpacked((byte)dpt);

            if (dpt.FastContains(DeltaPrecisionType.UInt8))
                WriteUInt8Unpacked((byte)Math.Floor(positiveValue * DOUBLE_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.UInt16))
                WriteUInt16Unpacked((ushort)Math.Floor(positiveValue * DOUBLE_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.UInt32))
                WriteUInt32Unpacked((uint)Math.Floor(positiveValue * DOUBLE_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.Unpacked))
                WriteDoubleUnpacked(positiveValue);
            else
                NetworkManagerExtensions.LogError($"Unhandled precision type of {dpt}.");
        }

        private void WriteDecimalDeltaPrecision(DeltaPrecisionType dpt, decimal positiveValue)
        {
            WriteUInt8Unpacked((byte)dpt);

            if (dpt.FastContains(DeltaPrecisionType.UInt8))
                WriteUInt8Unpacked((byte)Math.Floor(positiveValue * DECIMAL_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.UInt16))
                WriteUInt16Unpacked((ushort)Math.Floor(positiveValue * DECIMAL_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.UInt32))
                WriteUInt32Unpacked((uint)Math.Floor(positiveValue * DECIMAL_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.UInt64))
                WriteUInt64Unpacked((ulong)Math.Floor(positiveValue * DECIMAL_ACCURACY));
            else if (dpt.FastContains(DeltaPrecisionType.Unpacked))
                WriteDecimalUnpacked(positiveValue);
            else
                NetworkManagerExtensions.LogError($"Unhandled precision type of {dpt}.");
        }
        #endregion

    }


    ///// <summary>
    ///// Writes data to a buffer.
    ///// </summary>
    //public partial class Writer
    //{
    //    [System.Flags]
    //    internal enum AutoPackTypeSigned : byte
    //    {
    //        Unpacked = 0,
    //        PackedNegative = 1,
    //        PackedLessNegative = 2,
    //        PackedPositive = 4,            
    //        PackedLessPositive = 8,
    //    }


    //    internal const float LARGEST_PACKED_UNSIGNED_FLOAT = ((float)byte.MaxValue / ACCURACY);
    //    internal const float LARGEST_PACKEDLESS_UNSIGNED_FLOAT = ((float)ushort.MaxValue / ACCURACY);
    //    internal const float LARGEST_PACKED_SIGNED_FLOAT = ((float)sbyte.MaxValue / ACCURACY);
    //    internal const float LARGEST_PACKEDLESS_SIGNED_FLOAT = ((float)short.MaxValue / ACCURACY);
    //    internal const float ACCURACY = 1000f;
    //    internal const float ACCURACY_DECIMAL = 0.001f;


    //    //
    //    //internal void WriteUInt16Delta(ushort a, ushort b)
    //    //{
    //    //    int difference = (int)(b - a);
    //    //    WritePackedWhole((ulong)difference);
    //    //}
    //    //
    //    //internal void WriteInt16Delta(short a, short b) => WriteUInt16Delta((ushort)a, (ushort)b);


    //    //
    //    //internal void WriteUInt32Delta(uint a, uint b)
    //    //{
    //    //    long difference = (long)(b - a);
    //    //    WritePackedWhole((ulong)difference);
    //    //}
    //    //
    //    //internal void WriteInt32Delta(int a, int b) => WriteUInt32Delta((uint)a, (uint)b);


    //    //
    //    //internal void WriteUInt64Delta(ulong a, ulong b)
    //    //{
    //    //    ulong difference;
    //    //    bool bLarger;
    //    //    if (b > a)
    //    //    {
    //    //        bLarger = true;
    //    //        difference = (b - a);
    //    //    }
    //    //    else
    //    //    {
    //    //        bLarger = false;
    //    //        difference = (a - b);
    //    //    }

    //    //    WriteBoolean(bLarger);
    //    //    WritePackedWhole(difference);
    //    //}
    //    //
    //    //internal void WriteInt64Delta(long a, long b) => WriteUInt64Delta((ulong)a, (ulong)b);

    //    //
    //    //internal void WriteLayerMaskDelta(LayerMask a, LayerMask b)
    //    //{
    //    //    WriteInt32Delta(b.value, a.value);
    //    //}

    //    
    //    internal void WriteSingleDelta(float a, float b)
    //    {
    //        double difference = (b - a);            
    //        AutoPackTypeSigned apts = GetAutoPackTypeSigned(difference);
    //        WriteByte((byte)apts);

    //        switch (apts)
    //        {
    //            case AutoPackTypeSigned.PackedNegative:
    //            case AutoPackTypeSigned.PackedPositive:
    //                WriteByte((byte)(difference * ACCURACY));
    //                return;
    //            case AutoPackTypeSigned.PackedLessNegative:
    //            case AutoPackTypeSigned.PackedLessPositive:
    //                WriteUInt16((ushort)(difference * ACCURACY));
    //                return;
    //            case AutoPackTypeSigned.Unpacked:
    //                WriteSingleUnpacked(b);
    //                return;
    //        }
    //    }

    //    private AutoPackTypeSigned GetAutoPackTypeSigned(double value)
    //    {
    //        double absValue = (value >= 0d) ? value : (value * -1d);

    //        if (absValue <= LARGEST_PACKED_UNSIGNED_FLOAT)
    //            return (value >= 0d) ? AutoPackTypeSigned.PackedPositive : AutoPackTypeSigned.PackedNegative;
    //        else if (absValue <= LARGEST_PACKEDLESS_UNSIGNED_FLOAT)
    //            return (value >= 0d) ? AutoPackTypeSigned.PackedLessPositive : AutoPackTypeSigned.PackedLessNegative;
    //        else
    //            return AutoPackTypeSigned.Unpacked;
    //    }

    //    //Draft / WIP below. May become discarded.

    //    //internal enum DeltaPackType
    //    //{
    //    //    Packed = 1,
    //    //    PackedLess = 2,
    //    //}
    //    //internal enum Vector3DeltaA : byte
    //    //{
    //    //    Unset = 0,
    //    //    XPacked = 1,
    //    //    XPackedLess = 2,
    //    //    XUnpacked = 4,
    //    //    YPacked = 8,
    //    //    YPackedLess = 16,
    //    //    YUnpacked = 32,
    //    //    ZPacked = 64,
    //    //    ZPackedLess = 128,
    //    //    ZUnpacked = 4,
    //    //}

    //    //internal const float LARGEST_PACKED_DIFFERENCE = ((float)sbyte.MaxValue / ACCURACY);
    //    //internal const float LARGEST_PACKEDLESS_DIFFERENCE = ((float)short.MaxValue / ACCURACY);
    //    //internal const float ACCURACY = 1000f;
    //    //internal const float ACCURACY_DECIMAL = 0.001f;


    //    //
    //    //public void WriteVector3Delta(Vector3 a, Vector3 b)
    //    //{
    //    //    //Start as the highest.

    //    //    float xDiff = (b.x - a.x);
    //    //    float yDiff = (b.x - a.x);
    //    //    float zDiff = (b.x - a.x);

    //    //    float absXDiff = Mathf.Abs(xDiff);
    //    //    float absYDiff = Mathf.Abs(yDiff);
    //    //    float absZDiff = Mathf.Abs(zDiff);

    //    //    float largestDiff = 0f;
    //    //    Vector3Delta delta = Vector3Delta.Unset;
    //    //    if (absXDiff >= ACCURACY_DECIMAL)
    //    //    {
    //    //        delta |= Vector3Delta.HasX;
    //    //        largestDiff = absXDiff;
    //    //    }
    //    //    if (absYDiff >= ACCURACY_DECIMAL)
    //    //    {
    //    //        delta |= Vector3Delta.HasY;
    //    //        largestDiff = Mathf.Max(largestDiff, absYDiff);
    //    //    }
    //    //    if (absZDiff >= ACCURACY_DECIMAL)
    //    //    {
    //    //        delta |= Vector3Delta.HasZ;
    //    //        largestDiff = Mathf.Max(largestDiff, absZDiff);
    //    //    }

    //    //    /* If packed is not specified then unpacked
    //    //     * is assumed. */
    //    //    if (largestDiff <= LARGEST_PACKED_DIFFERENCE)
    //    //        delta |= Vector3Delta.Packed;
    //    //    else if (largestDiff <= LARGEST_PACKEDLESS_DIFFERENCE)
    //    //        delta |= Vector3Delta.PackedLess;


    //    //    xDiff = Mathf.CeilToInt(xDiff * ACCURACY);
    //    //    yDiff = Mathf.CeilToInt(yDiff * ACCURACY);
    //    //    zDiff = Mathf.CeilToInt(zDiff * ACCURACY);

    //    //    Reserve(1);


    //    //    UIntFloat valA;
    //    //    UIntFloat valB;

    //    //    valA = new UIntFloat(a.x);
    //    //    valB = new UIntFloat(b.x);
    //    //    WriteUInt32(valB.UIntValue - valA.UIntValue, packType);
    //    //    Debug.Log("Diff " + (valB.UIntValue - valA.UIntValue) + ", " + (valA.UIntValue - valB.UIntValue));
    //    //    valA = new UIntFloat(a.y);
    //    //    valB = new UIntFloat(b.y);
    //    //    WriteUInt32(valB.UIntValue - valA.UIntValue, packType);
    //    //    valA = new UIntFloat(a.z);
    //    //    valB = new UIntFloat(b.z);
    //    //    WriteUInt32(valB.UIntValue - valA.UIntValue, packType);
    //    //}

    //}
}