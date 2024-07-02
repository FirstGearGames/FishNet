using FishNet.Documenting;

namespace FishNet.Managing.Object
{
    [System.Flags]
    internal enum SpawnType : byte
    {
        Unset = 0,
        /// <summary>
        /// Is nested.
        /// </summary>
        Nested = 1,
        /// <summary>
        /// Is a scene object.
        /// </summary>
        Scene = 2,
        /// <summary>
        /// Instantiate into active scene.
        /// </summary>
        Instantiated = 4,
        /// <summary>
        /// Instantiate into the global scene.
        /// </summary>
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
        public static bool FastContains(SpawnType whole, SpawnType part)
        {
            return (whole & part) == part;
        }
    }



}