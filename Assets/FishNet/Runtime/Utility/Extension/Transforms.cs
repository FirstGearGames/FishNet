using System;
using FishNet.Documenting;
using FishNet.Object;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Jobs;

namespace FishNet.Utility.Extension
{
    [APIExclude]
    public static class TransformFN
    {
        /// <summary>
        /// Gets correct values of Vector3 pos and Quaternion rot
        /// </summary>
        public static void GetCorrectLocalPositionAndRotation(this TransformAccess t, out Vector3 pos, out Quaternion rot)
        {
            // https://issuetracker.unity3d.com/issues/wrong-position-and-rotation-values-are-returned-when-using-transformaccess-dot-getlocalpositionandrotation
            pos = t.localPosition;
            rot = t.localRotation;
        }
        
        /// <summary>
        /// Sets correct values of Vector3 pos and Quaternion rot
        /// </summary>
        public static void SetCorrectLocalPositionAndRotation(this TransformAccess t, Vector3 pos, Quaternion rot)
        {
            // https://issuetracker.unity3d.com/issues/wrong-position-and-rotation-values-are-returned-when-using-transformaccess-dot-getlocalpositionandrotation
            t.localPosition = pos;
            t.localRotation = rot;
        }
        
        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformProperties GetWorldProperties(this Transform t)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            TransformProperties tp = new(pos, rot, t.localScale);
            return tp;
        }
        
        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformProperties GetWorldProperties(this TransformAccess t)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            TransformProperties tp = new(pos, rot, t.localScale);
            return tp;
        }

        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformProperties GetWorldProperties(this Transform t, TransformProperties offset)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            TransformProperties tp = new(pos, rot, t.localScale);
            tp.Add(offset);
            return tp;
        }
        
        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformProperties GetWorldProperties(this TransformAccess t, TransformProperties offset)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            TransformProperties tp = new(pos, rot, t.localScale);
            tp.Add(offset);
            return tp;
        }

        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformPropertiesCls GetWorldPropertiesCls(this Transform t)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            TransformPropertiesCls tp = new(pos, rot, t.localScale);
            return tp;
        }
        
        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformPropertiesCls GetWorldPropertiesCls(this TransformAccess t)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            TransformPropertiesCls tp = new(pos, rot, t.localScale);
            return tp;
        }

        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformProperties GetLocalProperties(this Transform t)
        {
            t.GetLocalPositionAndRotation(out var pos, out var rot);
            TransformProperties tp = new(pos, rot, t.localScale);
            return tp;
        }
        
        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformProperties GetLocalProperties(this TransformAccess t)
        {
            t.GetCorrectLocalPositionAndRotation(out var pos, out var rot);
            TransformProperties tp = new(pos, rot, t.localScale);
            return tp;
        }

        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformPropertiesCls GetLocalPropertiesCls(this Transform t)
        {
            t.GetLocalPositionAndRotation(out var pos, out var rot);
            TransformPropertiesCls tp = new(pos, rot, t.localScale);
            return tp;
        }
        
        /// <summary>
        /// Gets values of TransformProperties from the transforms world properties.
        /// </summary>
        public static TransformPropertiesCls GetLocalPropertiesCls(this TransformAccess t)
        {
            t.GetCorrectLocalPositionAndRotation(out var pos, out var rot);
            TransformPropertiesCls tp = new(pos, rot, t.localScale);
            return tp;
        }

        /// <summary>
        /// Sets values of TransformPropertiesCls to a transforms world properties.
        /// </summary>
        [Obsolete("Use TransformPropertiesExtensions.SetWorldProperties.")]
        public static void SetWorldProperties(this TransformPropertiesCls tp, Transform t) => TransformPropertiesExtensions.SetWorldProperties(tp, t);

        /// <summary>
        /// Gets the offset values by subtracting this from target.
        /// </summary>
        /// <param name = "pos">Position offset result.</param>
        /// <param name = "rot">Rotation offset result.</param>
        public static void SetTransformOffsets(this Transform t, Transform target, ref Vector3 pos, ref Quaternion rot)
        {
            if (target == null)
                return;
            t.GetPositionAndRotation(out var tPos, out var tRot);
            target.GetPositionAndRotation(out var targetPos, out var targetRot);
            pos = targetPos - tPos;
            rot = targetRot * Quaternion.Inverse(tRot);
        }

        /// <summary>
        /// Gets the offset values by subtracting this from target.
        /// </summary>
        /// <param name = "zeroScale">True to set scale to Vector3.zero.</param>
        public static TransformProperties GetTransformOffsets(this Transform t, Transform target)
        {
            if (target == null)
                return default;

            t.GetPositionAndRotation(out var tPos, out var tRot);
            target.GetPositionAndRotation(out var targetPos, out var targetRot);
            return new(targetPos - tPos, targetRot * Quaternion.Inverse(tRot), target.localScale - t.localScale);
        }

        /// <summary>
        /// Sets a transform to local properties.
        /// </summary>
        public static void SetLocalProperties(this Transform t, TransformPropertiesCls tp)
        {
            t.SetLocalPositionAndRotation(tp.Position, tp.Rotation);
            t.localScale = tp.LocalScale;
        }
        
        /// <summary>
        /// Sets a transform to local properties.
        /// </summary>
        public static void SetLocalProperties(this TransformAccess t, TransformPropertiesCls tp)
        {
            t.SetCorrectLocalPositionAndRotation(tp.Position, tp.Rotation);
            t.localScale = tp.LocalScale;
        }

        /// <summary>
        /// Sets a transform to local properties.
        /// </summary>
        public static void SetLocalProperties(this Transform t, TransformProperties tp)
        {
            t.SetLocalPositionAndRotation(tp.Position, tp.Rotation);
            t.localScale = tp.Scale;
        }
        
        /// <summary>
        /// Sets a transform to local properties.
        /// </summary>
        public static void SetLocalProperties(this TransformAccess t, TransformProperties tp)
        {
            t.SetCorrectLocalPositionAndRotation(tp.Position, tp.Rotation);
            t.localScale = tp.Scale;
        }

        /// <summary>
        /// Sets a transform to world properties.
        /// </summary>
        public static void SetWorldProperties(this Transform t, TransformPropertiesCls tp)
        {
            t.SetPositionAndRotation(tp.Position, tp.Rotation);
            t.localScale = tp.LocalScale;
        }
        
        /// <summary>
        /// Sets a transform to world properties.
        /// </summary>
        public static void SetWorldProperties(this TransformAccess t, TransformPropertiesCls tp)
        {
            t.SetPositionAndRotation(tp.Position, tp.Rotation);
            t.localScale = tp.LocalScale;
        }

        /// <summary>
        /// Sets a transform to world properties.
        /// </summary>
        public static void SetWorldProperties(this Transform t, TransformProperties tp)
        {
            t.SetPositionAndRotation(tp.Position, tp.Rotation);
            t.localScale = tp.Scale;
        }
        
        /// <summary>
        /// Sets a transform to world properties.
        /// </summary>
        public static void SetWorldProperties(this TransformAccess t, TransformProperties tp)
        {
            t.SetPositionAndRotation(tp.Position, tp.Rotation);
            t.localScale = tp.Scale;
        }

        /// <summary>
        /// Sets local position and rotation for a transform.
        /// </summary>
        public static void SetLocalPositionAndRotation(this Transform t, Vector3 pos, Quaternion rot)
        {
            t.SetLocalPositionAndRotation(pos, rot);
        }
        
        /// <summary>
        /// Sets local position and rotation for a transform.
        /// </summary>
        public static void SetLocalPositionAndRotation(this TransformAccess t, Vector3 pos, Quaternion rot)
        {
            t.SetCorrectLocalPositionAndRotation(pos, rot);
        }

        /// <summary>
        /// Sets local position, rotation, and scale for a transform.
        /// </summary>
        public static void SetLocalPositionRotationAndScale(this Transform t, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            t.SetLocalPositionAndRotation(pos, rot);
            t.localScale = scale;
        }
        
        /// <summary>
        /// Sets local position, rotation, and scale for a transform.
        /// </summary>
        public static void SetLocalPositionRotationAndScale(this TransformAccess t, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            t.SetCorrectLocalPositionAndRotation(pos, rot);
            t.localScale = scale;
        }

        /// <summary>
        /// Sets local position, rotation, and scale using nullables for a transform. If a value is null then that property is skipped.
        /// </summary>
        public static void SetLocalPositionRotationAndScale(this Transform t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale)
        {
            if (nullablePos.HasValue)
            {
                if (nullableRot.HasValue)
                    t.SetLocalPositionAndRotation(nullablePos.Value, nullableRot.Value);
                else t.localPosition = nullablePos.Value;
            }
            else if (nullableRot.HasValue)
                t.localRotation = nullableRot.Value;
            if (nullableScale.HasValue)
                t.localScale = nullableScale.Value;
        }
        
        /// <summary>
        /// Sets local position, rotation, and scale using nullables for a transform. If a value is null then that property is skipped.
        /// </summary>
        public static void SetLocalPositionRotationAndScale(this TransformAccess t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale)
        {
            if (nullablePos.HasValue)
            {
                if (nullableRot.HasValue)
                    t.SetCorrectLocalPositionAndRotation(nullablePos.Value, nullableRot.Value);
                else t.localPosition = nullablePos.Value;
            }
            else if (nullableRot.HasValue)
                t.localRotation = nullableRot.Value;
            if (nullableScale.HasValue)
                t.localScale = nullableScale.Value;
        }

        /// <summary>
        /// Sets world position, rotation, and scale using nullables for a transform. If a value is null then that property is skipped.
        /// </summary>
        public static void SetWorldPositionRotationAndScale(this Transform t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale)
        {
            if (nullablePos.HasValue)
            {
                if (nullableRot.HasValue)
                    t.SetPositionAndRotation(nullablePos.Value, nullableRot.Value);
                else t.position = nullablePos.Value;
            }
            else if (nullableRot.HasValue)
                t.rotation = nullableRot.Value;
            if (nullableScale.HasValue)
                t.localScale = nullableScale.Value;
        }
        
        /// <summary>
        /// Sets world position, rotation, and scale using nullables for a transform. If a value is null then that property is skipped.
        /// </summary>
        public static void SetWorldPositionRotationAndScale(this TransformAccess t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale)
        {
            if (nullablePos.HasValue)
            {
                if (nullableRot.HasValue)
                    t.SetPositionAndRotation(nullablePos.Value, nullableRot.Value);
                else t.position = nullablePos.Value;
            }
            else if (nullableRot.HasValue)
                t.rotation = nullableRot.Value;
            if (nullableScale.HasValue)
                t.localScale = nullableScale.Value;
        }

        /// <summary>
        /// Oututs properties to use for a transform. When a nullable property has value that value is used, otherwise the transforms current property is used.
        /// </summary>
        public static void OutLocalPropertyValues(this Transform t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            if (!nullablePos.HasValue)
            {
                if (!nullableRot.HasValue)
                    t.GetLocalPositionAndRotation(out pos, out rot);
                else
                {
                    pos = t.localPosition;
                    rot = nullableRot.Value;
                }
            }
            else if (!nullableRot.HasValue)
            {
                pos = nullablePos.Value;
                rot = t.localRotation;
            }
            else
            {
                pos = nullablePos.Value;
                rot = nullableRot.Value;
            }
            
            scale = nullableScale == null ? t.localScale : nullableScale.Value;
        }
        
        /// <summary>
        /// Oututs properties to use for a transform. When a nullable property has value that value is used, otherwise the transforms current property is used.
        /// </summary>
        public static void OutLocalPropertyValues(this TransformAccess t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            if (!nullablePos.HasValue)
            {
                if (!nullableRot.HasValue)
                    t.GetCorrectLocalPositionAndRotation(out pos, out rot);
                else
                {
                    pos = t.localPosition;
                    rot = nullableRot.Value;
                }
            }
            else if (!nullableRot.HasValue)
            {
                pos = nullablePos.Value;
                rot = t.localRotation;
            }
            else
            {
                pos = nullablePos.Value;
                rot = nullableRot.Value;
            }
            
            scale = nullableScale == null ? t.localScale : nullableScale.Value;
        }

        /// <summary>
        /// Oututs properties to use for a transform. When a nullable property has value that value is used, otherwise the transforms current property is used.
        /// </summary>
        public static void OutWorldPropertyValues(this Transform t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            if (!nullablePos.HasValue)
            {
                if (!nullableRot.HasValue)
                    t.GetPositionAndRotation(out pos, out rot);
                else
                {
                    pos = t.position;
                    rot = nullableRot.Value;
                }
            }
            else if (!nullableRot.HasValue)
            {
                pos = nullablePos.Value;
                rot = t.rotation;
            }
            else
            {
                pos = nullablePos.Value;
                rot = nullableRot.Value;
            }

            scale = nullableScale == null ? t.localScale : nullableScale.Value;
        }
        
        /// <summary>
        /// Oututs properties to use for a transform. When a nullable property has value that value is used, otherwise the transforms current property is used.
        /// </summary>
        public static void OutWorldPropertyValues(this TransformAccess t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale, out Vector3 pos, out Quaternion rot, out Vector3 scale)
        {
            if (!nullablePos.HasValue)
            {
                if (!nullableRot.HasValue)
                    t.GetPositionAndRotation(out pos, out rot);
                else
                {
                    pos = t.position;
                    rot = nullableRot.Value;
                }
            }
            else if (!nullableRot.HasValue)
            {
                pos = nullablePos.Value;
                rot = t.rotation;
            }
            else
            {
                pos = nullablePos.Value;
                rot = nullableRot.Value;
            }

            scale = nullableScale == null ? t.localScale : nullableScale.Value;
        }
    }
}