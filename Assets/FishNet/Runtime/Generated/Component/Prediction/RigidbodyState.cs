using FishNet.Component.Prediction;
using FishNet.Serializing;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    public struct RigidbodyState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsKinematic;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
    }

    public struct Rigidbody2DState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool Simulated;
        public Vector3 Velocity;
        public float AngularVelocity;
    }
}

public static class RigidbodyStateSerializers
{

    public static void WriteRigidbodyState(this Writer writer, RigidbodyState value)
    {
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
        RigidbodyState rbs = new RigidbodyState()
        {
            Position = reader.ReadVector3(),
            Rotation = reader.ReadQuaternion(),
            IsKinematic = reader.ReadBoolean()
        };
        if (!rbs.IsKinematic)
        {
            rbs.Velocity = reader.ReadVector3();
            rbs.AngularVelocity = reader.ReadVector3();
        }

        return rbs;
    }


    public static void WriteRigidbody2DState(this Writer writer, Rigidbody2DState value)
    {
        writer.WriteVector3(value.Position);
        writer.WriteQuaternion(value.Rotation);
        writer.WriteBoolean(value.Simulated);
        if (!value.Simulated)
        {
            writer.WriteVector3(value.Velocity);
            writer.WriteSingle(value.AngularVelocity);
        }
    }

    public static Rigidbody2DState ReadRigidbody2DState(this Reader reader)
    {
        Rigidbody2DState rbs = new Rigidbody2DState()
        {
            Position = reader.ReadVector3(),
            Rotation = reader.ReadQuaternion(),
            Simulated = reader.ReadBoolean()
        };
        if (!rbs.Simulated)
        {
            rbs.Velocity = reader.ReadVector3();
            rbs.AngularVelocity = reader.ReadSingle();
        }

        return rbs;
    }


}

