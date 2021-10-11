namespace FishNet.Serializing
{
    /// <summary>
    /// How to pack data when using serialization.
    /// </summary>
    public enum AutoPackType
    {
        /// <summary>
        /// Data will not be packed. Can be used to save a small amount of bandwidth if it's known the value is too large to be packed.
        /// </summary>
        Unpacked = 0,
        /// <summary>
        /// Data will be packed.
        /// </summary>
        Packed = 1
    }
}
