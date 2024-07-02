using FishNet.Documenting;
using FishNet.Utility;
using System;
using System.Runtime.CompilerServices;


[assembly: InternalsVisibleTo(UtilityConstants.GENERATED_ASSEMBLY_NAME)]
//Required for internal tests.
[assembly: InternalsVisibleTo(UtilityConstants.TEST_ASSEMBLY_NAME)]
namespace FishNet.Serializing
{
    /// <summary>
    /// Used for read references to generic types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [APIExclude]
    public static class GenericReader<T>
    {
        public static Func<Reader, T> Read { get; set; }        
        /// <summary>
        /// True if this type has a custom writer.
        /// </summary>
        private static bool _hasCustomSerializer;

        public static void SetRead(Func<Reader, T> value)
        {
            /* If a custom serializer has already been set then exit method
             * to not overwrite serializer. */
            if (_hasCustomSerializer)
                return;

            //Set has custom serializer if value being used is not a generated method.
            _hasCustomSerializer = !(value.Method.Name.StartsWith(UtilityConstants.GENERATED_READER_PREFIX));
            Read = value;
        }

    }
}
