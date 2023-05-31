using FishNet.Documenting;

namespace FishNet.Managing.Object
{
    [System.Flags]
    internal enum SpawnType : byte
    {
        Unset = 0,
        Nested = 1,
        Scene = 2,
        Instantiated = 4,
        InstantiatedGlobal = 8,
    }

    [APIExclude]
    internal static partial class SpawnTypeEnum
    {
        /// <summary>
        /// Returns if whole contains part.
        /// </summary>
        /// <param name="whole"></param>
        /// <param name="part"></param>
        /// <returns></returns>
        public static bool Contains(SpawnType whole, SpawnType part)
        {
            return (whole & part) == part;
        }
    }



}