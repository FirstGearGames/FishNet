namespace FishNet.Object
{
    [System.Flags]
    public enum TransformPropertiesFlag : byte
    {
        Unset = 0,
        Position = 1,
        Rotation = 2,
        LocalScale = 4,
        Everything = ~(-1 << 8),
    }

    public static class TransformPropertiesOptionExtensions
    {
        /// <summary>
        /// Returns if enum contains a value.
        /// </summary>
        /// <param name="whole">Value checked against.</param>
        /// <param name="part">Value checked if whole contains.</param>
        /// <returns></returns>
        public static bool FastContains(this TransformPropertiesFlag whole, TransformPropertiesFlag part) => (whole & part) == part;        
    }
}

