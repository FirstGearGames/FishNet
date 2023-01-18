﻿using FishNet.Component.Prediction;
using FishNet.Serializing;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    public struct RigidbodyState
    {
        public uint LocalTick;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsKinematic;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;

        public RigidbodyState(Rigidbody rb, uint tick)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            IsKinematic = rb.isKinematic;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;
            LocalTick = tick;
        }
    }

    public struct Rigidbody2DState
    {
        public uint LocalTick;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool Simulated;
        public Vector2 Velocity;
        public float AngularVelocity;

        public Rigidbody2DState(Rigidbody2D rb, uint tick)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            Simulated = rb.simulated;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;
            LocalTick = tick;
        }
    }
}

public static class RigidbodyStateSerializers
{

    public static void WriteRigidbodyState(this Writer writer, RigidbodyState value)
    {
        writer.WriteUInt32(value.LocalTick, AutoPackType.Unpacked);
        writer.WriteVector3(value.Position);
        writer.WriteQuaternion(value.Rotation);
        writer.WriteBoolean(value.IsKinematic);
        if (!value.IsKinematic)
        {
            writer.WriteVector3(value.Velocity);
            writer.WriteVector3(value.AngularVelocity);
        }
    }

    public static RigidbodyState ReadRigidbodyState(this Reader reader)
    {
        RigidbodyState state = new RigidbodyState()
        {
            LocalTick = reader.ReadUInt32(AutoPackType.Unpacked),
            Position = reader.ReadVector3(),
            Rotation = reader.ReadQuaternion(),
            IsKinematic = reader.ReadBoolean()
        };

        if (!state.IsKinematic)
        {
            state.Velocity = reader.ReadVector3();
            state.AngularVelocity = reader.ReadVector3();
        }

        return state;
    }


    public static void WriteRigidbody2DState(this Writer writer, Rigidbody2DState value)
    {
        writer.WriteUInt32(value.LocalTick, AutoPackType.Unpacked);
        writer.WriteVector3(value.Position);
        writer.WriteQuaternion(value.Rotation);
        writer.WriteBoolean(value.Simulated);

        if (value.Simulated)
        {
            writer.WriteVector3(value.Velocity);
            writer.WriteSingle(value.AngularVelocity);
        }
    }

    public static Rigidbody2DState ReadRigidbody2DState(this Reader reader)
    {
        Rigidbody2DState state = new Rigidbody2DState()
        {
            LocalTick = reader.ReadUInt32(AutoPackType.Unpacked),
            Position = reader.ReadVector3(),
            Rotation = reader.ReadQuaternion(),
            Simulated = reader.ReadBoolean(),
        };

        if (state.Simulated)
        {
            state.Velocity = reader.ReadVector3();
            state.AngularVelocity = reader.ReadSingle();
        }

        return state;
    }


}

