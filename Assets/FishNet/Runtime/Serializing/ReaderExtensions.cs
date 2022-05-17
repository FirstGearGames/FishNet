using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Object;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Serializing
{
    /// <summary>
    /// Extensions to Read methods. Used by Read<T>.
    /// Internal use.
    /// </summary>
    [APIExclude]
    public static class ReaderExtensions
    {

        public static byte ReadByte(this Reader reader) => reader.ReadByte();
        [CodegenExclude]
        public static void ReadBytes(this Reader reader, ref byte[] target, int count) => reader.ReadBytes(ref target, count);
        public static byte[] ReadBytesAndSizeAllocated(this Reader reader) => reader.ReadBytesAndSizeAllocated();
        [CodegenExclude]
        public static int ReadBytesAndSize(this Reader reader, ref byte[] target) => reader.ReadBytesAndSize(ref target);
        [CodegenExclude]
        public static ArraySegment<byte> ReadArraySegment(this Reader reader, int count) => reader.ReadArraySegment(count);
        public static ArraySegment<byte> ReadArraySegmentAndSize(this Reader reader) => reader.ReadArraySegmentAndSize();
        public static sbyte ReadSByte(this Reader reader) => reader.ReadSByte();
        public static char ReadChar(this Reader reader) => reader.ReadChar();
        public static bool ReadBoolean(this Reader reader) => reader.ReadBoolean();
        public static short ReadInt16(this Reader reader) => reader.ReadInt16();
        public static ushort ReadUInt16(this Reader reader) => reader.ReadUInt16();
        public static int ReadInt32(this Reader reader, AutoPackType packType = AutoPackType.Packed) => reader.ReadInt32(packType);
        public static uint ReadUInt32(this Reader reader, AutoPackType packType = AutoPackType.Packed) => reader.ReadUInt32(packType);
        public static long ReadInt64(this Reader reader, AutoPackType packType = AutoPackType.Packed) => reader.ReadInt64(packType);
        public static ulong ReadUInt64(this Reader reader, AutoPackType packType = AutoPackType.Packed) => reader.ReadUInt64(packType);
        public static float ReadSingle(this Reader reader, AutoPackType packType = AutoPackType.Unpacked) => reader.ReadSingle(packType);
        public static double ReadDouble(this Reader reader) => reader.ReadDouble();
        public static decimal ReadDecimal(this Reader reader) => reader.ReadDecimal();
        public static string ReadString(this Reader reader) => reader.ReadString();
        public static Vector2 ReadVector2(this Reader reader) => reader.ReadVector2();
        public static Vector3 ReadVector3(this Reader reader) => reader.ReadVector3();
        public static Vector4 ReadVector4(this Reader reader) => reader.ReadVector4();
        public static Vector2Int ReadVector2Int(this Reader reader, AutoPackType packType = AutoPackType.Packed) => reader.ReadVector2Int(packType);
        public static Vector3Int ReadVector3Int(this Reader reader, AutoPackType packType = AutoPackType.Packed) => reader.ReadVector3Int(packType);
        public static Color ReadColor(this Reader reader, AutoPackType packType = AutoPackType.Packed) => reader.ReadColor(packType);
        public static Color32 ReadColor32(this Reader reader) => reader.ReadColor32();
        public static Quaternion ReadQuaternion(this Reader reader, AutoPackType packType = AutoPackType.Packed) => reader.ReadQuaternion(packType);
        public static Rect ReadRect(this Reader reader) => reader.ReadRect();
        public static Plane ReadPlane(this Reader reader) => reader.ReadPlane();
        public static Ray ReadRay(this Reader reader) => reader.ReadRay();
        public static Ray2D ReadRay2D(this Reader reader) => reader.ReadRay2D();
        public static Matrix4x4 ReadMatrix4x4(this Reader reader) => reader.ReadMatrix4x4();
        [CodegenExclude]
        public static byte[] ReadBytesAllocated(this Reader reader, int count) => reader.ReadBytesAllocated(count);
        public static System.Guid ReadGuid(this Reader reader) => reader.ReadGuid();
        public static GameObject ReadGameObject(this Reader reader) => reader.ReadGameObject();
        public static Transform ReadTransform(this Reader reader) => reader.ReadTransform();
        public static NetworkObject ReadNetworkObject(this Reader reader) => reader.ReadNetworkObject();
        public static NetworkBehaviour ReadNetworkBehaviour(this Reader reader) => reader.ReadNetworkBehaviour();
        public static Channel ReadChannel(this Reader reader) => reader.ReadChannel();
        public static NetworkConnection ReadNetworkConnection(this Reader reader) => reader.ReadNetworkConnection();
        [CodegenExclude]
        public static T Read<T>(this Reader reader) => reader.Read<T>();
    }
}
