using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using FishNet.Serializing.Helping;
using FishNet.Utility.Performance;
using MonoFN.Cecil;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Helping
{


    internal class GeneratorHelper : CodegenBase
    {
        /// <summary>
        /// Gets what objectTypeRef will be serialized as.
        /// </summary>
        public SerializerType GetSerializerType(TypeReference objectTr, bool writer, out TypeDefinition objectTd)
        {
            string errorPrefix = (writer) ? "CreateWrite: " : "CreateRead: ";
            objectTd = null;

            /* Check if already has a serializer. */
            if (writer)
            {
                if (base.GetClass<WriterProcessor>().GetWriteMethodReference(objectTr) != null)
                {
                    base.LogError($"Writer already exist for {objectTr.FullName}.");
                    return SerializerType.Invalid;
                }
            }
            else
            {
                if (base.GetClass<ReaderProcessor>().GetReadMethodReference(objectTr) != null)
                {
                    base.LogError($"Reader already exist for {objectTr.FullName}.");
                    return SerializerType.Invalid;
                }
            }

            objectTd = objectTr.CachedResolve(base.Session);
            //Invalid typeDef.
            if (objectTd == null)
            {
                base.LogError($"{errorPrefix}{objectTd.FullName} could not be resolved.");
                return SerializerType.Invalid;
            }
            //Intentionally excluded.
            if (objectTd.CustomAttributes.Count > 0)
            {
                foreach (CustomAttribute item in objectTd.CustomAttributes)
                {
                    if (item.AttributeType.Is(typeof(CodegenExcludeAttribute)))
                        return SerializerType.Invalid;
                }
            }

            //By reference.            
            if (objectTr.IsByReference)
            {
                base.LogError($"{errorPrefix}Cannot pass {objectTr.Name} by reference");
                return SerializerType.Invalid;
            }
            /* Arrays have to be processed first because it's possible for them to meet other conditions
             * below and be processed wrong. */
            else if (objectTr.IsArray)
            {
                if (objectTr.IsMultidimensionalArray())
                {
                    base.LogError($"{errorPrefix}{objectTr.Name} is an unsupported type. Multidimensional arrays are not supported");
                    return SerializerType.Invalid;
                }
                else
                {
                    return SerializerType.Array;
                }
            }
            //Enum.
            else if (objectTd.IsEnum)
            {
                return SerializerType.Enum;
            }
            else if (objectTd.Is(typeof(Dictionary<,>)))
            {
                return SerializerType.Dictionary;
            }
            else if (objectTd.Is(typeof(List<>)))
            {
                return SerializerType.List;
            }
#pragma warning disable CS0618 // Type or member is obsolete
            else if (objectTd.Is(typeof(ListCache<>))) //Remove on 2024/01/01
            {
                return SerializerType.ListCache;
            }
#pragma warning restore CS0618 // Type or member is obsolete
            else if (objectTd.InheritsFrom<NetworkBehaviour>(base.Session))
            {
                return SerializerType.NetworkBehaviour;
            }
            else if (objectTr.IsNullable(base.Session))
            {
                GenericInstanceType git = objectTr as GenericInstanceType;
                if (git == null || git.GenericArguments.Count != 1)
                    return SerializerType.Invalid;
                else
                    return SerializerType.Nullable;
            }
            //Invalid type. This must be called after trying to generate everything but class.
            else if (!CanGenerateSerializer(objectTd))
            {
                return SerializerType.Invalid;
            }
            //If here then the only type left is struct or class.
            else if (objectTr.IsClassOrStruct(base.Session))
            {
                return SerializerType.ClassOrStruct;
            }
            //Unknown type.
            else
            {
                base.LogError($"{errorPrefix}{objectTr.Name} is an unsupported type. Mostly because we don't know what the heck it is. Please let us know so we can fix this.");
                return SerializerType.Invalid;
            }
        }


        /// <summary>
        /// Returns if objectTd can have a serializer generated for it.
        /// </summary>
        private bool CanGenerateSerializer(TypeDefinition objectTd)
        {
            string errorText = $"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer";

            System.Type unityObjectType = typeof(UnityEngine.Object);
            //Unable to determine type, cannot generate for.
            if (objectTd == null)
            {
                base.LogError(errorText);
                return false;
            }
            //Component.
            if (objectTd.InheritsFrom<UnityEngine.Component>(base.Session))
            {
                base.LogError(errorText);
                return false;
            }
            //Unity Object.
            if (objectTd.Is(unityObjectType))
            {
                base.LogError(errorText);
                return false;
            }
            //ScriptableObject.
            if (objectTd.Is(typeof(UnityEngine.ScriptableObject)))
            {
                base.LogError(errorText);
                return false;
            }
            //Has generic parameters.
            if (objectTd.HasGenericParameters)
            {
                base.LogError(errorText);
                return false;
            }
            //Is an interface.
            if (objectTd.IsInterface)
            {
                base.LogError(errorText);
                return false;
            }
            //Is abstract.
            if (objectTd.IsAbstract)
            {
                base.LogError(errorText);
                return false;
            }
            if (objectTd.InheritsFrom(base.Session, unityObjectType) && objectTd.IsExcluded(GeneralHelper.UNITYENGINE_ASSEMBLY_PREFIX))
            {
                base.LogError(errorText);
                return false;
            }

            //If here type is valid.
            return true;
        }


    }


}