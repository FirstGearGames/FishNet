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
        LocalPosition = 1,
        LocalRotation = 2,
        LocalScale = 4,
    }

    [APIExclude]
    internal static partial class ChangedTransformPropertiesEnum
    {
        /// <summary>
        /// Returns if whole contains part.
        /// </summary>
        /// <param name="whole"></param>
        /// <param name="part"></param>
        /// <returns></returns>
        public static bool Contains(ChangedTransformProperties whole, ChangedTransformProperties part)
        {
            return (whole & part) == part;
        }
    }


}

