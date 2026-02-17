using System;
using GameKit.Dependencies.Utilities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace FishNet.Object
{
    public static class TransformPropertiesExtensions
    {
        /// <summary>
        /// Creates direction between two TransformProperties.
        /// </summary>
        /// <param name = "divisor">Value to divide results by.</param>
        /// <returns></returns>
        public static TransformProperties CreateDirections(this TransformProperties prevProperties, TransformProperties nextProperties, uint divisor = 1)
        {
            float3 position = (nextProperties.Position - prevProperties.Position) / divisor;

            quaternion rotation = nextProperties.Rotation.Subtract(prevProperties.Rotation);
            //If more than 1 tick span then get a portion of the rotation.
            if (divisor > 1)
            {
                float percent = 1f / (float)divisor;
                rotation = math.nlerp(quaternion.identity, nextProperties.Rotation, percent);
            }

            float3 scale = (nextProperties.Scale - prevProperties.Scale) / divisor;

            return new(position, rotation, scale);
        }

        /// <summary>
        /// Sets values of TransformPropertiesCls to a transforms world properties.
        /// </summary>
        public static void SetWorldProperties(this TransformPropertiesCls tp, Transform t)
        {
            tp.Position = t.position;
            tp.Rotation = t.rotation;
            tp.LocalScale = t.localScale;
        }

        /// <summary>
        /// Sets values of TransformPropertiesCls to a transforms world properties.
        /// </summary>
        public static void SetWorldProperties(this TransformProperties tp, Transform t)
        {
            tp.Position = t.position;
            tp.Rotation = t.rotation;
            tp.Scale = t.localScale;
        }
    }

    [Serializable]
    public class TransformPropertiesCls : IResettable
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 LocalScale;
        public TransformPropertiesCls() { }

        public TransformPropertiesCls(float3 position, quaternion rotation, float3 localScale)
        {
            Position = position;
            Rotation = rotation;
            LocalScale = localScale;
        }

        public void InitializeState() { }

        public void ResetState()
        {
            Update(float3.zero, quaternion.identity, float3.zero);
        }

        public void Update(Transform t)
        {
            Update(t.position, t.rotation, t.localScale);
        }

        public void Update(TransformPropertiesCls tp)
        {
            Update(tp.Position, tp.Rotation, tp.LocalScale);
        }

        public void Update(TransformProperties tp)
        {
            Update(tp.Position, tp.Rotation, tp.Scale);
        }

        public void Update(float3 position, quaternion rotation)
        {
            Update(position, rotation, LocalScale);
        }

        public void Update(float3 position, quaternion rotation, float3 localScale)
        {
            Position = position;
            Rotation = rotation;
            LocalScale = localScale;
        }

        /// <summary>
        /// Returns if this TransformProperties equals anothers values.
        /// </summary>
        public bool ValuesEquals(TransformPropertiesCls properties)
        {
            return Position.Equals(properties.Position) && Rotation.Equals(properties.Rotation) && LocalScale.Equals(properties.LocalScale);
        }

        /// <summary>
        /// Returns this classes values as the struct version of TransformProperties.
        /// </summary>
        /// <returns></returns>
        public TransformProperties ToStruct()
        {
            TransformProperties result = new(Position, Rotation, LocalScale);
            return result;
        }
    }

    [Serializable]
    public struct TransformProperties
    {
        public float3 Position;
        public quaternion Rotation;
        [Obsolete("Use Scale.")] //Remove V5
        public float3 LocalScale => Scale;
        public float3 Scale;
        public byte IsValidByte;

        /// <summary>
        /// Becomes true when values are set through update or constructor.
        /// </summary>
        public bool IsValid
        {
            get => IsValidByte != 0;
            set => IsValidByte = (byte)(value ? 1 : 0);
        }

        public TransformProperties(float3 position, quaternion rotation, float3 localScale)
        {
            Position = position;
            Rotation = rotation;
            Scale = localScale;
            IsValidByte = 1;
        }

        /// <summary>
        /// Creates a TransformProperties with default position and rotation, with float3.one scale.
        /// </summary>
        public static TransformProperties GetTransformDefault() => new(float3.zero, quaternion.identity, new float3(1f, 1f, 1f));
        public static TransformProperties GetOffsetDefault() => new(float3.zero, quaternion.identity, float3.zero);

        public static TransformProperties operator +(TransformProperties a, TransformProperties b)
        {
            if (!a.IsValid) return b;
            if (!b.IsValid) return a;
            return new TransformProperties(
                a.Position + b.Position,
                math.mul(a.Rotation, b.Rotation),
                a.Scale * b.Scale);
        }
        
        public static TransformProperties operator -(TransformProperties a, TransformProperties b)
        {
            if (!a.IsValid) return -b;
            if (!b.IsValid) return a;
            return new TransformProperties(
                a.Position - b.Position,
                math.mul(a.Rotation, math.inverse(b.Rotation)),
                a.Scale / b.Scale);
        }
        
        public static TransformProperties operator -(TransformProperties a)
        {
            return new TransformProperties(
                -a.Position,
                math.inverse(a.Rotation),
                1f / a.Scale);
        }
        
        public override string ToString()
        {
            return $"Position: {Position.ToString()}, Rotation {Rotation.ToString()}, Scale {Scale.ToString()}";
        }

        public TransformProperties(Transform t) : this(t.position, t.rotation, t.localScale) { }

        [Obsolete("Use ResetState.")]
        public void Reset() => ResetState();

        public void ResetState()
        {
            Update(float3.zero, quaternion.identity, float3.zero);
            IsValid = false;
        }

        public void Update(Transform t)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            Update(pos, rot, t.localScale);
        }
        
        public void Update(TransformAccess t)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            Update(pos, rot, t.localScale);
        }

        public void Update(TransformProperties tp)
        {
            Update(tp.Position, tp.Rotation, tp.Scale);
        }

        public void Update(float3 position, quaternion rotation)
        {
            Update(position, rotation, Scale);
        }

        public void Update(float3 position, quaternion rotation, float3 localScale)
        {
            Position = position;
            Rotation = rotation;
            Scale = localScale;
            IsValid = true;
        }

        /// <summary>
        /// Adds another transformProperties onto this.
        /// </summary>
        /// <param name = "tp"></param>
        public void Add(TransformProperties tp)
        {
            Position += tp.Position;
            Rotation = math.mul(Rotation, tp.Rotation);
            Scale += tp.Scale;
        }

        /// <summary>
        /// Subtracts another transformProperties from this.
        /// </summary>
        /// <param name = "tp"></param>
        public void Subtract(TransformProperties tp)
        {
            Position -= tp.Position;
            Rotation = math.mul(Rotation, math.inverse(tp.Rotation));
            Scale -= tp.Scale;
        }

        /// <summary>
        /// Returns if this TransformProperties equals anothers values.
        /// </summary>
        public bool ValuesEquals(TransformProperties properties)
        {
            return Position.Equals(properties.Position) && Rotation.Equals(properties.Rotation) && Scale.Equals(properties.Scale);
        }
    }
}