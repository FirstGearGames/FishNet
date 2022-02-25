using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishNet.Serializing.Helping
{
    public static class Quaternions
    {
        /// <summary>
        /// Static class used for fast conversion of quaternion structs. Not thread safe!
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct QuaternionConverter
        {
            [FieldOffset(0)]
            public Quaternion Q;
            [FieldOffset(0)]
            public Quaternion64 Q64;
            [FieldOffset(0)]
            public Quaternion128 Q128;

            public static QuaternionConverter StaticRef = new QuaternionConverter();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Quaternion64 QtoQ64(Quaternion quaternion64)
            {
                StaticRef.Q = quaternion64;
                return StaticRef.Q64;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Quaternion Q64toQ(Quaternion64 quaternion)
            {
                StaticRef.Q64 = quaternion;
                return StaticRef.Q;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Quaternion128 QtoQ128(Quaternion quaternion128)
            {
                StaticRef.Q = quaternion128;
                return StaticRef.Q128;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Quaternion Q128toQ(Quaternion128 quaternion)
            {
                StaticRef.Q128 = quaternion;
                return StaticRef.Q;
            }
        }

        public struct Quaternion64
        {
            public float x;
            public float y;
            public float z;
            public float w;

            public Quaternion64(float x, float y, float z, float w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }

            public Quaternion64(Quaternion q)
            {
                this.x = q.x;
                this.y = q.y;
                this.z = q.z;
                this.w = q.w;
            }

            /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Quaternion64(Quaternion q) => new Quaternion64(q);
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Quaternion(Quaternion64 q) => new Quaternion(q.x, q.y, q.z, q.w);*/
        }
        
        public struct Quaternion128
        {
            public float x;
            public float y;
            public float z;
            public float w;
            public Quaternion128(float x, float y, float z, float w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }

            public Quaternion128(Quaternion q)
            {
                x = q.x;
                y = q.y;
                z = q.z;
                w = q.w;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Quaternion128(Quaternion q) => new Quaternion128(q);
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Quaternion(Quaternion128 q) => new Quaternion(q.x, q.y, q.z, q.w);
        }

        /// <summary>
        ///     Credit to this man for converting gaffer games c code to c#
        ///     https://gist.github.com/fversnel/0497ad7ab3b81e0dc1dd
        /// </summary>
        private enum ComponentType : uint
        {
            X = 0,
            Y = 1,
            Z = 2,
            W = 3
        }

        // note: 1.0f / sqrt(2)
        private const float Maximum = +1.0f / 1.414214f;
        //private const float Maximum = +1.0f / 1.41421356237f;

        #region Compression 32 bits.
        
        private const int BitsPerAxis32 = 10;
        private const int LargestComponentShift32 = BitsPerAxis32 * 3;
        private const int AShift32 = BitsPerAxis32 * 2;
        private const int BShift32 = BitsPerAxis32 * 1;
        private const int IntScale32 = (1 << (BitsPerAxis32 - 1)) - 1;
        private const int IntMask32 = (1 << BitsPerAxis32) - 1;

        internal static uint Compress32(Quaternion quaternion)
        {
            float absX = Mathf.Abs(quaternion.x);
            float absY = Mathf.Abs(quaternion.y);
            float absZ = Mathf.Abs(quaternion.z);
            float absW = Mathf.Abs(quaternion.w);

            ComponentType largestComponent = ComponentType.X;
            float largestAbs = absX;
            float largest = quaternion.x;

            if (absY > largestAbs)
            {
                largestAbs = absY;
                largestComponent = ComponentType.Y;
                largest = quaternion.y;
            }
            if (absZ > largestAbs)
            {
                largestAbs = absZ;
                largestComponent = ComponentType.Z;
                largest = quaternion.z;
            }
            if (absW > largestAbs)
            {
                largestComponent = ComponentType.W;
                largest = quaternion.w;
            }

            float a = 0;
            float b = 0;
            float c = 0;
            switch (largestComponent)
            {
                case ComponentType.X:
                    a = quaternion.y;
                    b = quaternion.z;
                    c = quaternion.w;
                    break;
                case ComponentType.Y:
                    a = quaternion.x;
                    b = quaternion.z;
                    c = quaternion.w;
                    break;
                case ComponentType.Z:
                    a = quaternion.x;
                    b = quaternion.y;
                    c = quaternion.w;
                    break;
                case ComponentType.W:
                    a = quaternion.x;
                    b = quaternion.y;
                    c = quaternion.z;
                    break;
            }

            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            uint integerA = ScaleToUint32(a);
            uint integerB = ScaleToUint32(b);
            uint integerC = ScaleToUint32(c);

            return (((uint)largestComponent) << LargestComponentShift32) | (integerA << AShift32) | (integerB << BShift32) | integerC;
        }

        private static uint ScaleToUint32(float v)
        {
            float normalized = v / Maximum;
            return (uint)Mathf.RoundToInt(normalized * IntScale32) & IntMask32;
        }

        private static float ScaleToFloat32(uint v)
        {
            float unscaled = v * Maximum / IntScale32;

            if (unscaled > Maximum)
                unscaled -= Maximum * 2;
            return unscaled;
        }

        internal static Quaternion Decompress32(uint compressed)
        {
            var largestComponentType = (ComponentType)(compressed >> LargestComponentShift32);
            uint integerA = (compressed >> AShift32) & IntMask32;
            uint integerB = (compressed >> BShift32) & IntMask32;
            uint integerC = compressed & IntMask32;

            float a = ScaleToFloat32(integerA);
            float b = ScaleToFloat32(integerB);
            float c = ScaleToFloat32(integerC);

            Quaternion rotation;
            switch (largestComponentType)
            {
                case ComponentType.X:
                    // (?) y z w
                    rotation.y = a;
                    rotation.z = b;
                    rotation.w = c;
                    rotation.x = Mathf.Sqrt(1 - rotation.y * rotation.y
                                               - rotation.z * rotation.z
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.Y:
                    // x (?) z w
                    rotation.x = a;
                    rotation.z = b;
                    rotation.w = c;
                    rotation.y = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.z * rotation.z
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.Z:
                    // x y (?) w
                    rotation.x = a;
                    rotation.y = b;
                    rotation.w = c;
                    rotation.z = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.y * rotation.y
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.W:
                    // x y z (?)
                    rotation.x = a;
                    rotation.y = b;
                    rotation.z = c;
                    rotation.w = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.y * rotation.y
                                               - rotation.z * rotation.z);
                    break;
                default:
                    // Should never happen!
                    throw new ArgumentOutOfRangeException("Unknown rotation component type: " +
                                                          largestComponentType);
            }

            return rotation;
        }
        #endregion

        #region Compression 64 bits.
        
        // 64 bit quaternion compression
        // [4 bits] largest component
        // [21 bits] higher res 
        // [21 bits] higher res
        // [20 bits] higher res
        // sum is 64 bits

        private const int BitsPerAxis64_H = 21; // higher res, 21 bits
        private const int BitsPerAxis64_L = 20; // lower res, 20 bits
        private const int LargestComponentShift64 = BitsPerAxis64_H * 2 + BitsPerAxis64_L * 1;
        private const int AShift64 = BitsPerAxis64_H + BitsPerAxis64_L;
        private const int BShift64 = BitsPerAxis64_L;
        private const int IntScale64_H = (1 << (BitsPerAxis64_H - 1)) - 1;
        private const int IntMask64_H  = (1 << BitsPerAxis64_H) - 1;
        private const int IntScale64_L = (1 << (BitsPerAxis64_L - 1)) - 1;
        private const int IntMask64_L  = (1 << BitsPerAxis64_L) - 1;

        internal static ulong Compress64(Quaternion64 quaternion)
        {
            float absX = Mathf.Abs(quaternion.x);
            float absY = Mathf.Abs(quaternion.y);
            float absZ = Mathf.Abs(quaternion.z);
            float absW = Mathf.Abs(quaternion.w);

            ComponentType largestComponent = ComponentType.X;
            float largestAbs = absX;
            float largest = quaternion.x;

            if (absY > largestAbs)
            {
                largestAbs = absY;
                largestComponent = ComponentType.Y;
                largest = quaternion.y;
            }
            if (absZ > largestAbs)
            {
                largestAbs = absZ;
                largestComponent = ComponentType.Z;
                largest = quaternion.z;
            }
            if (absW > largestAbs)
            {
                largestComponent = ComponentType.W;
                largest = quaternion.w;
            }

            float a = 0;
            float b = 0;
            float c = 0;

            switch (largestComponent)
            {
                case ComponentType.X:
                    a = quaternion.y;
                    b = quaternion.z;
                    c = quaternion.w;
                    break;
                case ComponentType.Y:
                    a = quaternion.x;
                    b = quaternion.z;
                    c = quaternion.w;
                    break;
                case ComponentType.Z:
                    a = quaternion.x;
                    b = quaternion.y;
                    c = quaternion.w;
                    break;
                case ComponentType.W:
                    a = quaternion.x;
                    b = quaternion.y;
                    c = quaternion.z;
                    break;
            }

            if (largest < 0)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            ulong integerA = ScaleToUint64_H(a);
            ulong integerB = ScaleToUint64_H(b);
            ulong integerC = ScaleToUint64_L(c);

            return (((ulong)largestComponent) << LargestComponentShift64) | (integerA << AShift64) | (integerB << BShift64) | integerC;
        }

        private static ulong ScaleToUint64_H(float v)
        {
            float normalized = v / Maximum;
            return (ulong)Mathf.RoundToInt(normalized * IntScale64_H) & IntMask64_H;
        }

        private static ulong ScaleToUint64_L(float v)
        {
            float normalized = v / Maximum;
            return (ulong)Mathf.RoundToInt(normalized * IntScale64_L) & IntMask64_L;
        }

        private static float ScaleToFloat64_H(ulong v)
        {
            float unscaled = v * Maximum / IntScale64_H;

            if (unscaled > Maximum)
                unscaled -= Maximum * 2;
            return unscaled;
        }

        private static float ScaleToFloat64_L(ulong v)
        {
            float unscaled = v * Maximum / IntScale64_L;

            if (unscaled > Maximum)
                unscaled -= Maximum * 2;
            return unscaled;
        }

        internal static Quaternion64 Decompress64(ulong compressed)
        {
            var largestComponentType = (ComponentType)(compressed >> LargestComponentShift64);
            ulong integerA = (compressed >> AShift64) & IntMask64_H;
            ulong integerB = (compressed >> BShift64) & IntMask64_H;
            ulong integerC = compressed & IntMask64_L;

            float a = ScaleToFloat64_H(integerA);
            float b = ScaleToFloat64_H(integerB);
            float c = ScaleToFloat64_L(integerC);

            Quaternion64 rotation;
            switch (largestComponentType)
            {
                case ComponentType.X:
                    // (?) y z w
                    rotation.y = a;
                    rotation.z = b;
                    rotation.w = c;
                    rotation.x = Mathf.Sqrt(1 - rotation.y * rotation.y
                                               - rotation.z * rotation.z
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.Y:
                    // x (?) z w
                    rotation.x = a;
                    rotation.z = b;
                    rotation.w = c;
                    rotation.y = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.z * rotation.z
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.Z:
                    // x y (?) w
                    rotation.x = a;
                    rotation.y = b;
                    rotation.w = c;
                    rotation.z = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.y * rotation.y
                                               - rotation.w * rotation.w);
                    break;
                case ComponentType.W:
                    // x y z (?)
                    rotation.x = a;
                    rotation.y = b;
                    rotation.z = c;
                    rotation.w = Mathf.Sqrt(1 - rotation.x * rotation.x
                                               - rotation.y * rotation.y
                                               - rotation.z * rotation.z);
                    break;
                default:
                    // Should never happen!
                    throw new ArgumentOutOfRangeException("Unknown rotation component type: " +
                                                          largestComponentType);
            }

            return rotation;
        }
        #endregion

    }
}
