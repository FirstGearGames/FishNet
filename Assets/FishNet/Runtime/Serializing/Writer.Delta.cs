using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using FishNet.Utility;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo(UtilityConstants.GENERATED_ASSEMBLY_NAME)]
namespace FishNet.Serializing
{   

    /* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
    /* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
    /* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */

    /// <summary>
    /// Writes data to a buffer.
    /// </summary>
    public partial class Writer
    {
        [System.Flags]
        internal enum AutoPackTypeSigned : byte
        {
            Unpacked = 0,
            PackedNegative = 1,
            PackedLessNegative = 2,
            PackedPositive = 4,            
            PackedLessPositive = 8,
        }


        internal const float LARGEST_PACKED_UNSIGNED_FLOAT = ((float)byte.MaxValue / ACCURACY);
        internal const float LARGEST_PACKEDLESS_UNSIGNED_FLOAT = ((float)ushort.MaxValue / ACCURACY);
        internal const float LARGEST_PACKED_SIGNED_FLOAT = ((float)sbyte.MaxValue / ACCURACY);
        internal const float LARGEST_PACKEDLESS_SIGNED_FLOAT = ((float)short.MaxValue / ACCURACY);
        internal const float ACCURACY = 1000f;
        internal const float ACCURACY_DECIMAL = 0.001f;


        //[NotSerializer]
        //internal void WriteUInt16Delta(ushort a, ushort b)
        //{
        //    int difference = (int)(b - a);
        //    WritePackedWhole((ulong)difference);
        //}
        //[NotSerializer]
        //internal void WriteInt16Delta(short a, short b) => WriteUInt16Delta((ushort)a, (ushort)b);


        //[NotSerializer]
        //internal void WriteUInt32Delta(uint a, uint b)
        //{
        //    long difference = (long)(b - a);
        //    WritePackedWhole((ulong)difference);
        //}
        //[NotSerializer]
        //internal void WriteInt32Delta(int a, int b) => WriteUInt32Delta((uint)a, (uint)b);


        //[NotSerializer]
        //internal void WriteUInt64Delta(ulong a, ulong b)
        //{
        //    ulong difference;
        //    bool bLarger;
        //    if (b > a)
        //    {
        //        bLarger = true;
        //        difference = (b - a);
        //    }
        //    else
        //    {
        //        bLarger = false;
        //        difference = (a - b);
        //    }

        //    WriteBoolean(bLarger);
        //    WritePackedWhole(difference);
        //}
        //[NotSerializer]
        //internal void WriteInt64Delta(long a, long b) => WriteUInt64Delta((ulong)a, (ulong)b);

        //[NotSerializer]
        //internal void WriteLayerMaskDelta(LayerMask a, LayerMask b)
        //{
        //    WriteInt32Delta(b.value, a.value);
        //}

        [NotSerializer]
        internal void WriteSingleDelta(float a, float b)
        {
            double difference = (b - a);            
            AutoPackTypeSigned apts = GetAutoPackTypeSigned(difference);
            WriteByte((byte)apts);

            switch (apts)
            {
                case AutoPackTypeSigned.PackedNegative:
                case AutoPackTypeSigned.PackedPositive:
                    WriteByte((byte)(difference * ACCURACY));
                    return;
                case AutoPackTypeSigned.PackedLessNegative:
                case AutoPackTypeSigned.PackedLessPositive:
                    WriteUInt16((ushort)(difference * ACCURACY));
                    return;
                case AutoPackTypeSigned.Unpacked:
                    WriteSingle(b, AutoPackType.Unpacked);
                    return;
            }
        }

        private AutoPackTypeSigned GetAutoPackTypeSigned(double value)
        {
            double absValue = (value >= 0d) ? value : (value * -1d);

            if (absValue <= LARGEST_PACKED_UNSIGNED_FLOAT)
                return (value >= 0d) ? AutoPackTypeSigned.PackedPositive : AutoPackTypeSigned.PackedNegative;
            else if (absValue <= LARGEST_PACKEDLESS_UNSIGNED_FLOAT)
                return (value >= 0d) ? AutoPackTypeSigned.PackedLessPositive : AutoPackTypeSigned.PackedLessNegative;
            else
                return AutoPackTypeSigned.Unpacked;
        }

        //Draft / WIP below. May become discarded.

        //internal enum DeltaPackType
        //{
        //    Packed = 1,
        //    PackedLess = 2,
        //}
        //internal enum Vector3DeltaA : byte
        //{
        //    Unset = 0,
        //    XPacked = 1,
        //    XPackedLess = 2,
        //    XUnpacked = 4,
        //    YPacked = 8,
        //    YPackedLess = 16,
        //    YUnpacked = 32,
        //    ZPacked = 64,
        //    ZPackedLess = 128,
        //    ZUnpacked = 4,
        //}

        //internal const float LARGEST_PACKED_DIFFERENCE = ((float)sbyte.MaxValue / ACCURACY);
        //internal const float LARGEST_PACKEDLESS_DIFFERENCE = ((float)short.MaxValue / ACCURACY);
        //internal const float ACCURACY = 1000f;
        //internal const float ACCURACY_DECIMAL = 0.001f;


        //[NotSerializer]
        //public void WriteVector3Delta(Vector3 a, Vector3 b)
        //{
        //    //Start as the highest.

        //    float xDiff = (b.x - a.x);
        //    float yDiff = (b.x - a.x);
        //    float zDiff = (b.x - a.x);

        //    float absXDiff = Mathf.Abs(xDiff);
        //    float absYDiff = Mathf.Abs(yDiff);
        //    float absZDiff = Mathf.Abs(zDiff);

        //    float largestDiff = 0f;
        //    Vector3Delta delta = Vector3Delta.Unset;
        //    if (absXDiff >= ACCURACY_DECIMAL)
        //    {
        //        delta |= Vector3Delta.HasX;
        //        largestDiff = absXDiff;
        //    }
        //    if (absYDiff >= ACCURACY_DECIMAL)
        //    {
        //        delta |= Vector3Delta.HasY;
        //        largestDiff = Mathf.Max(largestDiff, absYDiff);
        //    }
        //    if (absZDiff >= ACCURACY_DECIMAL)
        //    {
        //        delta |= Vector3Delta.HasZ;
        //        largestDiff = Mathf.Max(largestDiff, absZDiff);
        //    }

        //    /* If packed is not specified then unpacked
        //     * is assumed. */
        //    if (largestDiff <= LARGEST_PACKED_DIFFERENCE)
        //        delta |= Vector3Delta.Packed;
        //    else if (largestDiff <= LARGEST_PACKEDLESS_DIFFERENCE)
        //        delta |= Vector3Delta.PackedLess;



        //    xDiff = Mathf.CeilToInt(xDiff * ACCURACY);
        //    yDiff = Mathf.CeilToInt(yDiff * ACCURACY);
        //    zDiff = Mathf.CeilToInt(zDiff * ACCURACY);

        //    Reserve(1);


        //    UIntFloat valA;
        //    UIntFloat valB;

        //    valA = new UIntFloat(a.x);
        //    valB = new UIntFloat(b.x);
        //    WriteUInt32(valB.UIntValue - valA.UIntValue, packType);
        //    Debug.Log("Diff " + (valB.UIntValue - valA.UIntValue) + ", " + (valA.UIntValue - valB.UIntValue));
        //    valA = new UIntFloat(a.y);
        //    valB = new UIntFloat(b.y);
        //    WriteUInt32(valB.UIntValue - valA.UIntValue, packType);
        //    valA = new UIntFloat(a.z);
        //    valB = new UIntFloat(b.z);
        //    WriteUInt32(valB.UIntValue - valA.UIntValue, packType);
        //}

    }
}
