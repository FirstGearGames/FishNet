using FishNet.Documenting;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Utility.Extension
{
    [APIExclude]
    public static class TransformFN
    {
        /// <summary>
        /// Sets values of TransformProperties to a transforms world properties.
        /// </summary>
        public static TransformProperties GetWorldProperties(this Transform t)
        {
            TransformProperties tp = new TransformProperties(t.position, t.rotation, t.localScale);
            return tp;
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
        /// Sets the offset values of target from a transform.
        /// </summary>
        /// <param name="pos">Position offset result.</param>
        /// <param name="rot">Rotation offset result.</param>
        public static void SetTransformOffsets(this Transform t, Transform target, ref Vector3 pos, ref Quaternion rot)
        {
            if (target == null)
                return;
            pos = (target.position - t.position);
            rot = (target.rotation * Quaternion.Inverse(t.rotation));
        }

        /// <summary>
        /// Sets the offset values of target from a transform.
        /// </summary>
        /// <param name="pos">Position offset result.</param>
        /// <param name="rot">Rotation offset result.</param>
        public static TransformProperties GetTransformOffsets(this Transform t, Transform target)
        {
            if (target == null)
                return default;

            return new TransformProperties(
                (target.position - t.position),
                (target.rotation * Quaternion.Inverse(t.rotation)),
                (target.localScale - t.localScale)
                );
        }

        /// <summary>
        /// Sets local position and rotation for a transform.
        /// </summary>
        public static void SetLocalPositionAndRotation(this Transform t, Vector3 pos, Quaternion rot)
        {
            t.localPosition = pos;
            t.localRotation = rot;
        }
        /// <summary>
        /// Sets local position, rotation, and scale for a transform.
        /// </summary>
        public static void SetLocalPositionRotationAndScale(this Transform t, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            t.localPosition = pos;
            t.localRotation = rot;
            t.localScale = scale;
        }

    }

}