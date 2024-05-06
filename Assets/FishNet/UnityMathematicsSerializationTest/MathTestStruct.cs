using System;

using UnityEngine;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;


//[FishNet.CodeGenerating.IncludeSerialization]
public struct MathTestStruct {

        public bool2 b2;
        public bool3 b3;
        public bool4 b4;

        public bool2x2 b2x2;
        public bool2x3 b2x3;
        public bool2x4 b2x4;

        public bool3x2 b3x2;
        public bool3x3 b3x3;
        public bool3x4 b3x4;

        public bool4x2 b4x2;
        public bool4x3 b4x3;
        public bool4x4 b4x4;


        public int2 i2;
        public int3 i3;
        public int4 i4;

        public int2x2 i2x2;
        public int2x3 i2x3;
        public int2x4 i2x4;

        public int3x2 i3x2;
        public int3x3 i3x3;
        public int3x4 i3x4;

        public int4x2 i4x2;
        public int4x3 i4x3;
        public int4x4 i4x4;


        public uint2 ui2;
        public uint3 ui3;
        public uint4 ui4;

        public uint2x2 ui2x2;
        public uint2x3 ui2x3;
        public uint2x4 ui2x4;

        public uint3x2 ui3x2;
        public uint3x3 ui3x3;
        public uint3x4 ui3x4;

        public uint4x2 ui4x2;
        public uint4x3 ui4x3;
        public uint4x4 ui4x4;


        public float2 f2;
        public float3 f3;
        public float4 f4;

        public float2x2 f2x2;
        public float2x3 f2x3;
        public float2x4 f2x4;

        public float3x2 f3x2;
        public float3x3 f3x3;
        public float3x4 f3x4;

        public float4x2 f4x2;
        public float4x3 f4x3;
        public float4x4 f4x4;


        public double2 d2;
        public double3 d3;
        public double4 d4;

        public double2x2 d2x2;
        public double2x3 d2x3;
        public double2x4 d2x4;

        public double3x2 d3x2;
        public double3x3 d3x3;
        public double3x4 d3x4;

        public double4x2 d4x2;
        public double4x3 d4x3;
        public double4x4 d4x4;


        public half2 h2;
        public half3 h3;
        public half4 h4;

        public quaternion quaternion;

        public Unity.Mathematics.Random random;

        public RigidTransform transform;

#if UNITYMATHEMATICS_131
        public AffineTransform affineTransform;
#endif

#if UNITYMATHEMATICS_132
        public MinMaxAABB aabb;
#endif

    public static MathTestStruct GenerateFromSeed(uint seed) {

            Unity.Mathematics.Random rand = new Unity.Mathematics.Random(seed);

            return new MathTestStruct() {

                // BOOL
                b2 = rand.NextBool2(),
                b3 = rand.NextBool3(),
                b4 = rand.NextBool4(),

                b2x2 = new(rand.NextBool2(), rand.NextBool2()),
                b2x3 = new(rand.NextBool2(), rand.NextBool2(), rand.NextBool2()),
                b2x4 = new(rand.NextBool2(), rand.NextBool2(), rand.NextBool2(), rand.NextBool2()),

                b3x2 = new(rand.NextBool3(), rand.NextBool3()),
                b3x3 = new(rand.NextBool3(), rand.NextBool3(), rand.NextBool3()),
                b3x4 = new(rand.NextBool3(), rand.NextBool3(), rand.NextBool3(), rand.NextBool3()),

                b4x2 = new(rand.NextBool4(), rand.NextBool4()),
                b4x3 = new(rand.NextBool4(), rand.NextBool4(), rand.NextBool4()),
                b4x4 = new(rand.NextBool4(), rand.NextBool4(), rand.NextBool4(), rand.NextBool4()),

                // FLOAT
                f2 = rand.NextFloat2(),
                f3 = rand.NextFloat3(),
                f4 = rand.NextFloat4(),

                f2x2 = new(rand.NextFloat2(), rand.NextFloat2()),
                f2x3 = new(rand.NextFloat2(), rand.NextFloat2(), rand.NextFloat2()),
                f2x4 = new(rand.NextFloat2(), rand.NextFloat2(), rand.NextFloat2(), rand.NextFloat2()),

                f3x2 = new(rand.NextFloat3(), rand.NextFloat3()),
                f3x3 = new(rand.NextFloat3(), rand.NextFloat3(), rand.NextFloat3()),
                f3x4 = new(rand.NextFloat3(), rand.NextFloat3(), rand.NextFloat3(), rand.NextFloat3()),

                f4x2 = new(rand.NextFloat4(), rand.NextFloat4()),
                f4x3 = new(rand.NextFloat4(), rand.NextFloat4(), rand.NextFloat4()),
                f4x4 = new(rand.NextFloat4(), rand.NextFloat4(), rand.NextFloat4(), rand.NextFloat4()),

                // DOUBLE
                d2 = rand.NextDouble2(),
                d3 = rand.NextDouble3(),
                d4 = rand.NextDouble4(),

                d2x2 = new(rand.NextDouble2(), rand.NextDouble2()),
                d2x3 = new(rand.NextDouble2(), rand.NextDouble2(), rand.NextDouble2()),
                d2x4 = new(rand.NextDouble2(), rand.NextDouble2(), rand.NextDouble2(), rand.NextDouble2()),

                d3x2 = new(rand.NextDouble3(), rand.NextDouble3()),
                d3x3 = new(rand.NextDouble3(), rand.NextDouble3(), rand.NextDouble3()),
                d3x4 = new(rand.NextDouble3(), rand.NextDouble3(), rand.NextDouble3(), rand.NextDouble3()),

                d4x2 = new(rand.NextDouble4(), rand.NextDouble4()),
                d4x3 = new(rand.NextDouble4(), rand.NextDouble4(), rand.NextDouble4()),
                d4x4 = new(rand.NextDouble4(), rand.NextDouble4(), rand.NextDouble4(), rand.NextDouble4()),

                // INT
                i2 = rand.NextInt2(),
                i3 = rand.NextInt3(),
                i4 = rand.NextInt4(),

                i2x2 = new(rand.NextInt2(), rand.NextInt2()),
                i2x3 = new(rand.NextInt2(), rand.NextInt2(), rand.NextInt2()),
                i2x4 = new(rand.NextInt2(), rand.NextInt2(), rand.NextInt2(), rand.NextInt2()),

                i3x2 = new(rand.NextInt3(), rand.NextInt3()),
                i3x3 = new(rand.NextInt3(), rand.NextInt3(), rand.NextInt3()),
                i3x4 = new(rand.NextInt3(), rand.NextInt3(), rand.NextInt3(), rand.NextInt3()),

                i4x2 = new(rand.NextInt4(), rand.NextInt4()),
                i4x3 = new(rand.NextInt4(), rand.NextInt4(), rand.NextInt4()),
                i4x4 = new(rand.NextInt4(), rand.NextInt4(), rand.NextInt4(), rand.NextInt4()),

                ui2 = rand.NextUInt2(),
                ui3 = rand.NextUInt3(),
                ui4 = rand.NextUInt4(),

                // UINT
                ui2x2 = new(rand.NextUInt2(), rand.NextUInt2()),
                ui2x3 = new(rand.NextUInt2(), rand.NextUInt2(), rand.NextUInt2()),
                ui2x4 = new(rand.NextUInt2(), rand.NextUInt2(), rand.NextUInt2(), rand.NextUInt2()),

                ui3x2 = new(rand.NextUInt3(), rand.NextUInt3()),
                ui3x3 = new(rand.NextUInt3(), rand.NextUInt3(), rand.NextUInt3()),
                ui3x4 = new(rand.NextUInt3(), rand.NextUInt3(), rand.NextUInt3(), rand.NextUInt3()),

                ui4x2 = new(rand.NextUInt4(), rand.NextUInt4()),
                ui4x3 = new(rand.NextUInt4(), rand.NextUInt4(), rand.NextUInt4()),
                ui4x4 = new(rand.NextUInt4(), rand.NextUInt4(), rand.NextUInt4(), rand.NextUInt4()),

                // HALF
                h2 = new half2(rand.NextFloat2Direction()),
                h3 = new half3(rand.NextFloat3Direction()),
                h4 = new half4(rand.NextFloat()),

                // OTHER
                quaternion = rand.NextQuaternionRotation(),

                transform = new RigidTransform(rand.NextQuaternionRotation(), rand.NextFloat3Direction()),

                random = new Unity.Mathematics.Random(seed),


#if UNITYMATHEMATICS_131
        affineTransform = new AffineTransform(rand.NextFloat3Direction(), rand.NextQuaternionRotation()),
#endif

#if UNITYMATHEMATICS_132
        aabb = new MinMaxAABB(rand.NextFloat(), rand.NextFloat())
#endif
            };
        }


    }
