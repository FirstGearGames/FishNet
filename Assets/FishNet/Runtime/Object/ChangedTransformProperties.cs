using FishNet.Documenting;

namespace FishNet.Object
{
    /// <summary>
    /// Properties which have changed on a transform.
    /// </summary>
    [System.Flags]
    [APIExclude]
    internal enum ChangedTransformProperties : byte
    {
        Unset = 0,
        Position = 2,
        Rotation = 4,
        LocalScale = 8
    }

    [APIExclude]
    internal static partial class Enums
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

