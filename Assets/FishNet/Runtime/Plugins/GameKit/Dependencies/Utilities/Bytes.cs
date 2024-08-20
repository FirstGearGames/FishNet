
using System.Runtime.CompilerServices;

namespace GameKit.Dependencies.Utilities
{

    /// <summary>
    /// Various utility classes relating to floats.
    /// </summary>
    public static class Bytes
    {
        /// <summary>
        /// Pads an index a specified value. Preferred over typical padding so that pad values used with skins can be easily found in the code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Pad(this byte value, int padding) => Ints.PadInt(value, padding);
        /// <summary>
        /// Provides a random inclusive int within a given range. Preferred over Unity's Random to eliminate confusion as Unity uses inclusive for floats max, and exclusive for int max. 
        /// </summary>
        /// <param name="minimum">Inclusive minimum value.</param>
        /// <param name="maximum">Inclusive maximum value.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte RandomInclusiveRange(byte minimum, byte maximum) => (byte)Ints.RandomInclusiveRange(minimum, maximum);
        /// <summary>
        /// Provides a random exclusive int within a given range. Preferred over Unity's Random to eliminate confusion as Unity uses inclusive for floats max, and exclusive for int max. 
        /// </summary>
        /// <param name="minimum">Inclusive minimum value.</param>
        /// <param name="maximum">Exclusive maximum value.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte RandomExclusiveRange(byte minimum, byte maximum) => (byte)Ints.RandomExclusiveRange(minimum, maximum);

        /// <summary>
        /// Returns a clamped int within a specified range.
        /// </summary>
        /// <param name="value">Value to clamp.</param>
        /// <param name="minimum">Minimum value.</param>
        /// <param name="maximum">Maximum value.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Clamp(byte value, byte minimum, byte maximum) => (byte)Ints.Clamp(value, minimum, maximum);

        /// <summary>
        /// Returns whichever value is lower.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Min(byte a, byte b) => (a < b) ? a : b;

        /// <summary>
        /// Determins if all values passed in are the same.
        /// </summary>
        /// <param name="values">Values to check.</param>
        /// <returns>True if all values are the same.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValuesMatch(params byte[] values) => Ints.ValuesMatch((int[])(object)values);

    }
}