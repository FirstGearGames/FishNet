using GameKit.Dependencies.Utilities;
using System.Runtime.CompilerServices;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetState()
        {
            Update(Vector3.zero, Quaternion.identity, Vector3.zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Transform t)
        {
            Update(t.position, t.rotation, t.localScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(TransformPropertiesCls tp)
        {
            Update(tp.Position, tp.Rotation, tp.LocalScale);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(TransformProperties tp)
        {
            Update(tp.Position, tp.Rotation, tp.LocalScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            return (this.Position == properties.Position
                && this.Rotation == properties.Rotation
                && this.LocalScale == properties.LocalScale);
        }

        /// <summary>
        /// Returns this classes values as the struct version of TransformProperties.
        /// </summary>
        /// <returns></returns>
        public TransformProperties ToStruct()
        {
            TransformProperties result = new TransformProperties(Position, Rotation, LocalScale);
            return result;
        }
    }

    [System.Serializable]
    public struct TransformProperties
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LocalScale;

        public TransformProperties(Vector3 position, Quaternion rotation, Vector3 localScale)
        {
            Position = position;
            Rotation = rotation;
            LocalScale = localScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            Update(Vector3.zero, Quaternion.identity, Vector3.zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Transform t)
        {
            Update(t.position, t.rotation, t.localScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(TransformProperties tp)
        {
            Update(tp.Position, tp.Rotation, tp.LocalScale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// Adds another transformProperties onto this.
        /// </summary>
        /// <param name="tp"></param>
        public void Add(TransformProperties tp)
        {
            Position += tp.Position;
            Rotation *= tp.Rotation;
            LocalScale += tp.LocalScale;
        }

        /// <summary>
        /// Subtracts another transformProperties from this.
        /// </summary>
        /// <param name="tp"></param>
        public void Subtract(TransformProperties tp)
        {
            Position -= tp.Position;
            Rotation *= Quaternion.Inverse(tp.Rotation);
            LocalScale -= tp.LocalScale;
        }

        /// <summary>
        /// Returns if this TransformProperties equals anothers values.
        /// </summary>
        public bool ValuesEquals(TransformProperties properties)
        {
            return (this.Position == properties.Position
                && this.Rotation == properties.Rotation
                && this.LocalScale == properties.LocalScale);
        }
    }
}

