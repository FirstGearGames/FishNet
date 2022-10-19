using FishNet.Documenting;
using UnityEngine;

namespace FishNet.Utility.Extension
{
    [APIExclude]
    public static class TransformFN
    {
        /// <summary>
        /// Sets the offset values of target from a transform.
        /// </summary>
        /// <param name="pos">Position offset result.</param>
        /// <param name="rot">Rotation offset result.</param>
        public static void SetTransformOffsets(this Transform t, Transform target, ref Vector3 pos, ref Quaternion rot)
        {
            if (target == null)
                return;
            pos = (t.position - target.position);
            rot = (t.rotation * Quaternion.Inverse(target.rotation));
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