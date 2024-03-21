#if PREDICTION_V2
using FishNet.CodeGenerating;
using FishNet.Serializing;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    [UseGlobalCustomSerializer]
    public struct RigidbodyState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsKinematic;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;

        public RigidbodyState(Rigidbody rb, bool isKinematic) : this(rb)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            IsKinematic = isKinematic;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;

        }

        public RigidbodyState(Rigidbody rb)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            IsKinematic = rb.isKinematic;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;
        }
    }

    [UseGlobalCustomSerializer]
    public struct Rigidbody2DState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector2 Velocity;
        public float AngularVelocity;
        public bool Simulated;
        public bool IsKinematic;

        public Rigidbody2DState(Rigidbody2D rb, bool simulated)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;
            Simulated = simulated;
            IsKinematic = rb.isKinematic;
        }

        public Rigidbody2DState(Rigidbody2D rb)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;
            Simulated = rb.simulated;
            IsKinematic = rb.isKinematic;
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
            RigidbodyState state = new RigidbodyState()
            {
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
            writer.WriteVector3(value.Position);
            writer.WriteQuaternion(value.Rotation);
            writer.WriteBoolean(value.Simulated);
            writer.WriteBoolean(value.IsKinematic);

            if (value.Simulated)
            {
                writer.WriteVector2(value.Velocity);
                writer.WriteSingle(value.AngularVelocity);
            }
        }

        public static Rigidbody2DState ReadRigidbody2DState(this Reader reader)
        {
            Rigidbody2DState state = new Rigidbody2DState()
            {
                Position = reader.ReadVector3(),
                Rotation = reader.ReadQuaternion(),
                Simulated = reader.ReadBoolean(),
                IsKinematic = reader.ReadBoolean()
            };

            if (state.Simulated)
            {
                state.Velocity = reader.ReadVector2();
                state.AngularVelocity = reader.ReadSingle();
            }

            return state;
        }


    }


    public static class RigidbodyStateExtensions
    {
        /// <summary>
        /// Gets a RigidbodyState.
        /// </summary>
        public static RigidbodyState GetState(this Rigidbody rb)
        {
            return new RigidbodyState(rb);
        }
        /// <summary>
        /// Sets a state to a rigidbody.
        /// </summary>
        public static void SetState(this Rigidbody rb, RigidbodyState state)
        {
            Transform t = rb.transform;
            t.position = state.Position;
            t.rotation = state.Rotation;
            rb.velocity = state.Velocity;
            rb.angularVelocity = state.AngularVelocity;
        }

        /// <summary>
        /// Gets a Rigidbody2DState.
        /// </summary>
        public static Rigidbody2DState GetState(this Rigidbody2D rb)
        {
            return new Rigidbody2DState(rb);
        }
        /// <summary>
        /// Sets a state to a rigidbody.
        /// </summary>
        public static void SetState(this Rigidbody2D rb, Rigidbody2DState state)
        {
            Transform t = rb.transform;
            t.position = state.Position;
            t.rotation = state.Rotation;
            rb.velocity = state.Velocity;
            rb.angularVelocity = state.AngularVelocity;
        }

    }
}
#else
using FishNet.Component.Prediction;
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

        public RigidbodyState(Rigidbody rb, bool isKinematic, uint tick) : this(rb, tick)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            IsKinematic = isKinematic;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;
            LocalTick = tick;

        }

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
        public Vector2 Velocity;
        public float AngularVelocity;
        public bool Simulated;
        public bool IsKinematic;

        public Rigidbody2DState(Rigidbody2D rb, bool simulated, uint tick)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;
            Simulated = simulated;
            IsKinematic = rb.isKinematic;
            LocalTick = tick;
        }

        public Rigidbody2DState(Rigidbody2D rb, uint tick)
        {
            Position = rb.transform.position;
            Rotation = rb.transform.rotation;
            Velocity = rb.velocity;
            AngularVelocity = rb.angularVelocity;
            Simulated = rb.simulated;
            IsKinematic = rb.isKinematic;
            LocalTick = tick;
        }
    }

    public static class RigidbodyStateSerializers
    {

        public static void WriteRigidbodyState(this Writer writer, RigidbodyState value)
        {
            writer.WriteTickUnpacked(value.LocalTick);
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
                LocalTick = reader.ReadTickUnpacked(),
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
            writer.WriteTickUnpacked(value.LocalTick);
            writer.WriteVector3(value.Position);
            writer.WriteQuaternion(value.Rotation);
            writer.WriteBoolean(value.Simulated);
            writer.WriteBoolean(value.IsKinematic);

            if (value.Simulated)
            {
                writer.WriteVector2(value.Velocity);
                writer.WriteSingle(value.AngularVelocity);
            }
        }

        public static Rigidbody2DState ReadRigidbody2DState(this Reader reader)
        {
            Rigidbody2DState state = new Rigidbody2DState()
            {
                LocalTick = reader.ReadTickUnpacked(),
                Position = reader.ReadVector3(),
                Rotation = reader.ReadQuaternion(),
                Simulated = reader.ReadBoolean(),
                IsKinematic = reader.ReadBoolean()
            };

            if (state.Simulated)
            {
                state.Velocity = reader.ReadVector2();
                state.AngularVelocity = reader.ReadSingle();
            }

            return state;
        }


    }


}
#endif