using System;
using System.Collections.Generic;
using FishNet.CodeGenerating;
using System.Runtime.CompilerServices;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Serializing
{
    public partial class Reader
    {
        internal double DOUBLE_ACCURACY => Writer.DOUBLE_ACCURACY;
        internal decimal DECIMAL_ACCURACY => Writer.DECIMAL_ACCURACY;

        #region Other.
        /// <summary>
        /// Reads a boolean.
        /// </summary>
        [DefaultDeltaReader]
        public bool ReadDeltaBoolean(bool valueA)
        {
            /* WriteDeltaBoolean writes the new value whenever it returns true, including
             * when the value is unchanged and a serialize option forced the write. The byte
             * must be consumed to keep the reader aligned, and it is the value to return. */
            return ReadBoolean();
        }
        #endregion

        #region Whole values.
        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public sbyte ReadDeltaInt8(sbyte valueA) => (sbyte)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public byte ReadDeltaUInt8(byte valueA) => (byte)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public short ReadDeltaInt16(short valueA) => (short)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public ushort ReadDeltaUInt16(ushort valueA) => (ushort)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public int ReadDeltaInt32(int valueA) => (int)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public uint ReadDeltaUInt32(uint valueA) => (uint)ReadDifference8_16_32(valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public long ReadDeltaInt64(long valueA) => (long)ReadDeltaUInt64((ulong)valueA);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public ulong ReadDeltaUInt64(ulong valueA)
        {
            bool bLargerThanA = ReadBoolean();
            ulong diff = ReadUnsignedPackedWhole();

            return bLargerThanA ? valueA + diff : valueA - diff;
        }

        /// <summary>
        /// Returns a new result by reading and applying a difference to a value.
        /// </summary>
        [DefaultDeltaReader]
        private long ReadDifference8_16_32(long valueA)
        {
            long diff = ReadSignedPackedWhole();
            return valueA + diff;
        }
        #endregion

        #region Single.
        /// <summary>
        /// Reads a value.
        /// </summary>
        public float ReadDeltaSingle(UDeltaPrecisionType dpt, bool unsigned)
        {
            if (dpt.FastContains(UDeltaPrecisionType.UInt8))
            {
                if (unsigned)
                    return ReadUInt8Unpacked() / (float)DOUBLE_ACCURACY;
                else
                    return ReadInt8Unpacked() / (float)DOUBLE_ACCURACY;
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
            {
                if (unsigned)
                    return ReadUInt16Unpacked() / (float)DOUBLE_ACCURACY;
                else
                    return ReadInt16Unpacked() / (float)DOUBLE_ACCURACY;
            }
            // Everything else is unpacked.
            else
            {
                return ReadSingleUnpacked();
            }
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        public float ReadDeltaSingle(UDeltaPrecisionType dpt, float valueA, bool unsigned)
        {
            float diff = ReadDeltaSingle(dpt, unsigned);

            if (unsigned)
            {
                bool bLargerThanA = dpt.FastContains(UDeltaPrecisionType.NextValueIsLarger);
                return bLargerThanA ? valueA + diff : valueA - diff;
            }
            else
            {
                return valueA + diff;
            }
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        public float ReadDeltaSingle(float valueA)
        {
            const bool unsigned = false;
            UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();

            return ReadDeltaSingle(dpt, valueA, unsigned);
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public float ReadUDeltaSingle(float valueA)
        {
            const bool unsigned = true;
            UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();

            return ReadDeltaSingle(dpt, valueA, unsigned);
        }
        #endregion

        #region Double.
        /// <summary>
        /// Reads a value.
        /// </summary>
        public double ReadDeltaDouble(UDeltaPrecisionType dpt, bool unsigned)
        {
            if (dpt.FastContains(UDeltaPrecisionType.UInt8))
            {
                if (unsigned)
                    return ReadUInt8Unpacked() / DOUBLE_ACCURACY;
                else
                    return ReadInt8Unpacked() / DOUBLE_ACCURACY;
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
            {
                if (unsigned)
                    return ReadUInt16Unpacked() / DOUBLE_ACCURACY;
                else
                    return ReadInt16Unpacked() / DOUBLE_ACCURACY;
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt32))
            {
                if (unsigned)
                    return ReadUInt32Unpacked() / DOUBLE_ACCURACY;
                else
                    return ReadInt32Unpacked() / DOUBLE_ACCURACY;
            }
            // Unpacked.
            else if (dpt.FastContains(UDeltaPrecisionType.Unset))
            {
                return ReadDoubleUnpacked();
            }
            else
            {
                NetworkManager.LogError($"Unhandled precision type of {dpt}.");
                return 0d;
            }
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        public double ReadDeltaDouble(UDeltaPrecisionType dpt, double valueA, bool unsigned)
        {
            double diff = ReadDeltaDouble(dpt, unsigned);
            // 8.

            if (unsigned)
            {
                bool bLargerThanA = dpt.FastContains(UDeltaPrecisionType.NextValueIsLarger);
                return bLargerThanA ? valueA + diff : valueA - diff;
            }
            else
            {
                return valueA + diff;
            }
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        public double ReadDeltaDouble(double valueA)
        {
            const bool unsigned = false;
            UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();

            return ReadDeltaDouble(dpt, valueA, unsigned);
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public double ReadUDeltaDouble(double valueA)
        {
            const bool unsigned = true;
            UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();

            return ReadDeltaDouble(dpt, valueA, unsigned);
        }
        #endregion

        #region Decimal.
        /// <summary>
        /// Reads a value.
        /// </summary>
        public decimal ReadDeltaDecimal(UDeltaPrecisionType dpt, bool unsigned)
        {
            if (dpt.FastContains(UDeltaPrecisionType.UInt8))
            {
                if (unsigned)
                    return ReadUInt8Unpacked() / DECIMAL_ACCURACY;
                else
                    return ReadInt8Unpacked() / DECIMAL_ACCURACY;
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
            {
                if (unsigned)
                    return ReadUInt16Unpacked() / DECIMAL_ACCURACY;
                else
                    return ReadInt16Unpacked() / DECIMAL_ACCURACY;
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt32))
            {
                if (unsigned)
                    return ReadUInt32Unpacked() / DECIMAL_ACCURACY;
                else
                    return ReadInt32Unpacked() / DECIMAL_ACCURACY;
            }
            else if (dpt.FastContains(UDeltaPrecisionType.UInt64))
            {
                if (unsigned)
                    return ReadUInt64Unpacked() / DECIMAL_ACCURACY;
                else
                    return ReadInt64Unpacked() / DECIMAL_ACCURACY;
            }
            // Unpacked.
            else if (dpt.FastContains(UDeltaPrecisionType.Unset))
            {
                return ReadDecimalUnpacked();
            }
            else
            {
                NetworkManager.LogError($"Unhandled precision type of {dpt}.");
                return 0m;
            }
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        public decimal ReadDeltaDecimal(UDeltaPrecisionType dpt, decimal valueA, bool unsigned)
        {
            decimal diff = ReadDeltaDecimal(dpt, unsigned);

            if (unsigned)
            {
                bool bLargerThanA = dpt.FastContains(UDeltaPrecisionType.NextValueIsLarger);
                return bLargerThanA ? valueA + diff : valueA - diff;
            }
            else
            {
                return valueA + diff;
            }
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public decimal ReadDeltaDecimal(decimal valueA)
        {
            const bool unsigned = false;
            UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();

            return ReadDeltaDecimal(dpt, valueA, unsigned);
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public decimal ReadUDeltaDecimal(decimal valueA)
        {
            const bool unsigned = true;
            UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();

            return ReadDeltaDecimal(dpt, valueA, unsigned);
        }
        #endregion

        #region FishNet Types.
        /// <summary>
        /// Reads a delta value.
        /// </summary>
        /// <returns>True if written.</returns>
        [DefaultDeltaReader]
        public NetworkBehaviour WriteDeltaNetworkBehaviour(NetworkBehaviour valueA)
        {
            return ReadNetworkBehaviour();
        }
        #endregion

        #region Unity.
        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// (not really for Quaternion).
        /// </summary>
        [DefaultDeltaReader]
        public Quaternion ReadDeltaQuaternion(Quaternion valueA, float precision = Writer.QUATERNION_PRECISION) => QuaternionDeltaPrecisionCompression.Decompress(this, valueA, precision);

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public Vector2 ReadDeltaVector2(Vector2 valueA)
        {
            byte allFlags = ReadUInt8Unpacked();

            if ((allFlags & 1) == 1)
                valueA.x = ReadUDeltaSingle(valueA.x);
            if ((allFlags & 2) == 2)
                valueA.y = ReadUDeltaSingle(valueA.y);

            return valueA;
        }

        /// <summary>
        /// Reads a difference, appending it onto a value.
        /// </summary>
        [DefaultDeltaReader]
        public Vector3 ReadDeltaVector3(Vector3 valueA)
        {
            byte allFlags = ReadUInt8Unpacked();

            if ((allFlags & 1) == 1)
                valueA.x = ReadUDeltaSingle(valueA.x);
            if ((allFlags & 2) == 2)
                valueA.y = ReadUDeltaSingle(valueA.y);
            if ((allFlags & 4) == 4)
                valueA.z = ReadUDeltaSingle(valueA.z);

            return valueA;
        }
        #endregion

        #region Prediction.
        /// <summary>
        /// Reads a reconcile written by WriteDeltaReconcile.
        /// </summary>
        /// <param name = "fullReconcile">Full reconcile a delta is applied to.</param>
        /// <param name = "isFull">True if the reconcile was written so that it could be read without a baseline.</param>
        /// <param name = "fullReconcileId">Identifies the full reconcile a delta was written against, or this reconcile when isFull is true.</param>
        internal T ReadDeltaReconcile<T>(T fullReconcile, out bool isFull, out byte fullReconcileId)
        {
            if (!Writer.HasDeltaSerializers<T>())
            {
                isFull = true;
                fullReconcileId = 0;
                return ReadReconcile<T>();
            }

            byte header = ReadUInt8Unpacked();
            isFull = (header & Writer.RECONCILE_FULL_FLAG) != 0;
            fullReconcileId = (byte)(header & Writer.RECONCILE_ID_MASK);

            return isFull ? ReadReconcile<T>() : ReadDelta(fullReconcile);
        }

        /// <summary>
        /// Reads replicates written by WriteDeltaReplicate.
        /// </summary>
        internal List<ReplicateDataContainer<T>> ReadDeltaReplicate<T>(uint tick) where T : IReplicateData, new()
        {
            if (!Writer.HasDeltaSerializers<T>())
                return ReadReplicate<T>(tick);

            List<ReplicateDataContainer<T>> collection = CollectionCaches<ReplicateDataContainer<T>>.RetrieveList();

            // Number of entries written.
            int count = (int)ReadUInt8Unpacked();
            if (count <= 0)
            {
                NetworkManager.Log($"Replicate count cannot be 0 or less.");
                // Purge remaining and return default.
                Position += Remaining;
                return collection;
            }
            // Ticks are assigned from oldest to newest, as in ReadReplicate.
            tick -= (uint)(count - 1);

            T previous = default;
            for (int i = 0; i < count; i++)
            {
                // The first entry is written in full, the rest as a delta against the entry before them.
                T data = i == 0 ? Read<T>() : ReadDelta(previous);
                Channel c = ReadChannel();
                collection.Add(new(data, c, tick + (uint)i, isCreated: true));

                previous = data;
            }

            return collection;
        }
        #endregion

        #region Generic.
        /// <summary>
        /// Reads a delta of any time.
        /// </summary>
        public T ReadDelta<T>(T prev)
        {
            Func<Reader, T, T> del = GenericDeltaReader<T>.Read;

            if (del == null)
            {
                NetworkManager.LogError($"Read delta method not found for {typeof(T).FullName}. Use a supported type or create a custom serializer.");
                return default;
            }
            else
            {
                return del.Invoke(this, prev);
            }
        }
        #endregion
    }
}