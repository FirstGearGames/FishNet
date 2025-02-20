using FishNet.Managing;
using FishNet.Serializing;
using System;
using UnityEngine;


/// <summary>
/// An unique id assigned to every prefab in a spawnable objects collection.
/// </summary>
public struct PrefabId : IEquatable<PrefabId>//, IComparable<PrefabId>
{
    // Hack for serializing the nullable fields
    [Serializable]
    public struct SerializedPrefabId : ISerializationCallbackReceiver
    {
        [NonSerialized]
        internal PrefabId PrefabId;

        [SerializeField] private int _idInt32;
        [SerializeField] private string _idGuid;
        [SerializeField] private ushort _idUshort;
        [SerializeField] private string _idString;

        public void OnAfterDeserialize()
        {
            // this class is here because this constructor is private
            if (_idInt32 == default && _idInt32 == default && _idUshort == default && _idString == default)
                PrefabId = Invalid;

            PrefabId = new PrefabId(_idInt32, Guid.Parse(_idGuid), _idUshort, _idString);
        }

        public void OnBeforeSerialize()
        {
            _idInt32 = PrefabId._idInt32 ?? Invalid._idInt32.Value;
            _idGuid = (PrefabId._idGuid ?? Invalid._idGuid.Value).ToString();
            _idUshort = PrefabId._idUshort ?? Invalid._idUshort.Value;
            _idString = PrefabId._idString ?? Invalid._idString;
        }
    }



    // Using 2 bits to represent 4 possible types:
    // 00 - Int32    (0)
    // 01 - Guid     (1)
    // 10 - UShort   (2)
    // 11 - String   (3)
    private int? _idInt32;
    private Guid? _idGuid;
    private ushort? _idUshort;
    private string _idString;

    private bool intValid => _idInt32.HasValue && _idInt32.Value != Invalid.AsInt32;
    private bool guidValid => _idGuid.HasValue && _idGuid.Value != Invalid.AsGuid;
    private bool ushortValid => _idUshort.HasValue && _idUshort.Value != Invalid.AsUshort;
    private bool stringValid => !string.IsNullOrEmpty(_idString) && _idString != Invalid.AsString;

    //private string __idString { get { return _idString ?? Invalid._idString; } set { _idString = value; } }

    // Calculate type flags based on which value is set
    private bool _typeFlag1 => intValid || guidValid;
    private bool _typeFlag2 => ushortValid || stringValid;

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

    public static PrefabId Invalid = new PrefabId(int.MaxValue, default(Guid), ushort.MaxValue, "");

    public bool IsNullOrInvalid()
    {
        if (this == Invalid)
        {
            return true;
        }
        if (!_idInt32.HasValue && !_idGuid.HasValue && !_idUshort.HasValue && _idString == null)
        {
            NetworkManagerExtensions.LogError($"A PrefabID is completely null, this should normally be impossible.");
            return true;
        }
        return false;

    }

    // Type checking using computed bit flags
    public bool IsInt32 => !_typeFlag1 && !_typeFlag2;
    public bool IsGuid => _typeFlag1 && !_typeFlag2;
    public bool IsUshort => !_typeFlag1 && _typeFlag2;
    public bool IsString => _typeFlag1 && _typeFlag2;

    public PrefabId(Reader reader)
    {
        bool typeFlag1 = reader.ReadBoolean();
        bool typeFlag2 = reader.ReadBoolean();
        _idInt32 = (!typeFlag1 && !typeFlag2) ? reader.ReadInt32() : null;
        _idGuid = (typeFlag1 && !typeFlag2) ? reader.ReadGuid() : null;
        _idUshort = (!typeFlag1 && typeFlag2) ? reader.ReadUInt16() : null;
        _idString = (typeFlag1 && typeFlag2) ? reader.ReadString() : null;
    }
    public void Write(Writer writer)
    {
        writer.WriteBoolean(_typeFlag1);
        writer.WriteBoolean(_typeFlag2);
        if (IsInt32) writer.WriteInt32(AsInt32);
        if (IsGuid) writer.WriteGuidAllocated(AsGuid);
        if (IsUshort) writer.WriteUInt16(AsUshort);
        if (IsString) writer.WriteString(AsString);
    }

    public Type GetIdType()
    {
        if (IsInt32) return typeof(int);
        if (IsGuid) return typeof(Guid);
        if (IsUshort) return typeof(ushort);
        if (IsString) return typeof(string);
        return default;
    }

    

    // Implicit conversions
    public static implicit operator PrefabId(int value) => new(value);
    public static implicit operator PrefabId(Guid value) => new(value);
    public static implicit operator PrefabId(ushort value) => new(value);
    public static implicit operator PrefabId(string value) => new(value);

    public static implicit operator int(PrefabId value) => value.AsInt32;
    public static implicit operator Guid(PrefabId value) => value.AsGuid;
    public static implicit operator ushort(PrefabId value) => value.AsUshort;

    public static implicit operator int?(PrefabId value) => value._idInt32;
    public static implicit operator Guid?(PrefabId value) => value._idGuid;
    public static implicit operator ushort?(PrefabId value) => value._idUshort;
    public static implicit operator string(PrefabId value) => value._idString;


    private int _int32OrInvalid => _idInt32 ?? Invalid._idInt32.Value;
    private Guid _guidOrInvalid => _idGuid ?? Invalid._idGuid.Value;
    private ushort _ushortOrInvalid => _idUshort ?? Invalid._idUshort.Value;
    private string _stringOrInvalid => _idString ?? Invalid._idString;

    // Value getters
    public int AsInt32 => _int32OrInvalid;
    public Guid AsGuid => _guidOrInvalid;
    public ushort AsUshort => _ushortOrInvalid;
    public string AsString => _stringOrInvalid;

    // ToString override
    public override string ToString()
    {
        if (IsInt32) return $"Int32:{_idInt32}";
        if (IsGuid) return $"Guid:{_idGuid}";
        if (IsUshort) return $"UShort:{_idUshort}";
        if (IsString) return $"String:{_idString}";
        return "Null";
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
   /* public int CompareTo(PrefabId other)
    {
        // First compare by type
        int typeComparison = GetTypeOrder().CompareTo(other.GetTypeOrder());
        if (typeComparison != 0) return typeComparison;

        // If same type, compare values
        if (IsInt32) return _idInt32.Value.CompareTo(other._idInt32 ?? Invalid._idInt32);
        if (IsGuid) return (_idGuid?.CompareTo(other._idGuid ?? Invalid._idGuid) ?? 0) ;
        if (IsUshort) return _idUshort.Value.CompareTo(other._idUshort ?? Invalid._idUshort);
        if (IsString) return string.Compare(_idString, other._idString, StringComparison.Ordinal);

        return 0;
    }*/

    // Equality operators
    public static bool operator ==(PrefabId left, PrefabId right) => left.Equals(right);
    public static bool operator !=(PrefabId left, PrefabId right) => !left.Equals(right);

   /* // Comparison operators
    public static bool operator <(PrefabId left, PrefabId right) => left.CompareTo(right) < 0;
    public static bool operator >(PrefabId left, PrefabId right) => left.CompareTo(right) > 0;
    public static bool operator <=(PrefabId left, PrefabId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PrefabId left, PrefabId right) => left.CompareTo(right) >= 0;*/
}