using FishNet.Documenting;
using FishNet.Utility;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.GENERATED_ASSEMBLY_NAME)]
namespace FishNet.Serializing
{

    /// <summary>
    /// Used to read generic types.
    /// </summary>
    [APIExclude]
    public static class GenericDeltaReader<T>
    {
        public static Func<Reader, T, T> Read { get; private set; }
        /// <summary>
        /// True if this type has a custom writer.
        /// </summary>
        private static bool _hasCustomSerializer;

        public static void SetRead(Func<Reader, T, T> value)
        {
            /* If a custom serializer has already been set then exit method
             * to not overwrite serializer. */
            if (_hasCustomSerializer)
                return;

            //Set has custom serializer if value being used is not a generated method.
            _hasCustomSerializer = !(value.Method.Name.StartsWith(UtilityConstants.GeneratedWriterPrefix));
            Read = value;
        }
    }

}