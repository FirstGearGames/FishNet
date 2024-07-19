#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;


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
        #region Types.
        public enum DataSource
        {
            Unset = 0,
            Server = 1,
            Client = 2,
        }
        #endregion

        #region Public.
        /// <summary>
        /// Which part of the network the data came from.
        /// </summary>
        public DataSource Source = DataSource.Unset;
        /// <summary>
        /// Capacity of the buffer.
        /// </summary>
        public int Capacity => _buffer.Length;
        /// <summary>
        /// NetworkManager for this reader. Used to lookup objects.
        /// </summary>
        public NetworkManager NetworkManager;
        /// <summary>
        /// Offset within the buffer when the reader was created.
        /// </summary>
        public int Offset { get; private set; }
        /// <summary>
        /// Position for the next read.
        /// </summary>
        public int Position;
        /// <summary>
        /// Total number of bytes available within the buffer.
        /// </summary>
        public int Length { get; private set; }
        /// <summary>
        /// Bytes remaining to be read. This value is Length - Position.
        /// </summary>
        public int Remaining => ((Length + Offset) - Position);
        #endregion

        #region Internal.
        /// <summary>
        /// NetworkConnection that this data came from.
        /// Value may not always be set.
        /// </summary>
        public NetworkConnection NetworkConnection { get; private set; }
#if DEVELOPMENT
        /// <summary>
        /// Last NetworkObject parsed.
        /// </summary>
        public static NetworkObject LastNetworkObject { get; private set; }
        /// <summary>
        /// Last NetworkBehaviour parsed. 
        /// </summary>
        public static NetworkBehaviour LastNetworkBehaviour { get; private set; }
#endif
        #endregion

        #region Private.
        /// <summary>
        /// Data being read.
        /// </summary>
        private byte[] _buffer;
        #endregion


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Reader(byte[] bytes, NetworkManager networkManager, NetworkConnection networkConnection = null, DataSource source = DataSource.Unset)
        {
            Initialize(bytes, networkManager, networkConnection, source);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Reader(ArraySegment<byte> segment, NetworkManager networkManager, NetworkConnection networkConnection = null, DataSource source = DataSource.Unset)
        {
            Initialize(segment, networkManager, networkConnection, source);
        }

        /// <summary>
        /// Outputs reader to string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Position: {Position}, Length: {Length}, Buffer: {BitConverter.ToString(_buffer, Offset, Length)}.";
        }

        /// <summary>
        /// Outputs reader to string.
        /// </summary>
        /// <returns></returns>
        public string RemainingToString()
        {
            string buffer = (Remaining > 0) ? BitConverter.ToString(_buffer, Position, Remaining) : "null";
            return $"Remaining: {Remaining}, Length: {Length}, Buffer: {buffer}.";
        }

        /// <summary>
        /// Returns remaining data as an ArraySegment.
        /// </summary>
        /// <returns></returns>
        public ArraySegment<byte> GetRemainingData()
        {
            if (Remaining == 0)
                return default;
            else
                return new ArraySegment<byte>(_buffer, Position, Remaining);
        }

        /// <summary>
        /// Initializes this reader with data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Initialize(ArraySegment<byte> segment, NetworkManager networkManager, DataSource source = DataSource.Unset)
        {
            Initialize(segment, networkManager, null, source);
        }

        /// <summary>
        /// Initializes this reader with data.
        /// </summary>
        internal void Initialize(ArraySegment<byte> segment, NetworkManager networkManager, NetworkConnection networkConnection = null, DataSource source = DataSource.Unset)
        {
            _buffer = segment.Array;
            if (_buffer == null)
                _buffer = new byte[0];

            Position = segment.Offset;
            Offset = segment.Offset;
            Length = segment.Count;

            NetworkManager = networkManager;
            NetworkConnection = networkConnection;
            Source = source;
        }

        /// <summary>
        /// Initializes this reader with data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Initialize(byte[] bytes, NetworkManager networkManager, DataSource source = DataSource.Unset)
        {
            Initialize(new ArraySegment<byte>(bytes), networkManager, null, source);
        }
        /// <summary>
        /// Initializes this reader with data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Initialize(byte[] bytes, NetworkManager networkManager, NetworkConnection networkConnection = null, DataSource source = DataSource.Unset)
        {
            Initialize(new ArraySegment<byte>(bytes), networkManager, networkConnection, source);
        }

        /// <summary>
        /// Reads a dictionary.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Dictionary<TKey, TValue> ReadDictionaryAllocated<TKey, TValue>()
        {
            bool isNull = ReadBoolean();
            if (isNull)
                return null;

            int count = ReadInt32();

            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>(count);
            for (int i = 0; i < count; i++)
            {
                TKey key = Read<TKey>();
                TValue value = Read<TValue>();
                result.Add(key, value);
            }

            return result;
        }


        /// <summary>
        /// Reads length. This method is used to make debugging easier.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadLength()
        {
            return ReadInt32();
        }

        /// <summary>
        /// Reads a packetId.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PacketId ReadPacketId()
        {
            return (PacketId)ReadUInt16Unpacked();
        }

        /// <summary>
        /// Returns a ushort without advancing the reader.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PacketId PeekPacketId()
        {
            int currentPosition = Position;
            PacketId result = ReadPacketId();
            Position = currentPosition;
            return result;
        }

        /// <summary>
        /// Returns the next byte to be read.
        /// </summary>
        /// <returns></returns>
        internal byte PeekUInt8()
        {
            return _buffer[Position];
        }

        /// <summary>
        /// Skips a number of bytes in the reader.
        /// </summary>
        /// <param name="value">Number of bytes to skip.</param>

        public void Skip(int value)
        {
            if (value < 1 || Remaining < value)
                return;

            Position += value;
        }
        /// <summary>
        /// Clears remaining bytes to be read.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (Remaining > 0)
                Skip(Remaining);
        }

        /// <summary>
        /// Returns the buffer as an ArraySegment.
        /// </summary>
        /// <returns></returns>
        public ArraySegment<byte> GetArraySegmentBuffer()
        {
            return new ArraySegment<byte>(_buffer, Offset, Length);
        }
        /// <summary>
        /// Returns the buffer as bytes. This does not trim excessive bytes.
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteBuffer()
        {
            return _buffer;
        }
        /// <summary>
        /// Returns the buffer as bytes and allocates into a new array.
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteBufferAllocated()
        {
            byte[] result = new byte[Length];
            Buffer.BlockCopy(_buffer, Offset, result, 0, Length);
            return result;
        }
        /// <summary>
        /// BlockCopies data from the reader to target and advances reader.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="targetOffset"></param>
        /// <param name="count"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlockCopy(ref byte[] target, int targetOffset, int count)
        {
            Buffer.BlockCopy(_buffer, Position, target, targetOffset, count);
            Position += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use ReadUInt8Unpacked.")] //Remove in V5.
        public byte ReadByte() => ReadUInt8Unpacked();
        /// <summary>
        /// Reads a byte.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public byte ReadUInt8Unpacked()
        {
            byte r = _buffer[Position];
            Position += 1;
            return r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use ReadUInt8ArrayAllocated.")]
        public byte[] ReadBytesAllocated(int count) => ReadUInt8ArrayAllocated(count);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use ReadUInt8Array.")]
        public void ReadBytes(ref byte[] buffer, int count) => ReadUInt8Array(ref buffer, count);
        /// <summary>
        /// Read bytes from position into target.
        /// </summary>
        /// <param name="buffer">Buffer to read bytes into.</param>
        /// <param name="count">Number of bytes to read.</param>
        public void ReadUInt8Array(ref byte[] buffer, int count)
        {
            if (buffer == null)
                NetworkManager.LogError($"Buffer cannot be null.");
            else if (count > buffer.Length)
                NetworkManager.LogError($"Count of {count} exceeds target length of {buffer.Length}.");
            else
                BlockCopy(ref buffer, 0, count);
        }

        /// <summary>
        /// Creates an ArraySegment by reading a number of bytes from position.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadArraySegment(int count)
        {
            ArraySegment<byte> result = new ArraySegment<byte>(_buffer, Position, count);
            Position += count;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use ReadInt8Unpacked.")] //Remove in V5.
        public sbyte ReadSByte() => ReadInt8Unpacked();
        /// <summary>
        /// Reads a sbyte.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public sbyte ReadInt8Unpacked() => (sbyte)ReadUInt8Unpacked();

        /// <summary>
        /// Reads a char.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public char ReadChar() => (char)ReadUInt16();

        /// <summary>
        /// Reads a boolean.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public bool ReadBoolean()
        {
            byte result = ReadUInt8Unpacked();
            return (result == 1) ? true : false;
        }

        /// <summary>
        /// Reads an int16.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16Unpacked()
        {
            ushort result = 0;
            result |= _buffer[Position++];
            result |= (ushort)(_buffer[Position++] << 8);

            return result;
        }
        /// <summary>
        /// Reads an int16.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]  //todo: should be using ReadPackedWhole but something relying on unpacked short/ushort is being written packed, corrupting packets.
        [DefaultReader]
        public ushort ReadUInt16() => ReadUInt16Unpacked();
        /// <summary>
        /// Reads a uint16.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]  //todo: should be using ReadPackedWhole but something relying on unpacked short/ushort is being written packed, corrupting packets.
        public short ReadInt16Unpacked() => (short)ReadUInt16Unpacked();
        /// <summary>
        /// Reads a uint16.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]  //todo: should be using ReadPackedWhole but something relying on unpacked short/ushort is being written packed, corrupting packets.
        [DefaultReader]
        public short ReadInt16() => (short)ReadUInt16Unpacked();

        /// <summary>
        /// Reads an int32.
        /// </summary>
        /// <returns></returns> 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32Unpacked()
        {
            uint result = 0;
            result |= _buffer[Position++];
            result |= (uint)_buffer[Position++] << 8;
            result |= (uint)_buffer[Position++] << 16;
            result |= (uint)_buffer[Position++] << 24;

            return result;
        }
        /// <summary>
        /// Reads an int32.
        /// </summary>
        /// <returns></returns> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public uint ReadUInt32() => (uint)ReadUnsignedPackedWhole();

        /// <summary>
        /// Reads a uint32.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32Unpacked() => (int)ReadUInt32Unpacked();
        /// <summary>
        /// Reads a uint32.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public int ReadInt32() => (int)ReadSignedPackedWhole();

        /// <summary>
        /// Reads a uint64.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64Unpacked() => (long)ReadUInt64Unpacked();
        /// <summary>
        /// Reads a uint64.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public long ReadInt64() => (long)ReadSignedPackedWhole();

        /// <summary>
        /// Reads an int64.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64Unpacked()
        {
            ulong result = 0;
            result |= _buffer[Position++];
            result |= (ulong)_buffer[Position++] << 8;
            result |= (ulong)_buffer[Position++] << 16;
            result |= (ulong)_buffer[Position++] << 24;
            result |= (ulong)_buffer[Position++] << 32;
            result |= (ulong)_buffer[Position++] << 40;
            result |= (ulong)_buffer[Position++] << 48;
            result |= (ulong)_buffer[Position++] << 56;

            return result;
        }
        /// <summary>
        /// Reads an int64.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public ulong ReadUInt64() => ReadUnsignedPackedWhole();

        /// <summary>
        /// Reads a single.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingleUnpacked()
        {
            UIntFloat converter = new UIntFloat();
            converter.UIntValue = ReadUInt32Unpacked();
            return converter.FloatValue;
        }
        /// <summary>
        /// Reads a single.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public float ReadSingle() => ReadSingleUnpacked();

        /// <summary>
        /// Reads a double.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDoubleUnpacked()
        {
            UIntDouble converter = new UIntDouble();
            converter.LongValue = ReadUInt64Unpacked();
            return converter.DoubleValue;
        }
        /// <summary>
        /// Reads a double.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public double ReadDouble() => ReadDoubleUnpacked();

        /// <summary>
        /// Reads a decimal.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimalUnpacked()
        {
            UIntDecimal converter = new UIntDecimal();
            converter.LongValue1 = ReadUInt64Unpacked();
            converter.LongValue2 = ReadUInt64Unpacked();
            return converter.DecimalValue;
        }
        /// <summary>
        /// Reads a decimal.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public decimal ReadDecimal() => ReadDecimalUnpacked();

        /// <summary>
        /// Reads a string.
        /// </summary>
        /// <returns></returns>        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public string ReadString()
        {
            int size = ReadInt32();
            //Null string.
            if (size == Writer.UNSET_COLLECTION_SIZE_VALUE)
                return null;
            else if (size == 0)
                return string.Empty;

            if (!CheckAllocationAttack(size))
                return string.Empty;
            ArraySegment<byte> data = ReadArraySegment(size);
            return ReaderStatics.GetString(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use ReadUInt8ArrayAndSizeAllocated.")]
        public byte[] ReadBytesAndSizeAllocated() => ReadUInt8ArrayAndSizeAllocated();
        /// <summary>
        /// Creates a byte array and reads bytes and size into it.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public byte[] ReadUInt8ArrayAndSizeAllocated()
        {
            int size = ReadInt32();
            if (size == Writer.UNSET_COLLECTION_SIZE_VALUE)
                return null;
            else
                return ReadUInt8ArrayAllocated(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use ReadUInt8ArrayAndSize.")]
        public int ReadBytesAndSize(ref byte[] target) => ReadUInt8ArrayAndSize(ref target);
        /// <summary>
        /// Reads bytes and size and copies results into target. Returns UNSET if null was written.
        /// </summary>
        /// <returns>Bytes read.</returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadUInt8ArrayAndSize(ref byte[] target)
        {
            int size = ReadInt32();
            if (size > 0)
                ReadUInt8Array(ref target, size);

            return size;
        }

        /// <summary>
        /// Reads bytes and size and returns as an ArraySegment.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public ArraySegment<byte> ReadArraySegmentAndSize()
        {
            int size = ReadInt32();
            /* UNSET would be written for null. But since
             * ArraySegments cannot be null return default if
             * length is unset or 0. */
            if (size == Writer.UNSET_COLLECTION_SIZE_VALUE)
                return default;

            return ReadArraySegment(size);
        }


        /// <summary>
        /// Reads a Vector2.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ReadVector2Unpacked() => new Vector2(ReadSingleUnpacked(), ReadSingleUnpacked());

        /// <summary>
        /// Reads a Vector2.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Vector2 ReadVector2() => ReadVector2Unpacked();

        /// <summary>
        /// Reads a Vector3.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ReadVector3Unpacked() => new Vector3(ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked());

        /// <summary>
        /// Reads a Vector3.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Vector3 ReadVector3() => ReadVector3Unpacked();

        /// <summary>
        /// Reads a Vector4.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ReadVector4Unpacked() => new Vector4(ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked());

        /// <summary>
        /// Reads a Vector4.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Vector4 ReadVector4() => ReadVector4Unpacked();

        /// <summary>
        /// Reads a Vector2Int.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2Int ReadVector2IntUnpacked() => new Vector2Int(ReadInt32Unpacked(), ReadInt32Unpacked());

        /// <summary>
        /// Reads a Vector2Int.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Vector2Int ReadVector2Int() => new Vector2Int((int)ReadSignedPackedWhole(), (int)ReadSignedPackedWhole());

        /// <summary>
        /// Reads a Vector3Int.
        /// </summary>
        /// <returns></returns>      

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3Int ReadVector3IntUnpacked() => new Vector3Int(ReadInt32Unpacked(), ReadInt32Unpacked(), ReadInt32Unpacked());

        /// <summary>
        /// Reads a Vector3Int.
        /// </summary>
        /// <returns></returns>      
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Vector3Int ReadVector3Int() => new Vector3Int((int)ReadSignedPackedWhole(), (int)ReadSignedPackedWhole(), (int)ReadSignedPackedWhole());

        /// <summary>
        /// Reads a color.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color ReadColorUnpacked()
        {
            float r = ReadSingleUnpacked();
            float g = ReadSingleUnpacked();
            float b = ReadSingleUnpacked();
            float a = ReadSingleUnpacked();

            return new Color(r, g, b, a);
        }
        /// <summary>
        /// Reads a color.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Color ReadColor()
        {
            float r = (float)(ReadUInt8Unpacked() / 100f);
            float g = (float)(ReadUInt8Unpacked() / 100f);
            float b = (float)(ReadUInt8Unpacked() / 100f);
            float a = (float)(ReadUInt8Unpacked() / 100f);

            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Reads a Color32.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Color32 ReadColor32() => new Color32(ReadUInt8Unpacked(), ReadUInt8Unpacked(), ReadUInt8Unpacked(), ReadUInt8Unpacked());

        /// <summary>
        /// Reads a Quaternion.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion ReadQuaternionUnpacked() => new Quaternion(ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked());

        /// <summary>
        /// Reads a Quaternion.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion ReadQuaternion64()
        {
            ulong result = ReadUInt64Unpacked();
            return Quaternion64Compression.Decompress(result);
        }
        /// <summary>
        /// Reads a Quaternion.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Quaternion ReadQuaternion32()
        {
            uint result = ReadUInt32Unpacked();
            return Quaternion32Compression.Decompress(result);
        }
        /// <summary>
        /// Reads a Quaternion.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Quaternion ReadQuaternion(AutoPackType autoPackType)
        {
            switch (autoPackType)
            {
                case AutoPackType.Packed:
                    return ReadQuaternion32();
                case AutoPackType.PackedLess:
                    return ReadQuaternion64();
                default:
                    return ReadQuaternionUnpacked();
            }
        }

        /// <summary>
        /// Reads a Rect.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rect ReadRectUnpacked() => new Rect(ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked());

        /// <summary>
        /// Reads a Rect.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Rect ReadRect() => ReadRectUnpacked();

        /// <summary>
        /// Plane.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Plane ReadPlaneUnpacked() => new Plane(ReadVector3Unpacked(), ReadSingleUnpacked());

        /// <summary>
        /// Plane.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Plane ReadPlane() => ReadPlaneUnpacked();

        /// <summary>
        /// Reads a Ray.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Ray ReadRayUnpacked()
        {
            Vector3 position = ReadVector3Unpacked();
            Vector3 direction = ReadVector3Unpacked();
            return new Ray(position, direction);
        }
        /// <summary>
        /// Reads a Ray.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Ray ReadRay() => ReadRayUnpacked();

        /// <summary>
        /// Reads a Ray.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Ray2D ReadRay2DUnpacked()
        {
            Vector3 position = ReadVector2Unpacked();
            Vector2 direction = ReadVector2Unpacked();
            return new Ray2D(position, direction);
        }
        /// <summary>
        /// Reads a Ray.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Ray2D ReadRay2D() => ReadRay2DUnpacked();

        /// <summary>
        /// Reads a Matrix4x4.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 ReadMatrix4x4Unpacked()
        {
            Matrix4x4 result = new Matrix4x4
            {
                m00 = ReadSingleUnpacked(),
                m01 = ReadSingleUnpacked(),
                m02 = ReadSingleUnpacked(),
                m03 = ReadSingleUnpacked(),
                m10 = ReadSingleUnpacked(),
                m11 = ReadSingleUnpacked(),
                m12 = ReadSingleUnpacked(),
                m13 = ReadSingleUnpacked(),
                m20 = ReadSingleUnpacked(),
                m21 = ReadSingleUnpacked(),
                m22 = ReadSingleUnpacked(),
                m23 = ReadSingleUnpacked(),
                m30 = ReadSingleUnpacked(),
                m31 = ReadSingleUnpacked(),
                m32 = ReadSingleUnpacked(),
                m33 = ReadSingleUnpacked()
            };

            return result;
        }

        /// <summary>
        /// Reads a Matrix4x4.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Matrix4x4 ReadMatrix4x4() => ReadMatrix4x4Unpacked();

        /// <summary>
        /// Creates a new byte array and reads bytes into it.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadUInt8ArrayAllocated(int count)
        {
            byte[] bytes = new byte[count];
            ReadUInt8Array(ref bytes, count);
            return bytes;
        }

        /// <summary>
        /// Reads a Guid.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public System.Guid ReadGuid()
        {
            byte[] buffer = ReaderStatics.GetGuidBuffer();
            ReadUInt8Array(ref buffer, 16);
            return new System.Guid(buffer);
        }

        /// <summary>
        /// Reads a tick without packing.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadTickUnpacked() => ReadUInt32Unpacked();

        /// <summary>
        /// Reads a GameObject.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public GameObject ReadGameObject()
        {
            byte writtenType = ReadUInt8Unpacked();

            GameObject result;
            //Do nothing for 0, as it indicates null.
            if (writtenType == 0)
            {
                result = null;
            }
            //1 indicates a networkObject.
            else if (writtenType == 1)
            {
                NetworkObject nob = ReadNetworkObject();
                result = (nob == null) ? null : nob.gameObject;
            }
            //2 indicates a networkBehaviour.
            else if (writtenType == 2)
            {
                NetworkBehaviour nb = ReadNetworkBehaviour();
                result = (nb == null) ? null : nb.gameObject;
            }
            else
            {
                result = null;
                NetworkManager.LogError($"Unhandled ReadGameObject type of {writtenType}.");
            }

            return result;
        }


        /// <summary>
        /// Reads a Transform.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Transform ReadTransform()
        {
            NetworkObject nob = ReadNetworkObject();
            return (nob == null) ? null : nob.transform;
        }


        /// <summary>
        /// Reads a NetworkObject.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public NetworkObject ReadNetworkObject() => ReadNetworkObject(out _);

        /// <summary>
        /// Reads a NetworkObject.
        /// </summary>
        /// <param name="readSpawningObjects">Objects which have been read to be spawned this tick, but may not have spawned yet.</param>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkObject ReadNetworkObject(out int objectOrPrefabId, HashSet<int> readSpawningObjects = null)
        {
#if DEVELOPMENT
            LastNetworkBehaviour = null;
#endif
            objectOrPrefabId = ReadNetworkObjectId();

            bool isSpawned;
            /* UNSET indicates that the object
             * is null or no PrefabId is set.
             * PrefabIds are set in Awake within
             * the NetworkManager so that should
             * never happen so long as nob isn't null. */
            if (objectOrPrefabId == NetworkObject.UNSET_OBJECTID_VALUE)
                return null;
            else
                isSpawned = ReadBoolean();

            bool isServer = NetworkManager.ServerManager.Started;
            bool isClient = NetworkManager.ClientManager.Started;

            NetworkObject result;
            //Is spawned.
            if (isSpawned)
            {
                result = null;
                /* Try to get the object client side first if client
                 * is running. When acting as a host generally the object
                 * will be available in the server and client list
                 * but there can be occasions where the server side
                 * deinitializes the object, making it unavailable, while
                 * it is still available in the client side. Since FishNet doesn't
                 * use a fake host connection like some lesser solutions the client
                 * has to always be treated as it's own entity. */
                if (isClient)
                    NetworkManager.ClientManager.Objects.Spawned.TryGetValueIL2CPP(objectOrPrefabId, out result);
                //If not found on client and server is running then try server.
                if (result == null && isServer)
                    NetworkManager.ServerManager.Objects.Spawned.TryGetValueIL2CPP(objectOrPrefabId, out result);

                if (result == null && !isServer)
                {
                    if (readSpawningObjects == null || !readSpawningObjects.Contains(objectOrPrefabId))
                        NetworkManager.LogWarning($"Spawned NetworkObject was expected to exist but does not for Id {objectOrPrefabId}. This may occur if you sent a NetworkObject reference which does not exist, be it destroyed or if the client does not have visibility.");
                }
            }
            //Not spawned.
            else
            {
                //Only look up asServer if not client, otherwise use client.
                bool asServer = !isClient;
                //Look up prefab.
                result = NetworkManager.GetPrefab(objectOrPrefabId, asServer);
            }

#if DEVELOPMENT
            LastNetworkObject = result;
#endif
            return result;
        }

        /// <summary>
        /// Reads a NetworkObjectId and nothing else.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadNetworkObjectId() => (int)ReadSignedPackedWhole();


        /// <summary>
        /// Reads the Id for a NetworkObject and outputs spawn settings.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadNetworkObjectForSpawn(out sbyte initializeOrder, out ushort collectionid, out bool spawned)
        {
            int objectId = ReadNetworkObjectId();
            bool isNull = (objectId == NetworkObject.UNSET_OBJECTID_VALUE);
            if (isNull)
            {
                initializeOrder = 0;
                collectionid = 0;
                spawned = false;
            }
            else
            {
                collectionid = ReadUInt16();
                initializeOrder = ReadInt8Unpacked();
                spawned = ReadBoolean();
            }

            return objectId;
        }


        /// <summary>
        /// Reads the Id for a NetworkObject and outputs despawn settings.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadNetworkObjectForDepawn(out DespawnType dt)
        {
            int objectId = ReadNetworkObjectId();
            dt = (DespawnType)ReadUInt8Unpacked();
            return objectId;
        }


        /// <summary>
        /// Reads a NetworkBehaviourId and ObjectId.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte ReadNetworkBehaviourId(out int objectId)
        {
            objectId = ReadNetworkObjectId();
            if (objectId != NetworkObject.UNSET_OBJECTID_VALUE)
                return ReadUInt8Unpacked();
            else
                return 0;
        }

        /// <summary>
        /// Reads a NetworkBehaviour.
        /// </summary>
        /// <param name="readSpawningObjects">Objects which have been read to be spawned this tick, but may not have spawned yet.</param>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkBehaviour ReadNetworkBehaviour(out int objectId, out byte componentIndex, HashSet<int> readSpawningObjects = null)
        {
            NetworkObject nob = ReadNetworkObject(out objectId, readSpawningObjects);
            componentIndex = ReadUInt8Unpacked();

            NetworkBehaviour result;
            if (nob == null)
            {
                result = null;
            }
            else
            {
                if (componentIndex >= nob.NetworkBehaviours.Length)
                {
                    NetworkManager.LogError($"ComponentIndex of {componentIndex} is out of bounds on {nob.gameObject.name} [id {nob.ObjectId}]. This may occur if you have modified your gameObject/prefab without saving it, or the scene.");
                    result = null;
                }
                else
                {
                    result = nob.NetworkBehaviours[componentIndex];
                }
            }

#if DEVELOPMENT
            LastNetworkBehaviour = result;
#endif
            return result;
        }

        /// <summary>
        /// Reads a NetworkBehaviour.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public NetworkBehaviour ReadNetworkBehaviour()
        {
            return ReadNetworkBehaviour(out _, out _);
        }

        /// <summary>
        /// Reads a DateTime.
        /// </summary>
        /// <param name="dt"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public DateTime ReadDateTime()
        {
            long value = (long)ReadSignedPackedWhole();
            DateTime result = DateTime.FromBinary(value);
            return result;
        }


        /// <summary>
        /// Reads a transport channel.
        /// </summary>
        /// <param name="channel"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public Channel ReadChannel()
        {
            return (Channel)ReadUInt8Unpacked();
        }

        /// <summary>
        /// Reads the Id for a NetworkConnection.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadNetworkConnectionId() => (int)ReadSignedPackedWhole();

        /// <summary>
        /// Reads a LayerMask.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public LayerMask ReadLayerMask()
        {
            int layerValue = (int)ReadSignedPackedWhole();
            return (LayerMask)layerValue;
        }

        /// <summary>
        /// Reads a NetworkConnection.
        /// </summary>
        /// <param name="conn"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DefaultReader]
        public NetworkConnection ReadNetworkConnection()
        {
            int value = ReadNetworkConnectionId();
            if (value == NetworkConnection.UNSET_CLIENTID_VALUE)
            {
                return FishNet.Managing.NetworkManager.EmptyConnection;
            }
            else
            {
                //Prefer server.
                if (NetworkManager.IsServerStarted)
                {
                    NetworkConnection result;
                    if (NetworkManager.ServerManager.Clients.TryGetValueIL2CPP(value, out result))
                    {
                        return result;
                    }
                    //If also client then try client side data.
                    else if (NetworkManager.IsClientStarted)
                    {
                        //If found in client collection then return.
                        if (NetworkManager.ClientManager.Clients.TryGetValueIL2CPP(value, out result))
                            return result;
                        /* Otherwise make a new instance.
                         * We do not know if this is for the server or client so
                         * initialize it either way. Connections rarely come through
                         * without being in server/client side collection. */
                        else
                            return new NetworkConnection(NetworkManager, value, -1, true);

                    }
                    //Only server and not found.
                    else
                    {
                        NetworkManager.LogWarning($"Unable to find connection for read Id {value}. An empty connection will be returned.");
                        return FishNet.Managing.NetworkManager.EmptyConnection;
                    }
                }
                //Try client side, will only be able to fetch against local connection.
                else
                {
                    //If value is self then return self.
                    if (value == NetworkManager.ClientManager.Connection.ClientId)
                        return NetworkManager.ClientManager.Connection;
                    //Try client side dictionary.
                    else if (NetworkManager.ClientManager.Clients.TryGetValueIL2CPP(value, out NetworkConnection result))
                        return result;
                    /* Otherwise make a new instance.
                    * We do not know if this is for the server or client so
                    * initialize it either way. Connections rarely come through
                    * without being in server/client side collection. */
                    else
                        return new NetworkConnection(NetworkManager, value, -1, true);
                }

            }
        }

        /// <summary>
        /// Checks if the size could possibly be an allocation attack.
        /// </summary>
        /// <param name="size"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckAllocationAttack(int size)
        {
            /* Possible attacks. Impossible size, or size indicates
            * more elements in collection or more bytes needed
            * than what bytes are available. */
            if (size != Writer.UNSET_COLLECTION_SIZE_VALUE && size < 0)
            {
                NetworkManager.LogError($"Size of {size} is invalid.");
                return false;
            }
            if (size > Remaining)
            {
                NetworkManager.LogError($"Read size of {size} is larger than remaining data of {Remaining}.");
                return false;
            }

            //Checks pass.
            return true;
        }

        /// <summary>
        /// Reads a state update packet.
        /// </summary>
        /// <param name="tick"></param>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReadStateUpdatePacket(out uint clientTick)
        {
            clientTick = ReadTickUnpacked();
        }

        #region Packed readers.        
        /// <summary>
        /// ZigZag decode an integer. Move the sign bit back to the left.
        /// </summary>
        public ulong ZigZagDecode(ulong value)
        {
            ulong sign = value << 63;
            if (sign > 0)
                return ~(value >> 1) | sign;
            return value >> 1;
        }

        /// <summary>
        /// Reads a packed whole number and applies zigzag decoding.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadSignedPackedWhole() => (long)ZigZagDecode(ReadUnsignedPackedWhole());
        /// <summary>
        /// Reads a packed whole number.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUnsignedPackedWhole()
        {
            byte data = ReadUInt8Unpacked();
            ulong result = (ulong)(data & 0x7F);
            if ((data & 0x80) == 0) return result;

            data = ReadUInt8Unpacked();
            result |= (ulong)(data & 0x7F) << 7;
            if ((data & 0x80) == 0) return result;

            data = ReadUInt8Unpacked();
            result |= (ulong)(data & 0x7F) << 14;
            if ((data & 0x80) == 0) return result;

            data = ReadUInt8Unpacked();
            result |= (ulong)(data & 0x7F) << 21;
            if ((data & 0x80) == 0) return result;

            data = ReadUInt8Unpacked();
            result |= (ulong)(data & 0x0F) << 28;
            int extraBytes = data >> 4;

            switch (extraBytes)
            {
                case 0:
                    break;
                case 1:
                    result |= (ulong)ReadUInt8Unpacked() << 32;
                    break;
                case 2:
                    result |= (ulong)ReadUInt8Unpacked() << 32;
                    result |= (ulong)ReadUInt8Unpacked() << 40;
                    break;
                case 3:
                    result |= (ulong)ReadUInt8Unpacked() << 32;
                    result |= (ulong)ReadUInt8Unpacked() << 40;
                    result |= (ulong)ReadUInt8Unpacked() << 48;
                    break;
                case 4:
                    result |= (ulong)ReadUInt8Unpacked() << 32;
                    result |= (ulong)ReadUInt8Unpacked() << 40;
                    result |= (ulong)ReadUInt8Unpacked() << 48;
                    result |= (ulong)ReadUInt8Unpacked() << 56;
                    break;
            }
            return result;
        }
        #endregion

        #region Generators.
        /// <summary>
        /// Reads a replicate into collection and returns item count read.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadReplicate<T>(ref T[] collection, uint tick) where T : IReplicateData
        {
            //Number of entries written.
            int count = (int)ReadUInt8Unpacked();
            if (collection == null || collection.Length < count)
                collection = new T[count];

            /* Subtract count total minus 1
             * from starting tick. This sets the tick to what the first entry would be.
             * EG packet came in as tick 100, so that was passed as tick.
             * if there are 3 replicates then 2 would be subtracted (count - 1).
             * The new tick would be 98.
             * Ticks would be assigned to read values from oldest to 
             * newest as 98, 99, 100. Which is the correct result. In order for this to
             * work properly past replicates cannot skip ticks. This will be ensured
             * in another part of the code. */
            tick -= (uint)(count - 1);

            for (int i = 0; i < count; i++)
            {
                T value = Read<T>();
                //Apply tick.
                value.SetTick(tick + (uint)i);
                //Assign to collection.
                collection[i] = value;
            }


            return count;
        }

        /// <summary>
        /// Reads a list with allocations.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<T> ReadListAllocated<T>()
        {
            List<T> result = null;
            ReadList<T>(ref result);
            return result;
        }

        /// <summary>
        /// Reads into collection and returns item count read.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="allowNullification">True to allow the referenced collection to be nullified when receiving a null collection read.</param>
        /// <returns>Number of values read into the collection. UNSET is returned if the collection were read as null.</returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadList<T>(ref List<T> collection, bool allowNullification = false)
        {
            int count = (int)ReadSignedPackedWhole();
            if (count == Writer.UNSET_COLLECTION_SIZE_VALUE)
            {
                if (allowNullification)
                    collection = null;
                return Writer.UNSET_COLLECTION_SIZE_VALUE;
            }
            else
            {
                if (collection == null)
                    collection = new List<T>(count);
                else
                    collection.Clear();


                for (int i = 0; i < count; i++)
                    collection.Add(Read<T>());

                return count;
            }
        }
        /// <summary>
        /// Reads an array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ReadArrayAllocated<T>()
        {
            T[] result = null;
            ReadArray<T>(ref result);
            return result;
        }
        /// <summary>
        /// Reads into collection and returns amount read.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadArray<T>(ref T[] collection)
        {
            int count = (int)ReadSignedPackedWhole();
            if (count == Writer.UNSET_COLLECTION_SIZE_VALUE)
            {
                return 0;
            }
            else if (count == 0)
            {
                if (collection == null)
                    collection = new T[0];

                return 0;
            }
            else
            {
                //Initialize buffer if not already done.
                if (collection == null)
                    collection = new T[count];
                else if (collection.Length < count)
                    Array.Resize(ref collection, count);

                for (int i = 0; i < count; i++)
                    collection[i] = Read<T>();

                return count;
            }
        }

        /// <summary>
        /// Reads any supported type as packed.
        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>()
        {
            Func<Reader, T> del = GenericReader<T>.Read;
            if (del == null)
            {
                NetworkManager.LogError($"Read method not found for {typeof(T).FullName}. Use a supported type or create a custom serializer.");
                return default;
            }
            else
            {
                return del.Invoke(this);
            }
        }

        #endregion
    }
}
