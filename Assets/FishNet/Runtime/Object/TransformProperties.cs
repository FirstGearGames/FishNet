using GameKit.Utilities;
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
    }
}

