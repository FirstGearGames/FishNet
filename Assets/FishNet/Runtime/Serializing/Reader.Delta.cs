using FishNet.CodeGenerating;
using FishNet.Managing;
using FishNet.Utility;
using System.Runtime.CompilerServices;
using UnityEngine;
using static FishNet.Serializing.Writer;

/* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
/* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */
/* THIS IS IN DRAFTING / WIP. Do not attempt to use or modify this file. */

[assembly: InternalsVisibleTo(UtilityConstants.GENERATED_ASSEMBLY_NAME)]
//Required for internal tests.
[assembly: InternalsVisibleTo(UtilityConstants.TEST_ASSEMBLY_NAME)]
namespace FishNet.Serializing
{

    /// <summary>
    /// Reads data from a buffer.
    /// </summary>
    public partial class Reader
    {
        //[NotSerializer]
        //internal ushort ReadUInt16Delta(ushort prev)
        //{
        //    int next = ReadInt32();
        //    return (ushort)(prev + next);
        //}
        //[NotSerializer]
        //internal short ReadInt16Delta(short prev) => (short)ReadUInt16Delta((ushort)prev);

        //[NotSerializer]
        //internal uint ReadUInt32Delta(uint prev)
        //{
        //    long next = ReadInt64();
        //    return (prev + (uint)next);
        //}

        //[NotSerializer]
        //internal int ReadInt32Delta(int prev) => (int)ReadUInt32Delta((uint)prev);


        //[NotSerializer]
        //internal ulong ReadUInt64Delta(ulong prev)
        //{
        //    bool newLarger = ReadBoolean();
        //    ulong difference = ReadPackedWhole();

        //    return (newLarger) ?
        //        (prev + difference) : (prev - difference);
        //}
        //[NotSerializer]
        //internal long ReadInt64Delta(long prev) => (long)ReadUInt64Delta((ulong)prev);

        //[NotSerializer]
        //internal LayerMask ReadLayerMaskDelta(LayerMask prev)
        //{
        //    int layerValue = ReadInt32Delta(prev);
        //    return (LayerMask)layerValue;
        //}

        [NotSerializer]
        internal float ReadSingleDelta(float prev)
        {
            AutoPackTypeSigned apts = (AutoPackTypeSigned)ReadByte();
            /* If numeric value is equal to PackedPositive or higher than
             * the pack type will be positive, as all positive types
             * are larger than negative in the enum. */
            float multiplier = ((byte)apts >= (byte)AutoPackTypeSigned.PackedPositive) ?
                1f : -1f;

            switch (apts)
            {
                case AutoPackTypeSigned.Unpacked:
                    return ReadSingle(AutoPackType.Unpacked);
                case AutoPackTypeSigned.PackedPositive:
                case AutoPackTypeSigned.PackedNegative:
                    return (prev + GetUnpackedValue(ReadByte()));
                case AutoPackTypeSigned.PackedLessPositive:
                case AutoPackTypeSigned.PackedLessNegative:
                    return (prev + GetUnpackedValue(ReadUInt16()));
            }

            //Fallthrough.
            NetworkManager.LogError("Unhandled ReadSingleDelta packType of {apts}.");
            return default;

            float GetUnpackedValue(ushort readValue)
            {
                return (readValue / Writer.ACCURACY) * multiplier;
            }
        }


    }
}
