using System;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Object
{
    [System.Serializable]
    public class TransformPropertiesCls : IResettable
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LocalScale;

        public TransformPropertiesCls() { }

        public TransformPropertiesCls(Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Position = position;
            Rotation = rotation;
            LocalScale = localScale;
        }

        public void InitializeState() { }

        public void ResetState()
        {
            Update(Vector3.zero, Quaternion.identity, Vector3.zero);
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

        public void Update(Vector3 position, Quaternion rotation)
        {
            Update(position, rotation, LocalScale);
        }

        public void Update(Vector3 position, Quaternion rotation, Vector3 localScale)
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
            return (this.Position == properties.Position && this.Rotation == properties.Rotation && this.LocalScale == properties.LocalScale);
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

    [System.Serializable]
    public struct TransformProperties
    {
        public Vector3 Position;
        public Quaternion Rotation;
        [Obsolete("Use Scale.")]
        public Vector3 LocalScale => Scale;
        public Vector3 Scale;
        /// <summary>
        /// Becomes true when values are set through update or constructor.
        /// </summary>
        public bool IsValid;

        public TransformProperties(Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Position = position;
            Rotation = rotation;
            Scale = localScale;
            IsValid = true;
        }

        public TransformProperties(Transform t) : this(t.position, t.rotation, t.localScale) { }

        [Obsolete("Use ResetState.")]
        public void Reset() => ResetState();

        public void ResetState()
        {
            Update(Vector3.zero, Quaternion.identity, Vector3.zero);
            IsValid = false;
        }

        public void Update(Transform t)
        {
            Update(t.position, t.rotation, t.localScale);
        }

        public void Update(TransformProperties tp)
        {
            Update(tp.Position, tp.Rotation, tp.Scale);
        }

        public void Update(Vector3 position, Quaternion rotation)
        {
            Update(position, rotation, Scale);
        }

        public void Update(Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Position = position;
            Rotation = rotation;
            Scale = localScale;
            IsValid = true;
        }

        /// <summary>
        /// Adds another transformProperties onto this.
        /// </summary>
        /// <param name="tp"></param>
        public void Add(TransformProperties tp)
        {
            Position += tp.Position;
            Rotation *= tp.Rotation;
            Scale += tp.Scale;
        }

        /// <summary>
        /// Subtracts another transformProperties from this.
        /// </summary>
        /// <param name="tp"></param>
        public void Subtract(TransformProperties tp)
        {
            Position -= tp.Position;
            Rotation *= Quaternion.Inverse(tp.Rotation);
            Scale -= tp.Scale;
        }

        /// <summary>
        /// Returns if this TransformProperties equals anothers values.
        /// </summary>
        public bool ValuesEquals(TransformProperties properties)
        {
            return (this.Position == properties.Position && this.Rotation == properties.Rotation && this.Scale == properties.Scale);
        }
    }
}