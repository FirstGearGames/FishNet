
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishNet.Serializing.Helping
{
    /// <summary>
    /// Static class used for fast conversion of quaternion structs. Not thread safe!
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct QuaternionConverter
    {
        //    [FieldOffset(0)]
        //    public Quaternion Q;
        //    [FieldOffset(0)]
        //    public Quaternion64 Q64;
        //    [FieldOffset(0)]
        //    public Quaternion128 Q128;

        //    public static QuaternionConverter StaticRef = new QuaternionConverter();

        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    public static Quaternion64 QtoQ64(Quaternion quaternion64)
        //    {
        //        StaticRef.Q = quaternion64;
        //        return StaticRef.Q64;
        //    }

        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    public static Quaternion Q64toQ(Quaternion64 quaternion)
        //    {
        //        StaticRef.Q64 = quaternion;
        //        return StaticRef.Q;
        //    }

        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    public static Quaternion128 QtoQ128(Quaternion quaternion128)
        //    {
        //        StaticRef.Q = quaternion128;
        //        return StaticRef.Q128;
        //    }

        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    public static Quaternion Q128toQ(Quaternion128 quaternion)
        //    {
        //        StaticRef.Q128 = quaternion;
        //        return StaticRef.Q;
        //    }
        //}

        //public struct Quaternion64
        //{
        //    public float x;
        //    public float y;
        //    public float z;
        //    public float w;

        //    public Quaternion64(float x, float y, float z, float w)
        //    {
        //        this.x = x;
        //        this.y = y;
        //        this.z = z;
        //        this.w = w;
        //    }

        //    public Quaternion64(Quaternion q)
        //    {
        //        this.x = q.x;
        //        this.y = q.y;
        //        this.z = q.z;
        //        this.w = q.w;
        //    }

        //    /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    public static implicit operator Quaternion64(Quaternion q) => new Quaternion64(q);

        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    public static implicit operator Quaternion(Quaternion64 q) => new Quaternion(q.x, q.y, q.z, q.w);*/
        //}

        //public struct Quaternion128
        //{
        //    public float x;
        //    public float y;
        //    public float z;
        //    public float w;
        //    public Quaternion128(float x, float y, float z, float w)
        //    {
        //        this.x = x;
        //        this.y = y;
        //        this.z = z;
        //        this.w = w;
        //    }

        //    public Quaternion128(Quaternion q)
        //    {
        //        x = q.x;
        //        y = q.y;
        //        z = q.z;
        //        w = q.w;
        //    }

        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    public static implicit operator Quaternion128(Quaternion q) => new Quaternion128(q);

        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    public static implicit operator Quaternion(Quaternion128 q) => new Quaternion(q.x, q.y, q.z, q.w);
        //}

        /// <summary>
        ///     Credit to this man for converting gaffer games c code to c#
        ///     https://gist.github.com/fversnel/0497ad7ab3b81e0dc1dd
        /// </summary>
    }

    public enum ComponentType : uint
    {
        X = 0,
        Y = 1,
        Z = 2,
        W = 3
    }

}