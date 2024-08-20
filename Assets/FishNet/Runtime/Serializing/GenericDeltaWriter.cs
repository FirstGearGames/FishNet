using FishNet.Documenting;
using FishNet.Serializing;
using FishNet.Utility;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.GENERATED_ASSEMBLY_NAME)]
namespace FishNet.Serializing
{

    /// <summary>
    /// Used to write generic types.
    /// </summary>
    [APIExclude]
    public static class GenericDeltaWriter<T>
    {
        public static Func<Writer, T, T, DeltaSerializerOption, bool> Write { get; private set; }
        /// <summary>
        /// True if this type has a custom writer.
        /// </summary>
        private static bool _hasCustomSerializer;

        public static void SetWrite(Func<Writer, T, T, DeltaSerializerOption, bool> value)
        {
            /* If a custom serializer has already been set then exit method
             * to not overwrite serializer. */
            if (_hasCustomSerializer)
                return;

            //Set has custom serializer if value being used is not a generated method.
            _hasCustomSerializer = !(value.Method.Name.StartsWith(UtilityConstants.GeneratedWriterPrefix));
            Write = value;
        }
    }

}
