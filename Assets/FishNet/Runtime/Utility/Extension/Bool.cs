
namespace FishNet.Utility.Extension
{
    public static class BooleanExtensions
    {
        /// <summary>
        /// Converts a boolean to an integer.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public static int ToInt(this bool b)
        {
            return (b) ? 1 : 0;
        }

    }

}