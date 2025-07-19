using System;
using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using FishNet.Serializing.Helping;
using FishNet.Utility.Performance;
using MonoFN.Cecil;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Helping
{
    /// <summary>
    /// Categories of what triggered automatic serializer generation.
    /// </summary>
    internal enum SerializationSource
    {
        /// <summary>
        /// Unknown or unspecified source.
        /// </summary>
        Unknown,
        /// <summary>
        /// Type was found as a parameter in an RPC method.
        /// </summary>
        RpcParameter,
        /// <summary>
        /// Type was found as the generic argument of a SyncVar.
        /// </summary>
        SyncVar,
        /// <summary>
        /// Type was found as a generic argument in a SyncList.
        /// </summary>
        SyncList,
        /// <summary>
        /// Type was found as a generic argument in a SyncDictionary.
        /// </summary>
        SyncDictionary,
        /// <summary>
        /// Type was found as a generic argument in a SyncHashSet.
        /// </summary>
        SyncHashSet,
        /// <summary>
        /// Type was found as a custom SyncType data type.
        /// </summary>
        CustomSyncType,
        /// <summary>
        /// Type was found as a broadcast type.
        /// </summary>
        Broadcast,
        /// <summary>
        /// Type was found as a prediction replicate data type.
        /// </summary>
        PredictionReplicate,
        /// <summary>
        /// Type was found as a prediction reconcile data type.
        /// </summary>
        PredictionReconcile,
        /// <summary>
        /// Type was found in a custom serializer method.
        /// </summary>
        CustomSerializer,
        /// <summary>
        /// Type was found as a field/property in a struct/class being serialized.
        /// </summary>
        NestedField,
        /// <summary>
        /// Type was found as a generic argument (e.g., List&lt;T&gt; where T is this type).
        /// </summary>
        GenericArgument,
        /// <summary>
        /// Type was found with IncludeSerialization attribute.
        /// </summary>
        IncludeSerialization
    }

    /// <summary>
    /// Enhanced context information about what triggered serializer generation for a type.
    /// </summary>
    internal class SerializationContext
    {
        /// <summary>
        /// What triggered the serialization attempt.
        /// </summary>
        public SerializationSource Source { get; set; }

        /// <summary>
        /// The type that triggered serialization (e.g., the NetworkBehaviour containing an RPC).
        /// </summary>
        public TypeDefinition DeclaringType { get; set; }

        /// <summary>
        /// The method that triggered serialization (e.g., an RPC method).
        /// </summary>
        public MethodDefinition DeclaringMethod { get; set; }

        /// <summary>
        /// The field that triggered serialization (e.g., a SyncVar field).
        /// </summary>
        public FieldDefinition DeclaringField { get; set; }

        /// <summary>
        /// The parameter that triggered serialization (e.g., an RPC parameter).
        /// </summary>
        public ParameterDefinition DeclaringParameter { get; set; }

        /// <summary>
        /// Additional context information.
        /// </summary>
        public string AdditionalInfo { get; set; }

        /// <summary>
        /// Chain of parent contexts (for nested types).
        /// </summary>
        public SerializationContext ParentContext { get; set; }

        public SerializationContext(SerializationSource source)
        {
            Source = source;
        }

        /// <summary>
        /// Creates a detailed trace string explaining what caused the serialization attempt.
        /// </summary>
        public string GetTraceString()
        {
            var parts = new System.Collections.Generic.List<string>();

            // Build the chain from root to current
            var contexts = new System.Collections.Generic.List<SerializationContext>();
            var current = this;
            while (current != null)
            {
                contexts.Insert(0, current);
                current = current.ParentContext;
            }

            foreach (var context in contexts)
            {
                string part = context.GetSingleTraceString();
                if (!string.IsNullOrEmpty(part))
                    parts.Add(part);
            }

            return string.Join(" -> ", parts);
        }

        private string GetSingleTraceString()
        {
            switch (Source)
            {
                case SerializationSource.RpcParameter:
                    if (DeclaringMethod != null && DeclaringParameter != null)
                        return $"RPC parameter '{DeclaringParameter.Name}' in method '{DeclaringMethod.DeclaringType.Name}.{DeclaringMethod.Name}'";
                    else if (DeclaringMethod != null)
                        return $"RPC method '{DeclaringMethod.DeclaringType.Name}.{DeclaringMethod.Name}'";
                    return "RPC parameter";

                case SerializationSource.SyncVar:
                    if (DeclaringField != null)
                        return $"SyncVar field '{DeclaringField.Name}' in type '{DeclaringField.DeclaringType.Name}'";
                    return "SyncVar field";

                case SerializationSource.SyncList:
                    if (DeclaringField != null)
                        return $"SyncList field '{DeclaringField.Name}' in type '{DeclaringField.DeclaringType.Name}'";
                    return "SyncList field";

                case SerializationSource.SyncDictionary:
                    if (DeclaringField != null)
                        return $"SyncDictionary field '{DeclaringField.Name}' in type '{DeclaringField.DeclaringType.Name}'";
                    return "SyncDictionary field";

                case SerializationSource.SyncHashSet:
                    if (DeclaringField != null)
                        return $"SyncHashSet field '{DeclaringField.Name}' in type '{DeclaringField.DeclaringType.Name}'";
                    return "SyncHashSet field";

                case SerializationSource.CustomSyncType:
                    if (DeclaringField != null)
                        return $"custom SyncType field '{DeclaringField.Name}' in type '{DeclaringField.DeclaringType.Name}'";
                    return "custom SyncType field";

                case SerializationSource.Broadcast:
                    if (DeclaringType != null)
                        return $"broadcast type '{DeclaringType.Name}'";
                    return "broadcast type";

                case SerializationSource.PredictionReplicate:
                    if (DeclaringMethod != null)
                        return $"prediction replicate data in method '{DeclaringMethod.DeclaringType.Name}.{DeclaringMethod.Name}'";
                    return "prediction replicate data";

                case SerializationSource.PredictionReconcile:
                    if (DeclaringMethod != null)
                        return $"prediction reconcile data in method '{DeclaringMethod.DeclaringType.Name}.{DeclaringMethod.Name}'";
                    return "prediction reconcile data";

                case SerializationSource.CustomSerializer:
                    if (DeclaringMethod != null)
                        return $"custom serializer method '{DeclaringMethod.DeclaringType.Name}.{DeclaringMethod.Name}'";
                    return "custom serializer method";

                case SerializationSource.NestedField:
                    if (!string.IsNullOrEmpty(AdditionalInfo))
                        return $"nested field '{AdditionalInfo}'";
                    return "nested field";

                case SerializationSource.GenericArgument:
                    if (!string.IsNullOrEmpty(AdditionalInfo))
                        return $"generic argument in {AdditionalInfo}";
                    return "generic argument";

                case SerializationSource.IncludeSerialization:
                    if (DeclaringType != null)
                        return $"type '{DeclaringType.Name}' with IncludeSerialization attribute";
                    return "type with IncludeSerialization attribute";

                default:
                    return !string.IsNullOrEmpty(AdditionalInfo) ? AdditionalInfo : "unknown source";
            }
        }
    }

    internal class GeneratorHelper : CodegenBase
    {
        /// <summary>
        /// Gets what objectTypeRef will be serialized as.
        /// </summary>
        /// <param name="typeTrace">A trace of what led up to the type needing to be serialized.</param>
        public SerializerType GetSerializerType(TypeReference objectTr, bool writer, out TypeDefinition objectTd, string typeTrace)
        {
            // Convert legacy string trace to SerializationContext for backward compatibility
            var context = new SerializationContext(SerializationSource.Unknown)
            {
                AdditionalInfo = typeTrace
            };
            return GetSerializerType(objectTr, writer, out objectTd, context);
        }

        /// <summary>
        /// Gets what objectTypeRef will be serialized as.
        /// </summary>
        /// <param name="context">Context information about what triggered the serialization.</param>
        public SerializerType GetSerializerType(TypeReference objectTr, bool writer, out TypeDefinition objectTd, SerializationContext context)
        {
            string errorPrefix = writer ? "CreateWrite: " : "CreateRead: ";
            objectTd = null;

            /* Check if already has a serializer. */
            if (writer)
            {
                if (GetClass<WriterProcessor>().GetWriteMethodReference(objectTr) != null)
                {
                    LogError(GetLogTextWithTrace($"Writer already exist for {objectTr.FullName}."));
                    return SerializerType.Invalid;
                }
            }
            else
            {
                if (GetClass<ReaderProcessor>().GetReadMethodReference(objectTr) != null)
                {
                    LogError(GetLogTextWithTrace($"Reader already exist for {objectTr.FullName}."));
                    return SerializerType.Invalid;
                }
            }

            objectTd = objectTr.CachedResolve(Session);
            // Invalid typeDef.
            if (objectTd == null)
            {
                LogError(GetLogTextWithTrace($"{errorPrefix}{objectTd.FullName} could not be resolved."));
                return SerializerType.Invalid;
            }
            // Intentionally excluded.
            if (objectTd.CustomAttributes.Count > 0)
            {
                foreach (CustomAttribute item in objectTd.CustomAttributes)
                {
                    if (item.AttributeType.Is(typeof(ExcludeSerializationAttribute)))
                        return SerializerType.Invalid;
                }
            }

            // By reference.            
            if (objectTr.IsByReference)
            {
                LogError(GetLogTextWithTrace($"{errorPrefix}Cannot pass {objectTr.Name} by reference."));
                return SerializerType.Invalid;
            }

            /* Arrays have to be processed first because it's possible for them to meet other conditions
             * below and be processed wrong. */
            if (objectTr.IsArray)
            {
                if (objectTr.IsMultidimensionalArray())
                {
                    LogError(GetLogTextWithTrace($"{errorPrefix}{objectTr.Name} is an unsupported type. Multidimensional arrays are not supported."));
                    return SerializerType.Invalid;
                }

                return SerializerType.Array;
            }

            // Enum.
            if (objectTd.IsEnum)
                return SerializerType.Enum;

            if (objectTd.Is(typeof(Dictionary<,>)))
                return SerializerType.Dictionary;

            if (objectTd.Is(typeof(List<>)))
                return SerializerType.List;

            if (objectTd.Is(typeof(HashSet<>)))
                return SerializerType.HashSet;

            if (objectTd.InheritsFrom<NetworkBehaviour>(Session))
                return SerializerType.NetworkBehaviour;

            if (objectTr.IsNullable(Session))
            {
                GenericInstanceType git = objectTr as GenericInstanceType;
                if (git == null || git.GenericArguments.Count != 1)
                    return SerializerType.Invalid;

                return SerializerType.Nullable;
            }

            // Invalid type. This must be called after trying to generate everything but class.
            if (!CanGenerateSerializer(objectTd, context))
                return SerializerType.Invalid;

            // If here then the only type left is struct or class.
            if (objectTr.IsClassOrStruct(Session))
                return SerializerType.ClassOrStruct;

            if (objectTr.FullName == typeof(System.IntPtr).FullName)
            {
                LogError(GetLogTextWithTrace($"{errorPrefix}{objectTr.FullName} is an unsupported type."));
                return SerializerType.Invalid;
            }

            // Unknown type.
            LogError(GetLogTextWithTrace($"{errorPrefix}{objectTr.FullName} is an unsupported type. Mostly because we don't know what the heck it is. Please let us know so we can fix this."));

            return SerializerType.Invalid;

            string GetLogTextWithTrace(string txt) => $"{txt} Trace: {context.GetTraceString()}.";
        }

        /// <summary>
        /// Returns if objectTd can have a serializer generated for it.
        /// </summary>
        private bool CanGenerateSerializer(TypeDefinition objectTd)
        {
            // Convert to enhanced context for backward compatibility
            var context = new SerializationContext(SerializationSource.Unknown)
            {
                AdditionalInfo = "legacy call without context"
            };
            return CanGenerateSerializer(objectTd, context);
        }

        /// <summary>
        /// Returns if objectTd can have a serializer generated for it with enhanced context information.
        /// </summary>
        private bool CanGenerateSerializer(TypeDefinition objectTd, SerializationContext context)
        {
            System.Type unityObjectType = typeof(UnityEngine.Object);
            // Unable to determine type, cannot generate for.
            if (objectTd == null)
            {
                LogError($"{objectTd?.Name ?? "null"} is not a supported type. Use a supported type or provide a custom serializer. Trace: {context.GetTraceString()}");
                return false;
            }
            // Component.
            if (objectTd.InheritsFrom<UnityEngine.Component>(Session))
            {
                LogError($"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer. Trace: {context.GetTraceString()}");
                return false;
            }
            // Unity Object.
            if (objectTd.Is(unityObjectType))
            {
                LogError($"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer. Trace: {context.GetTraceString()}");
                return false;
            }
            // ScriptableObject.
            if (objectTd.Is(typeof(UnityEngine.ScriptableObject)))
            {
                LogError($"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer. Trace: {context.GetTraceString()}");
                return false;
            }
            // Has generic parameters.
            if (objectTd.HasGenericParameters)
            {
                LogError($"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer. Trace: {context.GetTraceString()}");
                return false;
            }
            // Is an interface.
            if (objectTd.IsInterface)
            {
                LogError($"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer. Trace: {context.GetTraceString()}");
                return false;
            }
            // Is abstract.
            if (objectTd.IsAbstract)
            {
                LogError($"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer. Trace: {context.GetTraceString()}");
                return false;
            }
            if (objectTd.InheritsFrom(Session, unityObjectType) && objectTd.IsExcluded(GeneralHelper.UNITYENGINE_ASSEMBLY_PREFIX))
            {
                LogError($"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer. Trace: {context.GetTraceString()}");
                return false;
            }

            // If here type is valid.
            return true;
        }
    }
}