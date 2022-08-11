using FishNet.Documenting;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Utility.Extension
{
    [APIExclude]
    public static class QuaterionFN
    {

        /// <summary>
        /// Returns if two quaternions match.
        /// </summary>
        /// <param name="precise">True to use a custom implementation with no error tolerance. False to use Unity's implementation which may return a match even when not true due to error tolerance.</param>
        /// <returns></returns>
        public static bool Matches(this Quaternion a, Quaternion b, bool precise = false)
        {
            if (!precise)
                return (a == b);
            else
                return (a.w == b.w && a.x == b.x && a.y == b.y && a.z == b.z);
        }

        /// <summary>
        /// Returns the angle between two quaterions.
        /// </summary>
        /// <param name="precise">True to use a custom implementation with no error tolerance. False to use Unity's implementation which may return 0f due to error tolerance, even while there is a difference.</param>
        /// <returns></returns>
        public static float Angle(this Quaternion a, Quaternion b, bool precise = false)
        {
            if (precise)
            {
                return Quaternion.Angle(a, b);
            }
            else
            {
                //This is run Unitys implementation without the error tolerance.
                float dot = (a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w);
                return (Mathf.Acos(Mathf.Min(Mathf.Abs(dot), 1f)) * 2f * 57.29578f);
            }
        }
    }

}