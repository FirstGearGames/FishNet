﻿using FishNet.Utility;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.CodeGenerating
{
    /// <summary>
    /// Allows a SyncType to be mutable.
    /// </summary>
    public class AllowMutableSyncTypeAttribute : Attribute { }
    /// <summary>
    /// Type will be included in auto serializer creation.
    /// </summary>
    [AttributeUsage((AttributeTargets.Class | AttributeTargets.Struct), Inherited = true, AllowMultiple = false)]
    public class IncludeSerializationAttribute : Attribute { }
    /// <summary>
    /// Type will be excluded from auto serializer creation.
    /// </summary>
    public class ExcludeSerializationAttribute : Attribute { }
    /// <summary>
    /// Method will not be considered a writer or reader.
    /// </summary>
    public class NotSerializerAttribute : Attribute { }
    /// <summary>
    /// Method or type will be made public by codegen.
    /// </summary>
    internal class MakePublicAttribute : Attribute { }
    /// <summary>
    /// Method is a comparer for a value type.
    /// </summary>
    public class CustomComparerAttribute : Attribute { }
    /// <summary>
    /// Used on a type when you want a custom serializer to be global across all assemblies.
    /// </summary>
    [AttributeUsage((AttributeTargets.Class | AttributeTargets.Struct), Inherited = true, AllowMultiple = false)]
    public class UseGlobalCustomSerializerAttribute : Attribute { }
    /// <summary>
    /// Uses built-in caches to retrieve read classes rather than initializing a new instance.
    /// This attribute is primarily for internal use and may change at anytime without notice.
    /// </summary>
    [AttributeUsage((AttributeTargets.Class), Inherited = true, AllowMultiple = false)]
    public class ReadUnallocated : Attribute { }
}