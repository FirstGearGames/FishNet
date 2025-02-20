using FishNet.Serializing;
using System;
using UnityEngine;

[Serializable]
public struct PrefabId : IEquatable<PrefabId>, IComparable<PrefabId>
{
    // Using 2 bits to represent 4 possible types:
    // 00 - Int32    (0)
    // 01 - Guid     (1)
    // 10 - UShort   (2)
    // 11 - String   (3)
    [SerializeField] private readonly int? _idInt32;
    [SerializeField] private readonly Guid? _idGuid;
    [SerializeField] private readonly ushort? _idUshort;
    [SerializeField] private readonly string _idString;

    // Calculate type flags based on which value is set
    private bool _typeFlag1 => _idGuid.HasValue || (_idString != null);
    private bool _typeFlag2 => _idUshort.HasValue || (_idString != null);

    // Constructors
    public PrefabId(int id)
    {
        _idInt32 = id;
        _idGuid = null;
        _idUshort = null;
        _idString = null;
    }

    public PrefabId(Guid id)
    {
        _idInt32 = null;
        _idGuid = id;
        _idUshort = null;
        _idString = null;
    }

    public PrefabId(ushort id)
    {
        _idInt32 = null;
        _idGuid = null;
        _idUshort = id;
        _idString = null;
    }

    public PrefabId(string id)
    {
        _idInt32 = null;
        _idGuid = null;
        _idUshort = null;
        _idString = id;
    }

    private PrefabId(int intId, Guid guid, ushort ush, string str)
    {
        _idInt32 = intId;
        _idGuid = guid;
        _idUshort = ush;
        _idString = str;
    }

    public static PrefabId InvalidId = new PrefabId(int.MaxValue, default(Guid), ushort.MaxValue, null);

    public bool IsInvalid()
    {
        return (this == InvalidId);
    }

    // Type checking using computed bit flags
    public bool IsInt32 => !_typeFlag1 && !_typeFlag2;
    public bool IsGuid => _typeFlag1 && !_typeFlag2;
    public bool IsUshort => !_typeFlag1 && _typeFlag2;
    public bool IsString => _typeFlag1 && _typeFlag2;

    public PrefabId(PooledReader reader)
    {
        bool typeFlag1 = reader.ReadBoolean();
        bool typeFlag2 = reader.ReadBoolean();
        _idInt32 = (!typeFlag1 && !typeFlag2) ? reader.ReadInt32() : null;
        _idGuid = (typeFlag1 && !typeFlag2) ? reader.ReadGuid() : null;
        _idUshort = (!typeFlag1 && typeFlag2) ? reader.ReadUInt16() : null;
        _idString = (typeFlag1 && typeFlag2) ? reader.ReadString() : null;
    }
    public void Write(PooledWriter writer)
    {
        writer.WriteBoolean(_typeFlag1);
        writer.WriteBoolean(_typeFlag2);
        if (IsInt32) writer.WriteInt32(AsInt32);
        if (IsGuid) writer.WriteGuidAllocated(AsGuid);
        if (IsUshort) writer.WriteUInt16(AsUshort);
        if (IsString) writer.WriteString(AsString);
    }

    

    // Implicit conversions
    public static implicit operator PrefabId(int value) => new(value);
    public static implicit operator PrefabId(Guid value) => new(value);
    public static implicit operator PrefabId(ushort value) => new(value);
    public static implicit operator PrefabId(string value) => new(value);

    public static implicit operator int?(PrefabId value) => value._idInt32;
    public static implicit operator Guid?(PrefabId value) => value._idGuid;
    public static implicit operator ushort?(PrefabId value) => value._idUshort;
    public static implicit operator string(PrefabId value) => value._idString;

    // Value getters
    public int AsInt32 => _idInt32 ?? throw new InvalidOperationException("PrefabId does not contain an Int32 value");
    public Guid AsGuid => _idGuid ?? throw new InvalidOperationException("PrefabId does not contain a Guid value");
    public ushort AsUshort => _idUshort ?? throw new InvalidOperationException("PrefabId does not contain a UShort value");
    public string AsString => _idString ?? throw new InvalidOperationException("PrefabId does not contain a String value");

    // ToString override
    public override string ToString()
    {
        if (IsInt32) return $"Int32:{_idInt32}";
        if (IsGuid) return $"Guid:{_idGuid}";
        if (IsUshort) return $"UShort:{_idUshort}";
        if (IsString) return $"String:{_idString}";
        return "Invalid";
    }

    // Helper method to get type as number (for comparison)
    private int GetTypeOrder()
    {
        if (IsString) return 3;
        if (IsGuid) return 2;
        if (IsInt32) return 1;
        if (IsUshort) return 0;
        return -1;
    }

    // Equality
    public bool Equals(PrefabId other)
    {
        if (IsInt32 && other.IsInt32) return _idInt32 == other._idInt32;
        if (IsGuid && other.IsGuid) return _idGuid == other._idGuid;
        if (IsUshort && other.IsUshort) return _idUshort == other._idUshort;
        if (IsString && other.IsString) return string.Equals(_idString, other._idString);
        return false;
    }

    public override bool Equals(object obj)
    {
        return obj is PrefabId other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (IsInt32) return _idInt32.GetHashCode();
        if (IsGuid) return _idGuid.GetHashCode();
        if (IsUshort) return _idUshort.GetHashCode();
        if (IsString) return _idString?.GetHashCode() ?? 0;
        return 0;
    }

    // Comparison
    public int CompareTo(PrefabId other)
    {
        // First compare by type
        int typeComparison = GetTypeOrder().CompareTo(other.GetTypeOrder());
        if (typeComparison != 0) return typeComparison;

        // If same type, compare values
        if (IsInt32) return _idInt32.Value.CompareTo(other._idInt32.Value);
        if (IsGuid) return _idGuid.Value.CompareTo(other._idGuid.Value);
        if (IsUshort) return _idUshort.Value.CompareTo(other._idUshort.Value);
        if (IsString) return string.Compare(_idString, other._idString, StringComparison.Ordinal);

        return 0;
    }

    // Equality operators
    public static bool operator ==(PrefabId left, PrefabId right) => left.Equals(right);
    public static bool operator !=(PrefabId left, PrefabId right) => !left.Equals(right);

    // Comparison operators
    public static bool operator <(PrefabId left, PrefabId right) => left.CompareTo(right) < 0;
    public static bool operator >(PrefabId left, PrefabId right) => left.CompareTo(right) > 0;
    public static bool operator <=(PrefabId left, PrefabId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PrefabId left, PrefabId right) => left.CompareTo(right) >= 0;
}