using UnityEngine;

namespace FishNet.Object
{
    [System.Flags]
    public enum ChangedTransformProperties : byte
    {
        Unset = 0,
        Position = 2,
        Rotation = 4,
        LocalScale = 8
    }

    public static partial class Enums
    {
        /// <summary>
        /// Returns if whole contains part.
        /// </summary>
        /// <param name="whole"></param>
        /// <param name="part"></param>
        /// <returns></returns>
        public static bool TransformPropertiesContains(ChangedTransformProperties whole, ChangedTransformProperties part)
        {
            return (whole & part) == part;
        }
    }


}

