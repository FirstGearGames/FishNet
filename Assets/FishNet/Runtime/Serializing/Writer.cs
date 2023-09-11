using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using FishNet.Utility.Performance;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo(UtilityConstants.GENERATED_ASSEMBLY_NAME)]
namespace FishNet.Serializing
{

    /// <summary>
    /// Used for write references to generic types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [APIExclude]
    public static class GenericWriter<T>
    {
        public static Action<Writer, T> Write { get; set; }
        public static Action<Writer, T, AutoPackType> WriteAutoPack { get; set; }
    }

    /// <summary>
    /// Writes data to a buffer.
    /// </summary>
    public class Writer
    {
        #region Public.
        /// <summary>
        /// Capacity of the buffer.
        /// </summary>
        public int Capacity => _buffer.Length;
        /// <summary>
        /// Current write position.
        /// </summary>
        public int Position;
        /// <summary>
        /// Number of bytes writen to the buffer.
        /// </summary>
        public int Length;
        /// <summary>
        /// NetworkManager associated with this writer. May be null.
        /// </summary>
        public NetworkManager NetworkManager;
        #endregion

        #region Private.
        /// <summary>
        /// Buffer to prevent new allocations. This will grow as needed.
        /// </summary>
        private byte[] _buffer = new byte[64];
        #endregion

        #region Const.
        /// <summary>
        /// Replicate data is default of T.
        /// </summary>
        internal const byte REPLICATE_DEFAULT_BYTE = 0;
        /// <summary>
        /// Replicate data is the same as the previous.
        /// </summary>
        internal const byte REPLICATE_DUPLICATE_BYTE = 1;
        /// <summary>
        /// Replicate data is different from the previous.
        /// </summary>
        internal const byte REPLICATE_UNIQUE_BYTE = 2;
        /// <summary>
        /// Replicate data is repeating for every entry.
        /// </summary>
        internal const byte REPLICATE_REPEATING_BYTE = 3;
        /// <summary>
        /// All datas in the replicate are default.
        /// </summary>
        internal const byte REPLICATE_ALL_DEFAULT_BYTE = 4;
        /// <summary>
        /// Value used when a collection is unset, as in null.
        /// </summary>
        public const int UNSET_COLLECTION_SIZE_VALUE = -1;
        #endregion

        /// <summary>
        /// Outputs reader to string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Position: {Position}, Length: {Length}, Buffer: {BitConverter.ToString(_buffer, 0, Length)}.";
        }

        /// <summary>
        /// Resets the writer as though it was unused. Does not reset buffers.
        /// </summary>
        public void Reset(NetworkManager manager = null)
        {
            Length = 0;
            Position = 0;
            NetworkManager = manager;
        }

        /// <summary>
        /// Writes a dictionary.
        /// </summary>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            if (dict == null)
            {
                WriteBoolean(true);
                return;
            }
            else
            {
                WriteBoolean(false);
            }

            WriteInt32(dict.Count);
            foreach (KeyValuePair<TKey, TValue> item in dict)
            {
                Write(item.Key);
                Write(item.Value);
            }
        }

        /// <summary>
        /// Ensures the buffer Capacity is of minimum count.
        /// </summary>
        /// <param name="count"></param>
        public void EnsureBufferCapacity(int count)
        {
            if (Capacity < count)
                Array.Resize(ref _buffer, count);
        }

        /// <summary>
        /// Ensure a number of bytes to be available in the buffer from current position.
        /// </summary>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureBufferLength(int count)
        {
            if (Position + count > _buffer.Length)
            {
                int nextSize = (_buffer.Length * 2) + count;
                Array.Resize(ref _buffer, nextSize);
            }
        }

        /// <summary>
        /// Returns the buffer. The returned value will be the full buffer, even if not all of it is used.
        /// </summary>
        /// <returns></returns>
        public byte[] GetBuffer()
        {
            return _buffer;
        }

        /// <summary>
        /// Returns the used portion of the buffer as an ArraySegment.
        /// </summary>
        /// <returns></returns>
        public ArraySegment<byte> GetArraySegment()
        {
            return new ArraySegment<byte>(_buffer, 0, Length);
        }

        /// <summary>
        /// Reserves a number of bytes from current position.
        /// </summary>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reserve(int count)
        {
            EnsureBufferLength(count);
            Position += count;
            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes length. This method is used to make debugging easier.
        /// </summary>
        /// <param name="length"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteLength(int length)
        {
            WriteInt32(length);
        }

        /// <summary>
        /// Sends a packetId.
        /// </summary>
        /// <param name="pid"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WritePacketId(PacketId pid)
        {
            WriteUInt16((ushort)pid);
        }

        /// <summary>
        /// Inserts value at index within the buffer.
        /// This method does not perform error checks.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index"></param>
        [CodegenExclude]
        public void FastInsertByte(byte value, int index)
        {
            _buffer[index] = value;
        }

        /// <summary>
        /// Writes a byte.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            EnsureBufferLength(1);
            _buffer[Position++] = value;

            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes bytes.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(byte[] value, int offset, int count)
        {
            EnsureBufferLength(count);
            Buffer.BlockCopy(value, offset, _buffer, Position, count);
            Position += count;
            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes bytes and length of bytes.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesAndSize(byte[] value, int offset, int count)
        {
            if (value == null)
            {
                WriteInt32(Writer.UNSET_COLLECTION_SIZE_VALUE);
            }
            else
            {
                WriteInt32(count);
                WriteBytes(value, offset, count);
            }
        }

        /// <summary>
        /// Writes all bytes in value and length of bytes.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesAndSize(byte[] value)
        {
            int size = (value == null) ? 0 : value.Length;
            // buffer might be null, so we can't use .Length in that case
            WriteBytesAndSize(value, 0, size);
        }


        /// <summary>
        /// Writes a sbyte.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(sbyte value)
        {
            EnsureBufferLength(1);
            _buffer[Position++] = (byte)value;
            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes a char.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char value)
        {
            EnsureBufferLength(2);
            _buffer[Position++] = (byte)value;
            _buffer[Position++] = (byte)(value >> 8);
            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes a boolean.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolean(bool value)
        {
            EnsureBufferLength(1);
            _buffer[Position++] = (value) ? (byte)1 : (byte)0;
            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes a uint16.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort value)
        {
            EnsureBufferLength(2);
            _buffer[Position++] = (byte)value;
            _buffer[Position++] = (byte)(value >> 8);
            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes a int16.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(short value)
        {
            EnsureBufferLength(2);
            _buffer[Position++] = (byte)value;
            _buffer[Position++] = (byte)(value >> 8);
            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes a int32.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int value, AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
                WritePackedWhole(ZigZagEncode((ulong)value));
            else
                WriteUInt32((uint)value, packType);
        }
        /// <summary>
        /// Writes a uint32.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value, AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Unpacked)
            {
                EnsureBufferLength(4);
                WriterExtensions.WriteUInt32(_buffer, value, ref Position);
                Length = Math.Max(Length, Position);
            }
            else
            {
                WritePackedWhole(value);
            }
        }

        /// <summary>
        /// Writes an int64.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long value, AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
                WritePackedWhole(ZigZagEncode((ulong)value));
            else
                WriteUInt64((ulong)value, packType);
        }
        /// <summary>
        /// Writes a uint64.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value, AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Unpacked)
            {
                EnsureBufferLength(8);
                _buffer[Position++] = (byte)value;
                _buffer[Position++] = (byte)(value >> 8);
                _buffer[Position++] = (byte)(value >> 16);
                _buffer[Position++] = (byte)(value >> 24);
                _buffer[Position++] = (byte)(value >> 32);
                _buffer[Position++] = (byte)(value >> 40);
                _buffer[Position++] = (byte)(value >> 48);
                _buffer[Position++] = (byte)(value >> 56);

                Length = Math.Max(Position, Length);
            }
            else
            {
                WritePackedWhole(value);
            }
        }

        /// <summary>
        /// Writes a single (float).
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSingle(float value, AutoPackType packType = AutoPackType.Unpacked)
        {
            if (packType == AutoPackType.Unpacked)
            {
                UIntFloat converter = new UIntFloat { FloatValue = value };
                WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
            }
            else
            {
                long converter = (long)(value * 100f);
                WritePackedWhole((ulong)converter);
            }
        }

        /// <summary>
        /// Writes a double.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            UIntDouble converter = new UIntDouble { DoubleValue = value };
            WriteUInt64(converter.LongValue, AutoPackType.Unpacked);
        }

        /// <summary>
        /// Writes a decimal.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDecimal(decimal value)
        {
            UIntDecimal converter = new UIntDecimal { DecimalValue = value };
            WriteUInt64(converter.LongValue1, AutoPackType.Unpacked);
            WriteUInt64(converter.LongValue2, AutoPackType.Unpacked);
        }

        /// <summary>
        /// Writes a string.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteInt32(Writer.UNSET_COLLECTION_SIZE_VALUE);
                return;
            }
            else if (value.Length == 0)
            {
                WriteInt32(0);
                return;
            }

            /* Resize string buffer as needed. There's no harm in
             * increasing buffer on writer side because sender will
             * never intentionally inflict allocations on itself. 
             * Reader ensures string count cannot exceed received
             * packet size. */
            int size;
            byte[] stringBuffer = WriterStatics.GetStringBuffer(value, out size);
            WriteInt32(size);
            WriteBytes(stringBuffer, 0, size);
        }

        /// <summary>
        /// Writes a byte ArraySegment and it's size.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArraySegmentAndSize(ArraySegment<byte> value)
        {
            WriteBytesAndSize(value.Array, value.Offset, value.Count);
        }

        /// <summary>
        /// Writes an ArraySegment without size.
        /// </summary>
        /// <param name="value"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArraySegment(ArraySegment<byte> value)
        {
            WriteBytes(value.Array, value.Offset, value.Count);
        }

        /// <summary>
        /// Writes a Vector2.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector2(Vector2 value)
        {
            UIntFloat converter;
            converter = new UIntFloat { FloatValue = value.x };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
            converter = new UIntFloat { FloatValue = value.y };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
        }

        /// <summary>
        /// Writes a Vector3
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector3(Vector3 value)
        {
            UIntFloat converter;
            converter = new UIntFloat { FloatValue = value.x };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
            converter = new UIntFloat { FloatValue = value.y };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
            converter = new UIntFloat { FloatValue = value.z };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
        }

        /// <summary>
        /// Writes a Vector4.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector4(Vector4 value)
        {
            UIntFloat converter;
            converter = new UIntFloat { FloatValue = value.x };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
            converter = new UIntFloat { FloatValue = value.y };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
            converter = new UIntFloat { FloatValue = value.z };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
            converter = new UIntFloat { FloatValue = value.w };
            WriteUInt32(converter.UIntValue, AutoPackType.Unpacked);
        }

        /// <summary>
        /// Writes a Vector2Int.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector2Int(Vector2Int value, AutoPackType packType = AutoPackType.Packed)
        {
            WriteInt32(value.x, packType);
            WriteInt32(value.y, packType);
        }

        /// <summary>
        /// Writes a Vector3Int.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector3Int(Vector3Int value, AutoPackType packType = AutoPackType.Packed)
        {
            WriteInt32(value.x, packType);
            WriteInt32(value.y, packType);
            WriteInt32(value.z, packType);
        }

        /// <summary>
        /// Writes a Color.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColor(Color value, AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Unpacked)
            {
                WriteSingle(value.r);
                WriteSingle(value.g);
                WriteSingle(value.b);
                WriteSingle(value.a);
            }
            else
            {
                EnsureBufferLength(4);
                _buffer[Position++] = (byte)(value.r * 100f);
                _buffer[Position++] = (byte)(value.g * 100f);
                _buffer[Position++] = (byte)(value.b * 100f);
                _buffer[Position++] = (byte)(value.a * 100f);

                Length = Math.Max(Length, Position);
            }
        }

        /// <summary>
        /// Writes a Color32.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColor32(Color32 value)
        {
            EnsureBufferLength(4);
            _buffer[Position++] = value.r;
            _buffer[Position++] = value.g;
            _buffer[Position++] = value.b;
            _buffer[Position++] = value.a;

            Length = Math.Max(Length, Position);
        }

        /// <summary>
        /// Writes a Quaternion.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteQuaternion(Quaternion value, AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
            {
                EnsureBufferLength(4);
                uint result = Quaternion32Compression.Compress(value);
                WriterExtensions.WriteUInt32(_buffer, result, ref Position);
                Length = Math.Max(Length, Position);
            }
            else if (packType == AutoPackType.PackedLess)
            {
                EnsureBufferLength(8);
                ulong result = Quaternion64Compression.Compress(value);
                WriterExtensions.WriteUInt64(_buffer, result, ref Position);
                Length = Math.Max(Length, Position);
            }
            else
            {
                EnsureBufferLength(16);
                WriteSingle(value.x);
                WriteSingle(value.y);
                WriteSingle(value.z);
                WriteSingle(value.w);
            }
        }

        /// <summary>
        /// Writes a rect.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRect(Rect value)
        {
            WriteSingle(value.xMin);
            WriteSingle(value.yMin);
            WriteSingle(value.width);
            WriteSingle(value.height);
        }

        /// <summary>
        /// Writes a plane.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePlane(Plane value)
        {
            WriteVector3(value.normal);
            WriteSingle(value.distance);
        }

        /// <summary>
        /// Writes a Ray.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRay(Ray value)
        {
            WriteVector3(value.origin);
            WriteVector3(value.direction);
        }

        /// <summary>
        /// Writes a Ray2D.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRay2D(Ray2D value)
        {
            WriteVector2(value.origin);
            WriteVector2(value.direction);
        }


        /// <summary>
        /// Writes a Matrix4x4.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteMatrix4x4(Matrix4x4 value)
        {
            WriteSingle(value.m00);
            WriteSingle(value.m01);
            WriteSingle(value.m02);
            WriteSingle(value.m03);
            WriteSingle(value.m10);
            WriteSingle(value.m11);
            WriteSingle(value.m12);
            WriteSingle(value.m13);
            WriteSingle(value.m20);
            WriteSingle(value.m21);
            WriteSingle(value.m22);
            WriteSingle(value.m23);
            WriteSingle(value.m30);
            WriteSingle(value.m31);
            WriteSingle(value.m32);
            WriteSingle(value.m33);
        }

        /// <summary>
        /// Writes a Guid.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGuidAllocated(System.Guid value)
        {
            byte[] data = value.ToByteArray();
            WriteBytes(data, 0, data.Length);
        }

        /// <summary>
        /// Writes a tick without packing.
        /// </summary>
        /// <param name="value"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTickUnpacked(uint value)
        {
            WriteUInt32(value, AutoPackType.Unpacked);
        }

        /// <summary>
        /// Writes a GameObject. GameObject must be spawned over the network already or be a prefab with a NetworkObject attached.
        /// </summary>
        /// <param name="go"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGameObject(GameObject go)
        {
            //There needs to be a header to indicate if null, nob, or nb.
            if (go == null)
            {
                WriteByte(0);
            }
            else
            {
                //Try to write the NetworkObject first.
                if (go.TryGetComponent<NetworkObject>(out NetworkObject nob))
                {
                    WriteByte(1);
                    WriteNetworkObject(nob);
                }
                //If there was no nob try to write a NetworkBehaviour.
                else if (go.TryGetComponent<NetworkBehaviour>(out NetworkBehaviour nb))
                {
                    WriteByte(2);
                    WriteNetworkBehaviour(nb);
                }
                //Object cannot be serialized so write null.
                else
                {
                    WriteByte(0);
                    LogError($"GameObject {go.name} cannot be serialized because it does not have a NetworkObject nor NetworkBehaviour.");
                }
            }
        }

        /// <summary>
        /// Writes a Transform. Transform must be spawned over the network already or be a prefab with a NetworkObject attached.
        /// </summary>
        /// <param name="t"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTransform(Transform t)
        {
            if (t == null)
            {
                WriteNetworkObject(null);
            }
            else
            {
                NetworkObject nob = t.GetComponent<NetworkObject>();
                WriteNetworkObject(nob);
            }
        }


        /// <summary>
        /// Writes a NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNetworkObject(NetworkObject nob)
        {
            WriteNetworkObject(nob, false);
        }

        /// <summary>
        /// Writes a NetworkObject.ObjectId.
        /// </summary>
        /// <param name="nob"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNetworkObjectId(NetworkObject nob)
        {
            if (nob == null)
                WriteUInt16(NetworkObject.UNSET_OBJECTID_VALUE);
            else
                WriteNetworkObjectId(nob.ObjectId);
        }

        /// <summary>
        /// Writes a NetworkObject while optionally including the initialization order.
        /// </summary>
        /// <param name="nob"></param>
        /// <param name="forSpawn"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteNetworkObject(NetworkObject nob, bool forSpawn)
        {
            if (nob == null)
            {
                WriteUInt16(NetworkObject.UNSET_OBJECTID_VALUE);
            }
            else
            {
                bool spawned = nob.IsSpawned;
                if (spawned)
                    WriteNetworkObjectId(nob.ObjectId);
                else
                    WriteNetworkObjectId(nob.PrefabId);

                //Has to be written after objectId since that's expected first in reader.
                if (forSpawn)
                {
                    WriteUInt16(nob.SpawnableCollectionId);
                    WriteSByte(nob.GetInitializeOrder());
                }

                WriteBoolean(spawned);
            }
        }

        /// <summary>
        /// Writes a NetworkObject for a despawn message.
        /// </summary>
        /// <param name="nob"></param>
        /// <param name="dt"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteNetworkObjectForDespawn(NetworkObject nob, DespawnType dt)
        {
            WriteNetworkObjectId(nob.ObjectId);
            WriteByte((byte)dt);
        }


        /// <summary>
        /// Writes an objectId.
        /// </summary>
        [CodegenExclude]
        public void WriteNetworkObjectId(int objectId)
        {
            WriteUInt16((ushort)objectId);
        }

        /// <summary>
        /// Writes a NetworkObject for a spawn packet.
        /// </summary>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteNetworkObjectForSpawn(NetworkObject nob)
        {
            WriteNetworkObject(nob, true);
        }

        /// <summary>
        /// Writes a NetworkBehaviour.
        /// </summary>
        /// <param name="nb"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNetworkBehaviour(NetworkBehaviour nb)
        {
            if (nb == null)
            {
                WriteNetworkObject(null);
                WriteByte(0);
            }
            else
            {
                WriteNetworkObject(nb.NetworkObject);
                WriteByte(nb.ComponentIndex);
            }
        }

        /// <summary>
        /// Writes a NetworkBehaviourId.
        /// </summary>
        /// <param name="nb"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNetworkBehaviourId(NetworkBehaviour nb)
        {
            if (nb == null)
            {
                WriteNetworkObjectId(null);
            }
            else
            {
                WriteNetworkObjectId(nb.NetworkObject);
                WriteByte(nb.ComponentIndex);
            }
        }

        /// <summary>
        /// Writes a DateTime.
        /// </summary>
        /// <param name="dt"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDateTime(DateTime dt)
        {
            WriteInt64(dt.ToBinary());
        }

        /// <summary>
        /// Writes a transport channel.
        /// </summary>
        /// <param name="channel"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChannel(Channel channel)
        {
            WriteByte((byte)channel);
        }

        /// <summary>
        /// Writes a NetworkConnection.
        /// </summary>
        /// <param name="connection"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNetworkConnection(NetworkConnection connection)
        {
            int value = (connection == null) ? NetworkConnection.UNSET_CLIENTID_VALUE : connection.ClientId;
            WriteInt16((short)value);
        }

        /// <summary>
        /// Writes a short for a connectionId.
        /// </summary>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNetworkConnectionId(short id)
        {
            WriteInt16(id);
        }

        /// <summary>
        /// Writes a ListCache.
        /// </summary>
        /// <param name="lc">ListCache to write.</param>
        [CodegenExclude] //Remove on 2024/01/01.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable CS0618 // Type or member is obsolete
        public void WriteListCache<T>(ListCache<T> lc)
        {
            WriteList<T>(lc.Collection);
        }
#pragma warning restore CS0618 // Type or member is obsolete
        /// <summary>
        /// Writes a list.
        /// </summary>
        /// <param name="value">Collection to write.</param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteList<T>(List<T> value)
        {
            if (value == null)
                WriteList<T>(null, 0, 0);
            else
                WriteList<T>(value, 0, value.Count);
        }

        /// <summary>
        /// Writes a state update packet.
        /// </summary>
        /// <param name="tick"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteStateUpdatePacket(uint lastPacketTick)
        {
            WriteTickUnpacked(lastPacketTick);
        }

        #region Packed writers.
        /// <summary>
        /// ZigZag encode an integer. Move the sign bit to the right.
        /// </summary>
        [CodegenExclude]
        public ulong ZigZagEncode(ulong value)
        {
            if (value >> 63 > 0)
                return ~(value << 1) | 1;
            return value << 1;
        }
        /// <summary>
        /// Writes a packed whole number.
        /// </summary>
        /// <param name="value"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePackedWhole(ulong value)
        {
            if (value < 0x80UL)
            {
                EnsureBufferLength(1);
                _buffer[Position++] = (byte)(value & 0x7F);
            }
            else if (value < 0x4000UL)
            {
                EnsureBufferLength(2);
                _buffer[Position++] = (byte)(0x80 | (value & 0x7F));
                _buffer[Position++] = (byte)((value >> 7) & 0x7F);
            }
            else if (value < 0x200000UL)
            {
                EnsureBufferLength(3);
                _buffer[Position++] = (byte)(0x80 | (value & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 7) & 0x7F));
                _buffer[Position++] = (byte)((value >> 14) & 0x7F);
            }
            else if (value < 0x10000000UL)
            {
                EnsureBufferLength(4);
                _buffer[Position++] = (byte)(0x80 | (value & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 7) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 14) & 0x7F));
                _buffer[Position++] = (byte)((value >> 21) & 0x7F);
            }
            else if (value < 0x100000000UL)
            {
                EnsureBufferLength(5);
                _buffer[Position++] = (byte)(0x80 | (value & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 7) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 14) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 21) & 0x7F));
                _buffer[Position++] = (byte)((value >> 28) & 0x0F);
            }
            else if (value < 0x10000000000UL)
            {
                EnsureBufferLength(6);
                _buffer[Position++] = (byte)(0x80 | (value & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 7) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 14) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 21) & 0x7F));
                _buffer[Position++] = (byte)(0x10 | ((value >> 28) & 0x0F));
                _buffer[Position++] = (byte)((value >> 32) & 0xFF);
            }
            else if (value < 0x1000000000000UL)
            {
                EnsureBufferLength(7);
                _buffer[Position++] = (byte)(0x80 | (value & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 7) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 14) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 21) & 0x7F));
                _buffer[Position++] = (byte)(0x20 | ((value >> 28) & 0x0F));
                _buffer[Position++] = (byte)((value >> 32) & 0xFF);
                _buffer[Position++] = (byte)((value >> 40) & 0xFF);
            }
            else if (value < 0x100000000000000UL)
            {
                EnsureBufferLength(8);
                _buffer[Position++] = (byte)(0x80 | (value & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 7) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 14) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 21) & 0x7F));
                _buffer[Position++] = (byte)(0x30 | ((value >> 28) & 0x0F));
                _buffer[Position++] = (byte)((value >> 32) & 0xFF);
                _buffer[Position++] = (byte)((value >> 40) & 0xFF);
                _buffer[Position++] = (byte)((value >> 48) & 0xFF);
            }
            else
            {
                EnsureBufferLength(9);
                _buffer[Position++] = (byte)(0x80 | (value & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 7) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 14) & 0x7F));
                _buffer[Position++] = (byte)(0x80 | ((value >> 21) & 0x7F));
                _buffer[Position++] = (byte)(0x40 | ((value >> 28) & 0x0F));
                _buffer[Position++] = (byte)((value >> 32) & 0xFF);
                _buffer[Position++] = (byte)((value >> 40) & 0xFF);
                _buffer[Position++] = (byte)((value >> 48) & 0xFF);
                _buffer[Position++] = (byte)((value >> 56) & 0xFF);
            }

            Length = Math.Max(Length, Position);
        }
        #endregion

        #region Generators.
        /// <summary>
        /// Writes a list.
        /// </summary>
        /// <param name="value">Collection to write.</param>
        /// <param name="offset">Offset to begin at.</param>
        /// <param name="count">Entries to write.</param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteList<T>(List<T> value, int offset, int count)
        {
            if (value == null)
            {
                WriteInt32(Writer.UNSET_COLLECTION_SIZE_VALUE);
            }
            else
            {
                //Make sure values cannot cause out of bounds.
                if ((offset + count > value.Count))
                    count = 0;

                WriteInt32(count);
                for (int i = 0; i < count; i++)
                    Write<T>(value[i + offset]);
            }
        }
        /// <summary>
        /// Writes a list.
        /// </summary>
        /// <param name="value">Collection to write.</param>
        /// <param name="offset">Offset to begin at.</param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteList<T>(List<T> value, int offset)
        {
            if (value == null)
                WriteList<T>(null, 0, 0);
            else
                WriteList<T>(value, offset, value.Count - offset);
        }

        /// <summary>
        /// Writes a replication to the server.
        /// </summary>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteReplicate<T>(List<T> values, int offset)
        {
            /* COUNT
             * 
             * Each Entry:
             * 0 if the same as previous.
             * 1 if default. */
            int collectionCount = values.Count;
            //Replicate list will never be null, no need to write null check.
            //Number of entries being written.
            byte count = (byte)(collectionCount - offset);
            WriteByte(count);

            //Get comparer.
            Func<T, T, bool> compareDel = GeneratedComparer<T>.Compare;
            Func<T, bool> isDefaultDel = GeneratedComparer<T>.IsDefault;
            if (compareDel == null || isDefaultDel == null)
            {
                LogError($"ReplicateComparers not found for type {typeof(T).FullName}");
                return;
            }

            T lastData = default;
            /* It's possible to save more bytes by writing that they are all the same.
             * Run a check, and if they are all the same then only write
             * the first data with the same indicator code. */
            byte fullPackType = 0;
            bool repeating = true;
            bool allDefault = true;
            for (int i = offset; i < collectionCount; i++)
            {
                T v = values[i];
                if (!isDefaultDel(v))
                    allDefault = false;

                //Only check if i is larger than offset, giving us something in the past to check.
                if (i > offset)
                {
                    //Not repeating.
                    bool match = compareDel.Invoke(v, values[i - 1]);
                    if (!match)
                    {
                        repeating = false;
                        break;
                    }
                }

            }

            if (allDefault)
                fullPackType = REPLICATE_ALL_DEFAULT_BYTE;
            else if (repeating)
                fullPackType = REPLICATE_REPEATING_BYTE;
            WriteByte(fullPackType);

            //If repeating only write the first entry.
            if (repeating)
            {
                //Only write if not default.
                if (!allDefault)
                    Write<T>(values[offset]);
            }
            //Otherwise check each entry for differences.
            else
            {
                for (int i = offset; i < collectionCount; i++)
                {
                    T v = values[i];
                    bool isDefault = isDefaultDel.Invoke(v);
                    //Default data, easy exit on writes.
                    if (isDefault)
                    {
                        WriteByte(REPLICATE_DEFAULT_BYTE);
                    }
                    //Data is not default.
                    else
                    {
                        //Same as last data.
                        bool match = compareDel.Invoke(v, lastData);
                        if (match)
                        {
                            WriteByte(REPLICATE_DUPLICATE_BYTE);
                        }
                        else
                        {
                            WriteByte(REPLICATE_UNIQUE_BYTE);
                            Write<T>(v);
                            lastData = v;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Writes an array.
        /// </summary>
        /// <param name="value">Collection to write.</param>
        /// <param name="offset">Offset to begin at.</param>
        /// <param name="count">Entries to write.</param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<T>(T[] value, int offset, int count)
        {
            if (value == null)
            {
                WriteInt32(Writer.UNSET_COLLECTION_SIZE_VALUE);
            }
            else
            {
                //If theres no values, or offset exceeds count then write 0 for count.
                if (value.Length == 0 || (offset >= count))
                {
                    WriteInt32(0);
                }
                else
                {
                    WriteInt32(count);
                    for (int i = offset; i < count; i++)
                        Write<T>(value[i]);
                }
            }
        }
        /// <summary>
        /// Writes an array.
        /// </summary>
        /// <param name="value">Collection to write.</param>
        /// <param name="offset">Offset to begin at.</param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<T>(T[] value, int offset)
        {
            if (value == null)
                WriteArray<T>(null, 0, 0);
            else
                WriteArray<T>(value, offset, value.Length - offset);
        }
        /// <summary>
        /// Writes an array.
        /// </summary>
        /// <param name="value">Collection to write.</param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<T>(T[] value)
        {
            if (value == null)
                WriteArray<T>(null, 0, 0);
            else
                WriteArray<T>(value, 0, value.Length);
        }


        /// <summary>
        /// Writers any supported type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T value)
        {
            System.Type type = typeof(T);
            if (IsAutoPackType(type, out AutoPackType packType))
            {
                Action<Writer, T, AutoPackType> del = GenericWriter<T>.WriteAutoPack;
                if (del == null)
                    LogError(GetLogMessage());
                else
                    del.Invoke(this, value, packType);
            }
            else
            {
                Action<Writer, T> del = GenericWriter<T>.Write;
                if (del == null)
                    LogError(GetLogMessage());
                else
                    del.Invoke(this, value);
            }

            string GetLogMessage() => $"Write method not found for {type.FullName}. Use a supported type or create a custom serializer.";
        }

        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="msg"></param>
        private void LogError(string msg)
        {
            if (NetworkManager == null)
                NetworkManager.StaticLogError(msg);
            else
                NetworkManager.LogError(msg);
        }

        /// <summary>
        /// Returns if T takes AutoPackType argument.
        /// </summary>
        /// <param name="packType">Outputs the default pack type for T.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAutoPackType<T>(out AutoPackType packType)
        {
            System.Type type = typeof(T);
            return IsAutoPackType(type, out packType);
        }
        internal static bool IsAutoPackType(Type type, out AutoPackType packType)
        {
            if (WriterExtensions.DefaultPackedTypes.Contains(type))
            {
                packType = AutoPackType.Packed;
                return true;
            }
            else if (type == typeof(float))
            {
                packType = AutoPackType.Unpacked;
                return true;
            }
            else
            {
                packType = AutoPackType.Unpacked;
                return false;
            }
        }
        #endregion

    }
}
