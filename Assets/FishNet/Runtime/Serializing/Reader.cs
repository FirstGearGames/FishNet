using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;


[assembly: InternalsVisibleTo(UtilityConstants.GENERATED_ASSEMBLY_NAME)]
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
        #region Public.
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

        #region Private.
        /// <summary>
        /// Data being read.
        /// </summary>
        private byte[] _buffer;
        /// <summary>
        /// NetworkManager for this reader. Used to lookup objects.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Buffer to copy Guids into.
        /// </summary>
        private byte[] _guidBuffer = new byte[16];
        /// <summary>
        /// Used to encode strings.
        /// </summary>
        private readonly UTF8Encoding _encoding = new UTF8Encoding(false, true);
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Reader(byte[] bytes, NetworkManager networkManager)
        {
            Initialize(bytes, networkManager);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Reader(ArraySegment<byte> segment, NetworkManager networkManager)
        {
            Initialize(segment, networkManager);
        }

        /// <summary>
        /// Outputs reader to string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Reader pos={Position} len={Length} buffer={BitConverter.ToString(_buffer, Offset, Length)}";
        }

        /// <summary>
        /// Initializes this reader with data.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="networkManager"></param>
        internal void Initialize(ArraySegment<byte> bytes, NetworkManager networkManager)
        {
            _buffer = bytes.Array;
            Position = bytes.Offset;
            Offset = bytes.Offset;
            Length = bytes.Count;
            _networkManager = networkManager;
        }
        /// <summary>
        /// Initializes this reader with data.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="networkManager"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Initialize(byte[] bytes, NetworkManager networkManager)
        {
            Initialize(new ArraySegment<byte>(bytes), networkManager);
        }


        /// <summary>
        /// Writes a dictionary.
        /// </summary>
        public Dictionary<TKey,TValue> ReadDictionary<TKey, TValue>()
        {
            bool isNull = ReadBoolean();
            if (isNull)
                return null;

            Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();
            int count = ReadInt32();
            for (int i = 0; i < count; i++)
            {
                TKey key = Read<TKey>();
                TValue value = Read<TValue>();
                result.Add(key, value);
            }

            return result;
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
        /// Skips a number of bytes in the reader.
        /// </summary>
        /// <param name="value"></param>
        [CodegenExclude]
        public void Skip(int value)
        {
            if (value < 1 || Remaining < value)
                return;

            Position += value;
        }
        ///// <summary>
        ///// Creates a debug print for end of stream.
        ///// </summary>
        //private void LogEndOfStream()
        //{
        //    if (_networkManager.CanLog(LoggingType.Error))
        //        Debug.LogError("Read length is out of range.");
        //}
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
            //if (Remaining < count)
            //    LogEndOfStream();

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
            //if (Remaining < 1)
            //    LogEndOfStream();

            byte r = _buffer[Position];
            Position += 1;
            return r;
        }

        /// <summary>
        /// Read bytes from position into target.
        /// </summary>
        /// <returns><paramref name="target"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenExclude]
        public void ReadBytes(ref byte[] target, int count)
        {
            if (target == null)
                throw new EndOfStreamException($"Target is null.");
            //Target isn't large enough.
            if (count > target.Length)
                throw new EndOfStreamException($"Count of {count} exceeds target length of {target.Length}.");

            BlockCopy(ref target, 0, count);
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
            //if (Remaining < count)
            //    LogEndOfStream();

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
            //if (Remaining < 2)
            //    LogEndOfStream();

            ushort result = 0;
            result |= _buffer[Position++];
            result |= (ushort)(_buffer[Position++] << 8);

            return result;
        }

        /// <summary>
        /// Reads a uint16.
        /// </summary>
        /// <returns></returns>       
        public short ReadInt16() => (short)ReadUInt16();

        /// <summary>
        /// Reads an int32.
        /// </summary>
        /// <returns></returns>        
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
        public int ReadInt32(AutoPackType packType = AutoPackType.Packed) => (int)ReadUInt32(packType);

        /// <summary>
        /// Reads an int64.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64(AutoPackType packType = AutoPackType.Packed)
        {
            if (packType == AutoPackType.Packed)
                return (ulong)ReadPackedWhole();
            //if (Remaining < 8)
            //    LogEndOfStream();

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
        /// Reads a uint64.
        /// </summary>
        /// <returns></returns>
        public long ReadInt64(AutoPackType packType = AutoPackType.Packed) => (long)ReadUInt64(packType);

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
            if (size == -1)
                return null;
            else if (size == 0)
                return string.Empty;

            if (!CheckAllocationAttack(size))
                return string.Empty;
            ArraySegment<byte> data = ReadArraySegment(size);
            return _encoding.GetString(data.Array, data.Offset, data.Count);
        }

        /// <summary>
        /// Creates a byte array and reads bytes and size into it.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytesAndSizeAllocated()
        {
            int size = ReadInt32();
            if (size == -1)
                return null;
            else
                return ReadBytesAllocated(size);
        }

        /// <summary>
        /// Reads bytes and size and copies results into target. Returns -1 if null was written.
        /// </summary>
        /// <returns>Bytes read.</returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenExclude]
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
            /* -1 would be written for null. But since
             * ArraySegments cannot be null return default if
             * length is 0 or less. */
            if (size <= 0)
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
        public Quaternion ReadQuaternion()
        {
            uint result = ReadUInt32(AutoPackType.Unpacked);
            return Quaternions.Decompress(result);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenExclude]
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
            ReadBytes(ref _guidBuffer, 16);
            return new System.Guid(_guidBuffer);
        }


        /// <summary>
        /// Reads a GameObject.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameObject ReadGameObject()
        {
            NetworkObject nob = ReadNetworkObject();
            if (nob == null)
                throw new ArgumentException($"NetworkObject is null.");
            else
                return nob.gameObject;
        }

        /// <summary>
        /// Reads a NetworkObject.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkObject ReadNetworkObject()
        {
            int objectId = ReadInt16();
            if (objectId == -1)
                return null;

            NetworkObject result = null;
            /* Try to get the object client side first if client
             * is running. When acting as a host generally the object
             * will be available in the server and client list
             * but there can be occasions where the server side
             * deinitializes the object, making it unavailable, while
             * it is still available in the client side. Since FishNet doesn't
             * use a fake host connection like some lesser solutions the client
             * has to always be treated as it's own entity. */
            if (_networkManager.ClientManager.Started)
                _networkManager.ClientManager.Objects.Spawned.TryGetValue(objectId, out result);
            //If not found on client and server is running then try server.
            if (result == null && _networkManager.ServerManager.Started)
                _networkManager.ServerManager.Objects.Spawned.TryGetValue(objectId, out result);

            /* Do not error if not found because packet could
             * have been sent unreliably and arrived after the object
             * was destroyed. //improvement opportunity. */
            return result;
        }


        /// <summary>
        /// Reads a NetworkObject.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenExclude]
        public NetworkObject ReadNetworkObject(out int objectId)
        {
            objectId = ReadInt16();
            if (objectId == -1)
                return null;

            NetworkObject result = null;
            /* Try to get the object client side first if client
             * is running. When acting as a host generally the object
             * will be available in the server and client list
             * but there can be occasions where the server side
             * deinitializes the object, making it unavailable, while
             * it is still available in the client side. Since FishNet doesn't
             * use a fake host connection like some lesser solutions the client
             * has to always be treated as it's own entity. */
            if (_networkManager.ClientManager.Started)
                _networkManager.ClientManager.Objects.Spawned.TryGetValue(objectId, out result);
            //If not found on client and server is running then try server.
            if (result == null && _networkManager.ServerManager.Started)
                _networkManager.ServerManager.Objects.Spawned.TryGetValue(objectId, out result);

            /* Do not error if not found because packet could
             * have been sent unreliably and arrived after the object
             * was destroyed. //improvement opportunity. */
            return result;
        }

        /// <summary>
        /// Reads a NetworkBehaviour.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenExclude]
        public NetworkBehaviour ReadNetworkBehaviour(out int objectId, out byte componentIndex)
        {
            NetworkObject nob = ReadNetworkObject(out objectId);
            if (nob == null)
            {
                //Clear out the byte even if nob is null.
                componentIndex = ReadByte();
                return null;
            }
            else
            {
                componentIndex = ReadByte();
                if (componentIndex < 0 || componentIndex >= nob.NetworkBehaviours.Length)
                {
                    if (_networkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"ComponentIndex of {componentIndex} is out of bounds on {nob.gameObject.name} [id {nob.ObjectId}] . This may occur if you have modified your gameObject/prefab without saving it, or the scene.");
                    return null;
                }
                else
                {
                    return nob.NetworkBehaviours[componentIndex];
                }
            }
        }

        /// <summary>
        /// Reads a NetworkBehaviour.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkBehaviour ReadNetworkBehaviour()
        {
            NetworkObject nob = ReadNetworkObject();
            if (nob == null)
            {
                //Clear out the byte even if nob is null.
                ReadByte();
                return null;
            }
            else
            {
                byte componentIndex = ReadByte();
                if (componentIndex < 0 || componentIndex >= nob.NetworkBehaviours.Length)
                {
                    if (_networkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"ComponentIndex of {componentIndex} is out of bounds on {nob.gameObject.name}, id {nob.ObjectId}. This may occur if you have modified your gameObject/prefab without saving it, or the scene.");
                    return null;
                }
                else
                {
                    return nob.NetworkBehaviours[componentIndex];
                }
            }
        }

        /// <summary>
        /// Writes a transport channel.
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenExclude]
        public int ReadNetworkConnectionId()
        {
            return ReadInt16();
        }

        /// <summary>
        /// Reads the Id for a NetworkObject.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenExclude]
        public int ReadNetworkObjectId()
        {
            return ReadInt16();
        }

        /// <summary>
        /// Writes a NetworkConnection.
        /// </summary>
        /// <param name="conn"></param>
        public NetworkConnection ReadNetworkConnection()
        {
            int value = ReadInt16();
            if (value == -1)
            {
                return FishNet.Managing.NetworkManager.EmptyConnection;
            }
            else
            {
                //Prefer server.
                if (_networkManager.IsServer)
                {
                    if (_networkManager.ServerManager.Clients.TryGetValue((int)value, out NetworkConnection result))
                    {
                        return result;
                    }
                    else
                    {
                        if (_networkManager.CanLog(LoggingType.Warning))
                            Debug.LogWarning($"Unable to find connection for read Id {value}. An empty connection will be returned.");
                        return FishNet.Managing.NetworkManager.EmptyConnection;
                    }
                }
                //Try client side, will only be able to fetch against local connection.
                else
                {
                    //If value is self then return self.
                    if (value == _networkManager.ClientManager.Connection.ClientId)
                        return _networkManager.ClientManager.Connection;
                    //Otherwise return a new connection.
                    else
                        return new NetworkConnection(_networkManager, (int)value);
                }

            }
        }

        /// <summary>
        /// Checks if the size could possibly be an allocation attack.
        /// </summary>
        /// <param name="size"></param>
        private bool CheckAllocationAttack(int size)
        {
            /* Possible attacks. Impossible size, or size indicates
            * more elements in collection or more bytes needed
            * than what bytes are available. */
            if (size < -1)
            {
                if (_networkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Size of {size} is invalid.");
                return false;
            }
            if (size > Remaining)
            {
                if (_networkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Read size of {size} is larger than remaining data of {Remaining}.");
                return false;
            }

            //Checks pass.
            return true;
        }


        #region Packed readers.        
        /// <summary>
        /// Reads a packed whole number.
        /// </summary>
        [CodegenExclude]
        public ulong ReadPackedWhole()
        {
            //if (Remaining < 1)
            //    LogEndOfStream();

            PackRate pr = (PackRate)_buffer[Position++];

            if (pr == PackRate.OneByte)
            {
                return ReadByte();
            }
            else if (pr == PackRate.TwoBytes)
            {
                return ReadUInt16();
            }
            else if (pr == PackRate.FourBytes)
            {
                return ReadUInt32(AutoPackType.Unpacked);
            }
            else if (pr == PackRate.EightBytes)
            {
                return ReadUInt64(AutoPackType.Unpacked);
            }
            else
            {
                if (_networkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Unhandled PackRate of {pr}.");
                return 0;
            }
        }
        #endregion

        #region Generators.
        /// <summary>
        /// Reads into collection and returns amount read.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        [CodegenExclude]
        public int ReadToCollection<T>(ref T[] collection)
        {
            int count = ReadInt32();
            if (count <= 0)
            {
                return count;
            }
            else
            {
                //Initialize buffer if not already done.
                if (collection == null || collection.Length < count)
                    collection = new T[count];

                for (int i = 0; i < count; i++)
                    collection[i] = Read<T>();
            }

            return count;
        }
     
        /// <summary>
        /// Reads any supported type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Read<T>()
        {
            if (IsDefaultAutoPack<T>(out AutoPackType packType))
            {
                Func<Reader, AutoPackType, T> del = GenericReader<T>.ReadAutoPack;
                if (del == null)
                {
                    if (_networkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"Read method not found for {typeof(T).Name}. Use a supported type or create a custom serializer.");
                    return default;
                }
                else
                {
                    return del.Invoke(this, packType);
                }
            }
            else
            {
                Func<Reader, T> del = GenericReader<T>.Read;
                if (del == null)
                {
                    if (_networkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"Read method not found for {typeof(T).Name}. Use a supported type or create a custom serializer.");
                    return default;
                }
                else
                {
                    return del.Invoke(this);
                }
            }

        }

        /// <summary>
        /// Returns if T takes AutoPackType argument.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packType"></param>
        /// <returns></returns>
        internal bool IsDefaultAutoPack<T>(out AutoPackType packType) => Writer.IsDefaultAutoPack<T>(out packType);
        #endregion
    }
}
