using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using FishNet.Utility.Performance;
using GameKit.Utilities;
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
    /// Used for read references to generic types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [APIExclude]
    public static class GenericReader<T>
    {
        public static Func<Reader, T> Read { internal get; set; }
        public static Func<Reader, AutoPackType, T> ReadAutoPack { internal get; set; }
    }

    /// <summary>
    /// Reads data from a buffer.
    /// </summary>
    public class Reader
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use ReadDictionaryAllocated.")] //Remove on 2023/06/01
        public Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>()
        {
            return ReadDictionaryAllocated<TKey, TValue>();
        }

        /// <summary>
        /// Reads a dictionary.
        /// </summary>
        [CodegenExclude]
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
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadLength()
        {
            return ReadInt32();
        }

        /// <summary>
        /// Reads a packetId.
        /// </summary>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PacketId ReadPacketId()
        {
            return (PacketId)ReadUInt16();
        }

        /// <summary>
        /// Returns a ushort without advancing the reader.
        /// </summary>
        /// <returns></returns>
        [CodegenExclude]
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
        internal byte PeekByte()
        {
            return _buffer[Position];
        }

        /// <summary>
        /// Skips a number of bytes in the reader.
        /// </summary>
        /// <param name="value">Number of bytes to skip.</param>
        [CodegenExclude]
        public void Skip(int value)
        {
            if (value < 1 || Remaining < value)
                return;

            Position += value;
        }
        /// <summary>
        /// Clears remaining bytes to be read.
        /// </summary>
        [CodegenExclude]
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

        /// <summary>
        /// Reads a byte.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            byte r = _buffer[Position];
            Position += 1;
            return r;
        }

        /// <summary>
        /// Read bytes from position into target.
        /// </summary>
        /// <param name="buffer">Buffer to read bytes into.</param>
        /// <param name="count">Number of bytes to read.</param>
        [CodegenExclude]
        public void ReadBytes(ref byte[] buffer, int count)
        {
            if (buffer == null)
                throw new EndOfStreamException($"Target is null.");
            //Target isn't large enough.
            if (count > buffer.Length)
                throw new EndOfStreamException($"Count of {count} exceeds target length of {buffer.Length}.");

            BlockCopy(ref buffer, 0, count);
        }

        /// <summary>
        /// Creates an ArraySegment by reading a number of bytes from position.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArraySegment<byte> ReadArraySegment(int count)
        {
            ArraySegment<byte> result = new ArraySegment<byte>(_buffer, Position, count);
            Position += count;
            return result;
        }

        /// <summary>
        /// Reads a sbyte.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        /// <summary>
        /// Reads a char.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar() => (char)ReadUInt16();

        /// <summary>
        /// Reads a boolean.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean()
        {
            byte result = ReadByte();
            return (result == 1) ? true : false;
        }

        /// <summary>
        /// Reads an int16.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            ushort result = 0;
            result |= _buffer[Position++];
            result |= (ushort)(_buffer[Position++] << 8);

            return result;
        }

        /// <summary>
        /// Reads a uint16.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16() => (short)ReadUInt16();

        /// <summary>
        /// Reads an int32.
        /// </summary>
        /// <returns></returns> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32(AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
                return (uint)ReadPackedWhole();

            uint result = 0;
            result |= _buffer[Position++];
            result |= (uint)_buffer[Position++] << 8;
            result |= (uint)_buffer[Position++] << 16;
            result |= (uint)_buffer[Position++] << 24;

            return result;
        }
        /// <summary>
        /// Reads a uint32.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32(AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
                return (int)(long)ZigZagDecode(ReadPackedWhole());

            return (int)ReadUInt32(packType);
        }

        /// <summary>
        /// Reads a uint64.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64(AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
                return (long)ZigZagDecode(ReadPackedWhole());

            return (long)ReadUInt64(packType);
        }

        /// <summary>
        /// Reads an int64.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64(AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
                return (ulong)ReadPackedWhole();

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
        /// Reads a single.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingle(AutoPackType packType = AutoPackType.Unpacked)
        {
            if (packType == AutoPackType.Unpacked)
            {
                UIntFloat converter = new UIntFloat();
                converter.UIntValue = ReadUInt32(AutoPackType.Unpacked);
                return converter.FloatValue;
            }
            else
            {
                long converter = (long)ReadPackedWhole();
                return (float)(converter / 100f);
            }
        }

        /// <summary>
        /// Reads a double.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            UIntDouble converter = new UIntDouble();
            converter.LongValue = ReadUInt64(AutoPackType.Unpacked);
            return converter.DoubleValue;
        }

        /// <summary>
        /// Reads a decimal.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal()
        {
            UIntDecimal converter = new UIntDecimal();
            converter.LongValue1 = ReadUInt64(AutoPackType.Unpacked);
            converter.LongValue2 = ReadUInt64(AutoPackType.Unpacked);
            return converter.DecimalValue;
        }

        /// <summary>
        /// Reads a string.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        /// <summary>
        /// Creates a byte array and reads bytes and size into it.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytesAndSizeAllocated()
        {
            int size = ReadInt32();
            if (size == Writer.UNSET_COLLECTION_SIZE_VALUE)
                return null;
            else
                return ReadBytesAllocated(size);
        }

        /// <summary>
        /// Reads bytes and size and copies results into target. Returns UNSET if null was written.
        /// </summary>
        /// <returns>Bytes read.</returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadBytesAndSize(ref byte[] target)
        {
            int size = ReadInt32();
            if (size > 0)
                ReadBytes(ref target, size);

            return size;
        }

        /// <summary>
        /// Reads bytes and size and returns as an ArraySegment.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        public Vector2 ReadVector2()
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        /// <summary>
        /// Reads a Vector3.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ReadVector3()
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        /// <summary>
        /// Reads a Vector4.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ReadVector4()
        {
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        /// <summary>
        /// Reads a Vector2Int.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2Int ReadVector2Int(AutoPackType packType = AutoPackType.Packed)
        {
            return new Vector2Int(ReadInt32(packType), ReadInt32(packType));
        }

        /// <summary>
        /// Reads a Vector3Int.
        /// </summary>
        /// <returns></returns>      
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3Int ReadVector3Int(AutoPackType packType = AutoPackType.Packed)
        {
            return new Vector3Int(ReadInt32(packType), ReadInt32(packType), ReadInt32(packType));
        }

        /// <summary>
        /// Reads a color.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color ReadColor(AutoPackType packType = AutoPackType.Packed)
        {
            float r, g, b, a;
            if (packType == AutoPackType.Unpacked)
            {
                r = ReadSingle();
                g = ReadSingle();
                b = ReadSingle();
                a = ReadSingle();
            }
            else
            {
                r = (float)(ReadByte() / 100f);
                g = (float)(ReadByte() / 100f);
                b = (float)(ReadByte() / 100f);
                a = (float)(ReadByte() / 100f);
            }
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Reads a Color32.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color32 ReadColor32()
        {
            return new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());
        }

        /// <summary>
        /// Reads a Quaternion.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion ReadQuaternion(AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
            {
                uint result = ReadUInt32(AutoPackType.Unpacked);
                return Quaternion32Compression.Decompress(result);
            }
            else if (packType == AutoPackType.PackedLess)
            {
                ulong result = ReadUInt64(AutoPackType.Unpacked);
                return Quaternion64Compression.Decompress(result);
            }
            else
            {
                return new Quaternion(
                    ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle()
                    );
            }
        }

        /// <summary>
        /// Reads a Rect.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rect ReadRect()
        {
            return new Rect(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        /// <summary>
        /// Plane.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Plane ReadPlane()
        {
            return new Plane(ReadVector3(), ReadSingle());
        }

        /// <summary>
        /// Reads a Ray.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Ray ReadRay()
        {
            Vector3 position = ReadVector3();
            Vector3 direction = ReadVector3();
            return new Ray(position, direction);
        }

        /// <summary>
        /// Reads a Ray.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Ray2D ReadRay2D()
        {
            Vector3 position = ReadVector2();
            Vector2 direction = ReadVector2();
            return new Ray2D(position, direction);
        }

        /// <summary>
        /// Reads a Matrix4x4.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 ReadMatrix4x4()
        {
            Matrix4x4 result = new Matrix4x4
            {
                m00 = ReadSingle(),
                m01 = ReadSingle(),
                m02 = ReadSingle(),
                m03 = ReadSingle(),
                m10 = ReadSingle(),
                m11 = ReadSingle(),
                m12 = ReadSingle(),
                m13 = ReadSingle(),
                m20 = ReadSingle(),
                m21 = ReadSingle(),
                m22 = ReadSingle(),
                m23 = ReadSingle(),
                m30 = ReadSingle(),
                m31 = ReadSingle(),
                m32 = ReadSingle(),
                m33 = ReadSingle()
            };

            return result;
        }

        /// <summary>
        /// Creates a new byte array and reads bytes into it.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytesAllocated(int count)
        {
            byte[] bytes = new byte[count];
            ReadBytes(ref bytes, count);
            return bytes;
        }

        /// <summary>
        /// Reads a Guid.
        /// </summary>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public System.Guid ReadGuid()
        {
            byte[] buffer = ReaderStatics.GetGuidBuffer();
            ReadBytes(ref buffer, 16);
            return new System.Guid(buffer);
        }

        /// <summary>
        /// Reads a tick without packing.
        /// </summary>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadTickUnpacked()
        {
            return ReadUInt32(AutoPackType.Unpacked);
        }

        /// <summary>
        /// Reads a GameObject.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameObject ReadGameObject()
        {
            byte writtenType = ReadByte();

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
                LogError($"Unhandled ReadGameObject type of {writtenType}.");
            }

            return result;
        }


        /// <summary>
        /// Reads a Transform.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        public NetworkObject ReadNetworkObject()
        {
            return ReadNetworkObject(out _);
        }

        /// <summary>
        /// Reads a NetworkObject.
        /// </summary>
        /// <param name="readSpawningObjects">Objects which have been read to be spawned this tick, but may not have spawned yet.</param>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkObject ReadNetworkObject(out int objectOrPrefabId, HashSet<int> readSpawningObjects = null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
                        LogWarning($"Spawned NetworkObject was expected to exist but does not for Id {objectOrPrefabId}. This may occur if you sent a NetworkObject reference which does not exist, be it destroyed or if the client does not have visibility.");
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastNetworkObject = result;
#endif
            return result;
        }

        /// <summary>
        /// Reads a NetworkObjectId and nothing else.
        /// </summary>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadNetworkObjectId()
        {
            return ReadUInt16();
        }

        /// <summary>
        /// Reads the Id for a NetworkObject and outputs spawn settings.
        /// </summary>
        /// <returns></returns>
        [CodegenExclude]
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
                initializeOrder = ReadSByte();
                spawned = ReadBoolean();
            }

            return objectId;
        }


        /// <summary>
        /// Reads the Id for a NetworkObject and outputs despawn settings.
        /// </summary>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadNetworkObjectForDepawn(out DespawnType dt)
        {
            int objectId = ReadNetworkObjectId();
            dt = (DespawnType)ReadByte();
            return objectId;
        }


        /// <summary>
        /// Reads a NetworkBehaviourId and ObjectId.
        /// </summary>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte ReadNetworkBehaviourId(out int objectId)
        {
            objectId = ReadNetworkObjectId();
            if (objectId != NetworkObject.UNSET_OBJECTID_VALUE)
                return ReadByte();
            else
                return 0;
        }

        /// <summary>
        /// Reads a NetworkBehaviour.
        /// </summary>
        /// <param name="readSpawningObjects">Objects which have been read to be spawned this tick, but may not have spawned yet.</param>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkBehaviour ReadNetworkBehaviour(out int objectId, out byte componentIndex, HashSet<int> readSpawningObjects = null)
        {
            NetworkObject nob = ReadNetworkObject(out objectId, readSpawningObjects);
            componentIndex = ReadByte();

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastNetworkBehaviour = result;
#endif
            return result;
        }

        /// <summary>
        /// Reads a NetworkBehaviour.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkBehaviour ReadNetworkBehaviour()
        {
            return ReadNetworkBehaviour(out _, out _);
        }

        /// <summary>
        /// Reads a DateTime.
        /// </summary>
        /// <param name="dt"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTime ReadDateTime()
        {
            DateTime result = DateTime.FromBinary(ReadInt64());
            return result;
        }


        /// <summary>
        /// Reads a transport channel.
        /// </summary>
        /// <param name="channel"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Channel ReadChannel()
        {
            return (Channel)ReadByte();
        }

        /// <summary>
        /// Reads the Id for a NetworkConnection.
        /// </summary>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadNetworkConnectionId()
        {
            return ReadInt16();
        }

        /// <summary>
        /// Writes a NetworkConnection.
        /// </summary>
        /// <param name="conn"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                if (NetworkManager.IsServer)
                {
                    NetworkConnection result;
                    if (NetworkManager.ServerManager.Clients.TryGetValueIL2CPP(value, out result))
                    {
                        return result;
                    }
                    //If also client then try client side data.
                    else if (NetworkManager.IsClient)
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
        /// Writes a state update packet.
        /// </summary>
        /// <param name="tick"></param>
        [CodegenExclude]
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
        /// Reads a packed whole number.
        /// </summary>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadPackedWhole()
        {
            byte data = ReadByte();
            ulong result = (ulong)(data & 0x7F);
            if ((data & 0x80) == 0) return result;

            data = ReadByte();
            result |= (ulong)(data & 0x7F) << 7;
            if ((data & 0x80) == 0) return result;

            data = ReadByte();
            result |= (ulong)(data & 0x7F) << 14;
            if ((data & 0x80) == 0) return result;

            data = ReadByte();
            result |= (ulong)(data & 0x7F) << 21;
            if ((data & 0x80) == 0) return result;

            data = ReadByte();
            result |= (ulong)(data & 0x0F) << 28;
            int extraBytes = data >> 4;

            switch (extraBytes)
            {
                case 0:
                    break;
                case 1:
                    result |= (ulong)ReadByte() << 32;
                    break;
                case 2:
                    result |= (ulong)ReadByte() << 32;
                    result |= (ulong)ReadByte() << 40;
                    break;
                case 3:
                    result |= (ulong)ReadByte() << 32;
                    result |= (ulong)ReadByte() << 40;
                    result |= (ulong)ReadByte() << 48;
                    break;
                case 4:
                    result |= (ulong)ReadByte() << 32;
                    result |= (ulong)ReadByte() << 40;
                    result |= (ulong)ReadByte() << 48;
                    result |= (ulong)ReadByte() << 56;
                    break;
            }
            return result;
        }
        #endregion

        #region Generators.
        /// <summary>
        /// Reads a replicate into collection and returns item count read.
        /// </summary>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int ReadReplicate<T>(ref T[] collection, uint tick) where T : IReplicateData
        {
            //Number of entries written.
            int count = (int)ReadByte();
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

            int fullPackType = ReadByte();
            //Read once and apply to all entries.
            if (fullPackType > 0)
            {
                T value;
                if (fullPackType == Writer.REPLICATE_ALL_DEFAULT_BYTE)
                {
                    value = default(T);
                }
                else if (fullPackType == Writer.REPLICATE_REPEATING_BYTE)
                {
                    value = Read<T>();
                }
                else
                {
                    value = default(T);
                    NetworkManager?.LogError($"Unhandled Replicate pack type {fullPackType}.");
                }

                for (int i = 0; i < count; i++)
                {
                    collection[i] = value;
                    collection[i].SetTick(tick + (uint)i);
                }
            }
            //Values vary, read each indicator.
            else
            {
                T lastData = default;

                for (int i = 0; i < count; i++)
                {
                    T value = default;
                    byte indicatorB = ReadByte();
                    if (indicatorB == Writer.REPLICATE_DUPLICATE_BYTE)
                    {
                        value = lastData;
                    }
                    else if (indicatorB == Writer.REPLICATE_UNIQUE_BYTE)
                    {
                        value = Read<T>();
                        lastData = value;
                    }
                    else if (indicatorB == Writer.REPLICATE_DEFAULT_BYTE)
                    {
                        value = default(T);
                    }

                    //Apply tick.
                    value.SetTick(tick + (uint)i);
                    //Assign to collection.
                    collection[i] = value;
                }
            }

            return count;
        }

        /// <summary>
        /// Reads a ListCache with allocations.
        /// </summary>
        [CodegenExclude]  //Remove on 2024/01/01.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable CS0618 // Type or member is obsolete
        public ListCache<T> ReadListCacheAllocated<T>()
        {
            List<T> lst = ReadListAllocated<T>();
            ListCache<T> lc = new ListCache<T>();
            lc.Collection = lst;
            return lc;
        }
        /// <summary>
        /// Reads a ListCache and returns the item count read.
        /// </summary>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]  //Remove on 2024/01/01.
        public int ReadListCache<T>(ref ListCache<T> listCache)
        {
            listCache.Collection = ReadListAllocated<T>();
            return listCache.Collection.Count;
        }
#pragma warning restore CS0618 // Type or member is obsolete
        /// <summary>
        /// Reads a list with allocations.
        /// </summary>
        [CodegenExclude]
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
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadList<T>(ref List<T> collection, bool allowNullification = false)
        {
            int count = ReadInt32();
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
        [CodegenExclude]
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
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadArray<T>(ref T[] collection)
        {
            int count = ReadInt32();
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
        /// Reads any supported type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [CodegenExclude]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>()
        {
            System.Type type = typeof(T);
            if (IsAutoPackType(type, out AutoPackType packType))
            {
                Func<Reader, AutoPackType, T> autopackDel = GenericReader<T>.ReadAutoPack;
                if (autopackDel == null)
                {
                    LogError(GetLogMessage());
                    return default;
                }
                else
                {
                    return autopackDel.Invoke(this, packType);
                }
            }
            else
            {
                Func<Reader, T> del = GenericReader<T>.Read;
                if (del == null)
                {
                    LogError(GetLogMessage());
                    return default;
                }
                else
                {
                    return del.Invoke(this);
                }
            }

            string GetLogMessage() => $"Read method not found for {type.FullName}. Use a supported type or create a custom serializer.";
        }

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="msg"></param>
        private void LogWarning(string msg)
        {
            if (NetworkManager == null)
                NetworkManager.StaticLogWarning(msg);
            else
                NetworkManager.LogWarning(msg);
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
        internal bool IsAutoPackType<T>(out AutoPackType packType) => Writer.IsAutoPackType<T>(out packType);
        /// <summary>
        /// Returns if T takes AutoPackType argument.
        /// </summary>
        /// <param name="packType">Outputs the default pack type for T.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsAutoPackType(Type type, out AutoPackType packType) => Writer.IsAutoPackType(type, out packType);
        #endregion
    }
}
