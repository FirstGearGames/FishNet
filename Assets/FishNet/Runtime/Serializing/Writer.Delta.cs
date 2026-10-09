using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.CodeGenerating;
using System.Runtime.CompilerServices;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;

namespace FishNet.Serializing
{
    public partial class Writer
    {
        #region Types.
        [Flags]
        internal enum UnsignedVector3DeltaFlag : int
        {
            Unset = 0,
            More = 1 << 0,
            X1 = 1 << 1,
            NextXIsLarger = 1 << 2,
            Y1 = 1 << 3,
            NextYIsLarger = 1 << 4,
            Z1 = 1 << 5,
            NextZIsLarger = 1 << 6,
            X2 = 1 << 8,
            X4 = 1 << 9,
            Y2 = 1 << 10,
            Y4 = 1 << 11,
            Z2 = 1 << 12,
            Z4 = 1 << 13
        }
        #endregion

        /// <summary>
        /// Used to insert length for delta flags.
        /// </summary>
        private ReservedLengthWriter _reservedLengthWriter = new();
        private const double LARGEST_DELTA_PRECISION_INT8 = sbyte.MaxValue / DOUBLE_ACCURACY;
        private const double LARGEST_DELTA_PRECISION_INT16 = short.MaxValue / DOUBLE_ACCURACY;
        private const double LARGEST_DELTA_PRECISION_INT32 = int.MaxValue / DOUBLE_ACCURACY;
        private const double LARGEST_DELTA_PRECISION_INT64 = long.MaxValue / DOUBLE_ACCURACY;
        private const double LARGEST_DELTA_PRECISION_UINT8 = byte.MaxValue / DOUBLE_ACCURACY;
        private const double LARGEST_DELTA_PRECISION_UINT16 = ushort.MaxValue / DOUBLE_ACCURACY;
        private const double LARGEST_DELTA_PRECISION_UINT32 = uint.MaxValue / DOUBLE_ACCURACY;
        private const double LARGEST_DELTA_PRECISION_UINT64 = ulong.MaxValue / DOUBLE_ACCURACY;
        internal const double DOUBLE_ACCURACY = 1000d;
        internal const double DOUBLE_ACCURACY_PRECISION = 1f / DOUBLE_ACCURACY;
        internal const decimal DECIMAL_ACCURACY = 1000m;
        internal const float QUATERNION_PRECISION = 0.0001f;

        #region Other.
        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaBoolean(bool valueA, bool valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            bool valuesMatch = valueA == valueB;
            if (valuesMatch && option == DeltaSerializerOption.Unset)
                return false;

            WriteBoolean(valueB);

            return true;
        }
        #endregion

        #region Whole values.
        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaInt8(sbyte valueA, sbyte valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset) => WriteDifference8_16_32(valueA, valueB, option);

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        public bool WriteDeltaUInt8(byte valueA, byte valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset) => WriteDifference8_16_32(valueA, valueB, option);

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaInt16(short valueA, short valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset) => WriteDifference8_16_32(valueA, valueB, option);

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaUInt16(ushort valueA, ushort valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset) => WriteDifference8_16_32(valueA, valueB, option);

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaInt32(int valueA, int valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset) => WriteDifference8_16_32(valueA, valueB, option);

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaUInt32(uint valueA, uint valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset) => WriteDifference8_16_32(valueA, valueB, option);

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaInt64(long valueA, long valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset) => WriteDeltaUInt64((ulong)valueA, (ulong)valueB, option);

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaUInt64(ulong valueA, ulong valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            bool unchangedValue = valueA == valueB;
            if (unchangedValue && option == DeltaSerializerOption.Unset)
                return false;

            bool bLargerThanA = valueB > valueA;
            ulong next = bLargerThanA ? valueB - valueA : valueA - valueB;

            WriteBoolean(bLargerThanA);
            WriteUnsignedPackedWhole(next);

            return true;
        }

        /// <summary>
        /// Writes the difference between two values for signed and unsigned shorts and ints.
        /// </summary>
        private bool WriteDifference8_16_32(long valueA, long valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            bool unchangedValue = valueA == valueB;
            if (unchangedValue && option == DeltaSerializerOption.Unset)
                return false;

            long next = valueB - valueA;
            WriteSignedPackedWhole(next);

            return true;
        }
        #endregion

        #region Single.
        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteUDeltaSingle(float valueA, float valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            UDeltaPrecisionType dpt = GetUDeltaPrecisionType(valueA, valueB, out float unsignedDifference);

            if (dpt == UDeltaPrecisionType.Unset && option == DeltaSerializerOption.Unset)
                return false;

            WriteUInt8Unpacked((byte)dpt);
            WriteDeltaSingle(dpt, unsignedDifference, unsigned: true);

            return true;
        }

        /// <summary>
        /// Writes a delta value using a compression type.
        /// </summary>
        private void WriteDeltaSingle(UDeltaPrecisionType dpt, float value, bool unsigned)
        {
            /* Round rather than floor. Flooring the magnitude of a difference always moves the
             * decoded value toward the previous one, so the error does not cancel out over a
             * chain of deltas; rounding halves the worst case and removes the bias. The precision
             * type is only chosen while the scaled value is below the type's maximum, so the
             * rounded value still fits. The same applies to the double and decimal writers. */
            if (dpt.FastContains(UDeltaPrecisionType.UInt8))
            {
                if (unsigned)
                    WriteUInt8Unpacked((byte)Math.Round(value * DOUBLE_ACCURACY));
                else
                    WriteInt8Unpacked((sbyte)Math.Round(value * DOUBLE_ACCURACY));
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
            {
                if (unsigned)
                    WriteUInt16Unpacked((ushort)Math.Round(value * DOUBLE_ACCURACY));
                else
                    WriteInt16Unpacked((short)Math.Round(value * DOUBLE_ACCURACY));
            }
            // Anything else is unpacked.
            else
            {
                WriteSingleUnpacked(value);
            }
        }

        /// <summary>
        /// Returns DeltaPrecisionType for the difference of two values.
        /// Value returned should be written as signed.
        /// </summary>
        public UDeltaPrecisionType GetSDeltaPrecisionType(float valueA, float valueB, out float signedDifference)
        {
            signedDifference = valueB - valueA;
            float posValue = signedDifference < 0f ? signedDifference * -1f : signedDifference;

            return GetDeltaPrecisionType(posValue, unsigned: false);
        }

        /// <summary>
        /// Returns DeltaPrecisionType for the difference of two values.
        /// </summary>
        public UDeltaPrecisionType GetUDeltaPrecisionType(float valueA, float valueB, out float unsignedDifference)
        {
            bool bIsLarger = valueB > valueA;
            if (bIsLarger)
                unsignedDifference = valueB - valueA;
            else
                unsignedDifference = valueA - valueB;

            UDeltaPrecisionType result = GetDeltaPrecisionType(unsignedDifference, unsigned: true);
            // If result is set then set if bIsLarger.
            if (bIsLarger && result != UDeltaPrecisionType.Unset)
                result |= UDeltaPrecisionType.NextValueIsLarger;

            return result;
        }

        /// <summary>
        /// Returns DeltaPrecisionType for a value.
        /// </summary>
        public UDeltaPrecisionType GetDeltaPrecisionType(float positiveValue, bool unsigned)
        {
            if (unsigned)
            {
                return positiveValue switch
                {
                    < (float)DOUBLE_ACCURACY_PRECISION => UDeltaPrecisionType.Unset,
                    < (float)LARGEST_DELTA_PRECISION_UINT8 => UDeltaPrecisionType.UInt8,
                    < (float)LARGEST_DELTA_PRECISION_UINT16 => UDeltaPrecisionType.UInt16,
                    < (float)LARGEST_DELTA_PRECISION_UINT32 => UDeltaPrecisionType.UInt32,
                    _ => UDeltaPrecisionType.Unset
                };
            }
            else
            {
                return positiveValue switch
                {
                    < (float)(DOUBLE_ACCURACY_PRECISION / 2d) => UDeltaPrecisionType.Unset,
                    < (float)LARGEST_DELTA_PRECISION_INT8 => UDeltaPrecisionType.UInt8,
                    < (float)LARGEST_DELTA_PRECISION_INT16 => UDeltaPrecisionType.UInt16,
                    < (float)LARGEST_DELTA_PRECISION_INT32 => UDeltaPrecisionType.UInt32,
                    _ => UDeltaPrecisionType.Unset
                };
            }
        }
        #endregion

        #region Double.
        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteUDeltaDouble(double valueA, double valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            UDeltaPrecisionType dpt = GetUDeltaPrecisionType(valueA, valueB, out double positiveDifference);

            if (dpt == UDeltaPrecisionType.Unset && option == DeltaSerializerOption.Unset)
                return false;

            WriteUInt8Unpacked((byte)dpt);
            WriteDeltaDouble(dpt, positiveDifference, unsigned: true);

            return true;
        }

        /// <summary>
        /// Writes a double using DeltaPrecisionType.
        /// </summary>
        private void WriteDeltaDouble(UDeltaPrecisionType dpt, double value, bool unsigned)
        {
            if (dpt.FastContains(UDeltaPrecisionType.UInt8))
            {
                if (unsigned)
                    WriteUInt8Unpacked((byte)Math.Round(value * DOUBLE_ACCURACY));
                else
                    WriteInt8Unpacked((sbyte)Math.Round(value * DOUBLE_ACCURACY));
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
            {
                if (unsigned)
                    WriteUInt16Unpacked((ushort)Math.Round(value * DOUBLE_ACCURACY));
                else
                    WriteInt16Unpacked((short)Math.Round(value * DOUBLE_ACCURACY));
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt32))
            {
                if (unsigned)
                    WriteUInt32Unpacked((uint)Math.Round(value * DOUBLE_ACCURACY));
                else
                    WriteInt32Unpacked((int)Math.Round(value * DOUBLE_ACCURACY));
            }
            else if (dpt.FastContains(UDeltaPrecisionType.Unset))
            {
                WriteDoubleUnpacked(value);
            }
            else
            {
                NetworkManager.LogError($"Unhandled precision type of {dpt}.");
            }
        }

        /// <summary>
        /// Returns DeltaPrecisionType for the difference of two values.
        /// </summary>
        public UDeltaPrecisionType GetSDeltaPrecisionType(double valueA, double valueB, out double signedDifference)
        {
            signedDifference = valueB - valueA;
            double posValue = signedDifference < 0d ? signedDifference * -1d : signedDifference;

            return GetDeltaPrecisionType(posValue, unsigned: false);
        }

        /// <summary>
        /// Returns DeltaPrecisionType for the difference of two values.
        /// </summary>
        public UDeltaPrecisionType GetUDeltaPrecisionType(double valueA, double valueB, out double unsignedDifference)
        {
            bool bIsLarger = valueB > valueA;
            if (bIsLarger)
                unsignedDifference = valueB - valueA;
            else
                unsignedDifference = valueA - valueB;

            UDeltaPrecisionType result = GetDeltaPrecisionType(unsignedDifference, unsigned: true);
            if (bIsLarger && result != UDeltaPrecisionType.Unset)
                result |= UDeltaPrecisionType.NextValueIsLarger;

            return result;
        }

        /// <summary>
        /// Returns DeltaPrecisionType for a value.
        /// </summary>
        public UDeltaPrecisionType GetDeltaPrecisionType(double positiveValue, bool unsigned)
        {
            if (unsigned)
            {
                return positiveValue switch
                {
                    < LARGEST_DELTA_PRECISION_UINT8 => UDeltaPrecisionType.UInt8,
                    < LARGEST_DELTA_PRECISION_UINT16 => UDeltaPrecisionType.UInt16,
                    < LARGEST_DELTA_PRECISION_UINT32 => UDeltaPrecisionType.UInt32,
                    _ => UDeltaPrecisionType.Unset
                };
            }
            else
            {
                return positiveValue switch
                {
                    < LARGEST_DELTA_PRECISION_INT8 => UDeltaPrecisionType.UInt8,
                    < LARGEST_DELTA_PRECISION_INT16 => UDeltaPrecisionType.UInt16,
                    < LARGEST_DELTA_PRECISION_INT32 => UDeltaPrecisionType.UInt32,
                    _ => UDeltaPrecisionType.Unset
                };
            }
        }
        #endregion

        #region Decimal
        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteUDeltaDecimal(decimal valueA, decimal valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            UDeltaPrecisionType dpt = GetUDeltaPrecisionType(valueA, valueB, out decimal positiveDifference);

            if (dpt == UDeltaPrecisionType.Unset && option == DeltaSerializerOption.Unset)
                return false;

            WriteUInt8Unpacked((byte)dpt);
            WriteDeltaDecimal(dpt, positiveDifference, unsigned: true);

            return true;
        }

        /// <summary>
        /// Writes a double using DeltaPrecisionType.
        /// </summary>
        private void WriteDeltaDecimal(UDeltaPrecisionType dpt, decimal value, bool unsigned)
        {
            if (dpt.FastContains(UDeltaPrecisionType.UInt8))
            {
                if (unsigned)
                    WriteUInt8Unpacked((byte)Math.Round(value * DECIMAL_ACCURACY));
                else
                    WriteInt8Unpacked((sbyte)Math.Round(value * DECIMAL_ACCURACY));
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
            {
                if (unsigned)
                    WriteUInt16Unpacked((ushort)Math.Round(value * DECIMAL_ACCURACY));
                else
                    WriteInt16Unpacked((short)Math.Round(value * DECIMAL_ACCURACY));
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt32))
            {
                if (unsigned)
                    WriteUInt32Unpacked((uint)Math.Round(value * DECIMAL_ACCURACY));
                else
                    WriteInt32Unpacked((int)Math.Round(value * DECIMAL_ACCURACY));
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt64))
            {
                if (unsigned)
                    WriteUInt64Unpacked((ulong)Math.Round(value * DECIMAL_ACCURACY));
                else
                    WriteInt64Unpacked((long)Math.Round(value * DECIMAL_ACCURACY));
            }
            else if (dpt.FastContains(UDeltaPrecisionType.Unset))
            {
                WriteDecimalUnpacked(value);
            }
            else
            {
                NetworkManager.LogError($"Unhandled precision type of {dpt}.");
            }
        }

        /// <summary>
        /// Returns DeltaPrecisionType for the difference of two values.
        /// </summary>
        public UDeltaPrecisionType GetSDeltaPrecisionType(decimal valueA, decimal valueB, out decimal signedDifference)
        {
            signedDifference = valueB - valueA;
            decimal posValue = signedDifference < 0m ? signedDifference * -1m : signedDifference;

            return GetDeltaPrecisionType(posValue, unsigned: false);
        }

        /// <summary>
        /// Returns DeltaPrecisionType for the difference of two values.
        /// </summary>
        public UDeltaPrecisionType GetUDeltaPrecisionType(decimal valueA, decimal valueB, out decimal unsignedDifference)
        {
            bool bIsLarger = valueB > valueA;
            if (bIsLarger)
                unsignedDifference = valueB - valueA;
            else
                unsignedDifference = valueA - valueB;

            UDeltaPrecisionType result = GetDeltaPrecisionType(unsignedDifference, unsigned: true);
            if (bIsLarger && result != UDeltaPrecisionType.Unset)
                result |= UDeltaPrecisionType.NextValueIsLarger;

            return result;
        }

        /// <summary>
        /// Returns DeltaPrecisionType for a value.
        /// </summary>
        public UDeltaPrecisionType GetDeltaPrecisionType(decimal positiveValue, bool unsigned)
        {
            if (unsigned)
            {
                return positiveValue switch
                {
                    < (decimal)LARGEST_DELTA_PRECISION_UINT8 => UDeltaPrecisionType.UInt8,
                    < (decimal)LARGEST_DELTA_PRECISION_UINT16 => UDeltaPrecisionType.UInt16,
                    < (decimal)LARGEST_DELTA_PRECISION_UINT32 => UDeltaPrecisionType.UInt32,
                    < (decimal)LARGEST_DELTA_PRECISION_UINT64 => UDeltaPrecisionType.UInt64,
                    _ => UDeltaPrecisionType.Unset
                };
            }
            else
            {
                return positiveValue switch
                {
                    < (decimal)LARGEST_DELTA_PRECISION_INT8 => UDeltaPrecisionType.UInt8,
                    < (decimal)LARGEST_DELTA_PRECISION_INT16 => UDeltaPrecisionType.UInt16,
                    < (decimal)LARGEST_DELTA_PRECISION_INT32 => UDeltaPrecisionType.UInt32,
                    < (decimal)LARGEST_DELTA_PRECISION_INT64 => UDeltaPrecisionType.UInt64,
                    _ => UDeltaPrecisionType.Unset
                };
            }
        }
        #endregion

        #region FishNet Types.
        /// <summary>
        /// Writes a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaWriter]
        public bool WriteDeltaNetworkBehaviour(NetworkBehaviour valueA, NetworkBehaviour valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            bool unchangedValue = valueA == valueB;
            if (unchangedValue && option == DeltaSerializerOption.Unset)
                return false;

            WriteNetworkBehaviour(valueB);
            return true;
        }
        #endregion

        #region Unity.
        /// <summary>
        /// Writes delta position, rotation, and scale of a transform.
        /// </summary>
        public bool WriteDeltaTransformProperties(TransformProperties valueA, TransformProperties valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            /* Skip grows Length, which only ever grows, so rewinding when nothing is written must
             * restore Length as well as Position. Otherwise the skipped byte remains within
             * GetArraySegment when this is the last value written. */
            int startLength = Length;
            int startPosition = Position;
            Skip(1);

            byte allFlags = 0;

            if (WriteDeltaVector3(valueA.Position, valueB.Position))
                allFlags |= 1;
            if (WriteDeltaQuaternion(valueA.Rotation, valueB.Rotation))
                allFlags |= 2;
            if (WriteDeltaVector3(valueA.Scale, valueB.Scale))
                allFlags |= 4;

            if (allFlags != 0 || option != DeltaSerializerOption.Unset)
            {
                InsertUInt8Unpacked(allFlags, startPosition);
                return true;
            }
            else
            {
                Position = startPosition;
                Length = startLength;
                return false;
            }
        }

        /// <summary>
        /// Writes a delta quaternion.
        /// </summary>
        [DefaultDeltaWriter]
        public bool WriteDeltaQuaternion(Quaternion valueA, Quaternion valueB, float precision = QUATERNION_PRECISION, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            bool changed = option != DeltaSerializerOption.Unset || IsQuaternionChanged(valueA, valueB, precision);

            if (!changed)
                return false;

            QuaternionDeltaPrecisionCompression.Compress(this, valueA, valueB, precision);

            return true;
        }

        /// <summary>
        /// Returns if quaternion values differ by enough to be written at precision.
        /// </summary>
        private bool IsQuaternionChanged(Quaternion valueA, Quaternion valueB, float precision)
        {
            /* Half of precision is the smallest difference which compresses to a non-zero value.
             * A fixed threshold of 0.0025 ignored precision, discarding changes of up to 25
             * times the default precision. */
            float minimumChange = precision * 0.5f;

            if (Mathf.Abs(valueA.x - valueB.x) > minimumChange)
                return true;
            else if (Mathf.Abs(valueA.y - valueB.y) > minimumChange)
                return true;
            else if (Mathf.Abs(valueA.z - valueB.z) > minimumChange)
                return true;
            else if (Mathf.Abs(valueA.w - valueB.w) > minimumChange)
                return true;

            return false;
        }

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        [DefaultDeltaWriter]
        public bool WriteDeltaVector2(Vector2 valueA, Vector2 valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            // TODO Fit as many flags into a byte as possible for pack levels of each axis rather than 1 per axis.
            byte allFlags = 0;

            // See WriteDeltaTransformProperties for why Length is restored.
            int startLength = Length;
            int startPosition = Position;
            Skip(1);

            if (WriteUDeltaSingle(valueA.x, valueB.x))
                allFlags += 1;
            if (WriteUDeltaSingle(valueA.y, valueB.y))
                allFlags += 2;

            if (allFlags != 0 || option != DeltaSerializerOption.Unset)
            {
                InsertUInt8Unpacked(allFlags, startPosition);
                return true;
            }

            Position = startPosition;
            Length = startLength;
            return false;
        }

        [DefaultDeltaWriter]
        public bool WriteDeltaVector3(Vector3 valueA, Vector3 valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            byte allFlags = 0;

            // See WriteDeltaTransformProperties for why Length is restored.
            int startLength = Length;
            int startPosition = Position;
            Skip(1);

            if (WriteUDeltaSingle(valueA.x, valueB.x))
                allFlags += 1;
            if (WriteUDeltaSingle(valueA.y, valueB.y))
                allFlags += 2;
            if (WriteUDeltaSingle(valueA.z, valueB.z))
                allFlags += 4;

            if (allFlags != 0 || option != DeltaSerializerOption.Unset)
            {
                InsertUInt8Unpacked(allFlags, startPosition);
                return true;
            }

            Position = startPosition;
            Length = startLength;
            return false;
        }

        /// <summary>
        /// Writes a delta value.
        /// </summary>
        // [DefaultDeltaWriter]
        public bool WriteDeltaVector3_New(Vector3 valueA, Vector3 valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            UnsignedVector3DeltaFlag flags = UnsignedVector3DeltaFlag.Unset;

            // Get precision type and out values.
            UDeltaPrecisionType xDpt = GetUDeltaPrecisionType(valueA.x, valueB.x, out float xUnsignedDifference);
            UDeltaPrecisionType yDpt = GetUDeltaPrecisionType(valueA.y, valueB.y, out float yUnsignedDifference);
            UDeltaPrecisionType zDpt = GetUDeltaPrecisionType(valueA.z, valueB.z, out float zUnsignedDifference);

            byte unsetDpt = (byte)UDeltaPrecisionType.Unset;
            bool flagsAreUnset = (byte)xDpt == unsetDpt && (byte)yDpt > unsetDpt && (byte)zDpt > unsetDpt;

            // No change, can exit early.
            if (flagsAreUnset && option == DeltaSerializerOption.Unset)
                return false;

            // No change but must write there's no change.
            if (flagsAreUnset && option != DeltaSerializerOption.Unset)
            {
                WriteUInt8Unpacked((byte)UnsignedVector3DeltaFlag.Unset);
                return true;
            }

            /* If here there is change. */
            int startPosition = Position;

            /* If x, y, or z dpt doesn't contain uint8 then it must contain a higher value.
             * We already exited early if all values were unset, so there's no reason to
             * check for unset here. */
            bool areFlagsMultipleBytes = !xDpt.FastContains(UDeltaPrecisionType.UInt8) || !yDpt.FastContains(UDeltaPrecisionType.UInt8) || !zDpt.FastContains(UDeltaPrecisionType.UInt8);

            if (areFlagsMultipleBytes)
            {
                Skip(2);
                flags |= UnsignedVector3DeltaFlag.More;
            }
            else
            {
                Skip(1);
            }

            // Write X.
            if (xDpt != UDeltaPrecisionType.Unset)
            {
                flags |= GetShiftedFlag(xDpt, shift: 0);
                WriteDeltaSingle(xDpt, xUnsignedDifference, unsigned: true);
            }

            // Write Y.
            if (yDpt != UDeltaPrecisionType.Unset)
            {
                flags |= GetShiftedFlag(yDpt, shift: 2);
                WriteDeltaSingle(yDpt, yUnsignedDifference, unsigned: true);
            }

            // Write Z.
            if (zDpt != UDeltaPrecisionType.Unset)
            {
                flags |= GetShiftedFlag(zDpt, shift: 4);
                WriteDeltaSingle(zDpt, zUnsignedDifference, unsigned: true);
            }

            // Returns flags to add onto delta flags using precisionType and shift.
            UnsignedVector3DeltaFlag GetShiftedFlag(UDeltaPrecisionType precisionType, int shift)
            {
                int result;
                if (precisionType.FastContains(UDeltaPrecisionType.UInt8))
                {
                    result = (int)UnsignedVector3DeltaFlag.X1 << shift;
                    //   Debug.Log($"Axes {axes}. X1 {(int)UnsignedVector3DeltaFlag.X1}. Shifted {result}. Shift {shift}.");
                }
                else if (precisionType.FastContains(UDeltaPrecisionType.UInt16))
                {
                    result = (int)UnsignedVector3DeltaFlag.X2 << shift;
                }
                else
                {
                    result = (int)UnsignedVector3DeltaFlag.X4 << shift;
                }

                if (precisionType.FastContains(UDeltaPrecisionType.NextValueIsLarger))
                    result |= (int)UnsignedVector3DeltaFlag.NextXIsLarger << shift;

                return (UnsignedVector3DeltaFlag)result;
            }

            /* Do another check for if one byte or two, then write flags. */

            // Multiple bytes.
            if (areFlagsMultipleBytes)
            {
                int flagsValue = (int)flags;

                int firstByte = flagsValue & 0xff;
                InsertUInt8Unpacked((byte)firstByte, startPosition);
                int secondByte = flagsValue >> 8;
                InsertUInt8Unpacked((byte)secondByte, startPosition + 1);
            }
            // One byte.
            else
            {
                InsertUInt8Unpacked((byte)flags, startPosition);
            }

            return true;
        }
        #endregion

        #region Prediction.
        /// <summary>
        /// Set in a delta reconcile header when the reconcile is written in full rather than as a delta.
        /// </summary>
        internal const byte RECONCILE_FULL_FLAG = 1 << 7;
        /// <summary>
        /// Bits of a delta reconcile header which identify the full reconcile.
        /// </summary>
        internal const byte RECONCILE_ID_MASK = RECONCILE_FULL_FLAG - 1;

        /// <summary>
        /// True if T has both a delta writer and a delta reader.
        /// Prediction types without both are sent using their regular serializers, exactly as when delta prediction is disabled.
        /// </summary>
        internal static bool HasDeltaSerializers<T>() => GenericDeltaWriter<T>.Write != null && GenericDeltaReader<T>.Read != null;

        /// <summary>
        /// Writes a delta reconcile.
        /// </summary>
        /// <param name = "fullReconcile">Full reconcile which value is written as a delta against.</param>
        /// <param name = "fullReconcileId">Identifies fullReconcile, or value when fullSerialize is true, so readers can tell if they hold it.</param>
        /// <param name = "fullSerialize">True to write the reconcile so that it may be read without a baseline.</param>
        /// <remarks>Delta serializers must not modify the value they are given as a baseline, as it is reused for every delta until the next full reconcile.</remarks>
        internal void WriteDeltaReconcile<T>(T fullReconcile, T value, byte fullReconcileId, bool fullSerialize)
        {
            if (!HasDeltaSerializers<T>())
            {
                WriteReconcile(value);
                return;
            }

            byte header = (byte)(fullReconcileId & RECONCILE_ID_MASK);
            if (fullSerialize)
                header |= RECONCILE_FULL_FLAG;
            WriteUInt8Unpacked(header);

            /* A full reconcile uses the regular serializer. Delta serializers write differences,
             * so even with FullSerialize their output cannot be read without the baseline. */
            if (fullSerialize)
                WriteReconcile(value);
            else
                WriteDelta(fullReconcile, value, DeltaSerializerOption.RootSerialize);
        }

        /// <summary>
        /// Writes replicates using delta serializers.
        /// </summary>
        /// <remarks>
        /// Each packet can be read on its own: the first entry is written in full and every following entry as a delta against the entry before it in the same packet.
        /// Replicates are sent unreliably with past entries for redundancy; a delta against an entry from an earlier packet could not be read once that packet were lost.
        /// </remarks>
        internal void WriteDeltaReplicate<T>(RingBuffer<ReplicateDataContainer<T>> values, int offset) where T : IReplicateData, new()
        {
            if (!HasDeltaSerializers<T>())
            {
                WriteReplicate(values, offset);
                return;
            }

            int collectionCount = values.Count;
            // Replicate list will never be null, no need to write null check.
            // Number of entries being written.
            byte count = (byte)(collectionCount - offset);
            WriteUInt8Unpacked(count);

            for (int i = offset; i < collectionCount; i++)
            {
                bool isFirst = i == offset;
                WriteDeltaReplicateDataContainer(isFirst ? default : values[i - 1], values[i], isFirst);
            }
        }

        /// <summary>
        /// Writes replicates using delta serializers.
        /// </summary>
        /// <remarks>See the RingBuffer overload.</remarks>
        internal void WriteDeltaReplicate<T>(BasicQueue<ReplicateDataContainer<T>> values, int redundancyCount) where T : IReplicateData, new()
        {
            if (!HasDeltaSerializers<T>())
            {
                WriteReplicate(values, redundancyCount);
                return;
            }

            int collectionCount = values.Count;
            // Replicate list will never be null, no need to write null check.
            // Number of entries being written.
            byte count = (byte)redundancyCount;
            WriteUInt8Unpacked(count);

            int offset = collectionCount - redundancyCount;
            for (int i = offset; i < collectionCount; i++)
            {
                bool isFirst = i == offset;
                WriteDeltaReplicateDataContainer(isFirst ? default : values[i - 1], values[i], isFirst);
            }
        }

        /// <summary>
        /// Writes a replicate entry in full or as a delta against the previous entry, followed by its channel.
        /// </summary>
        private void WriteDeltaReplicateDataContainer<T>(ReplicateDataContainer<T> previous, ReplicateDataContainer<T> value, bool fullSerialize) where T : IReplicateData, new()
        {
            if (fullSerialize)
                Write<T>(value.Data);
            else
                WriteDelta(previous.Data, value.Data, DeltaSerializerOption.RootSerialize);

            WriteChannel(value.Channel);
        }
        #endregion

        #region Generic.
        public bool WriteDelta<T>(T prev, T next, DeltaSerializerOption option = DeltaSerializerOption.Unset)
        {
            Func<Writer, T, T, DeltaSerializerOption, bool> del = GenericDeltaWriter<T>.Write;

            if (del == null)
            {
                NetworkManager.LogError($"Write delta method not found for {typeof(T).FullName}. Use a supported type or create a custom serializer.");

                return false;
            }
            else
            {
                return del.Invoke(this, prev, next, option);
            }
        }
        #endregion
    }
}