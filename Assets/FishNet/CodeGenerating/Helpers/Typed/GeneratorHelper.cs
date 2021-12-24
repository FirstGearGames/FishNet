using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using MonoFN.Cecil;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{


    internal static class GeneratorHelper
    {
        /// <summary>
        /// Gets what objectTypeRef will be serialized as.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <param name="writer"></param>
        /// <param name="objectTd"></param>
        /// <param name="diagnostics"></param>
        /// <returns></returns>
        internal static SerializerType GetSerializerType(TypeReference objectTr, bool writer, out TypeDefinition objectTd)
        {
            string errorPrefix = (writer) ? "CreateWrite: " : "CreateRead: ";
            objectTd = null;

            /* Check if already has a serializer. */
            if (writer)
            {
                if (CodegenSession.WriterHelper.GetFavoredWriteMethodReference(objectTr, true) != null)
                {
                    CodegenSession.LogError($"Writer already exist for {objectTr.FullName}.");
                    return SerializerType.Invalid;
                }
            }
            else
            {
                if (CodegenSession.ReaderHelper.GetFavoredReadMethodReference(objectTr, true) != null)
                {
                    CodegenSession.LogError($"Reader already exist for {objectTr.FullName}.");
                    return SerializerType.Invalid;
                }
            }

            objectTd = objectTr.CachedResolve();
            //Invalid typeDef.
            if (objectTd == null)
            {
                CodegenSession.LogError($"{errorPrefix}{objectTd.FullName} could not be resolved.");
                return SerializerType.Invalid;
            }
            //By reference.            
            if (objectTr.IsByReference)
            {
                CodegenSession.LogError($"{errorPrefix}Cannot pass {objectTr.Name} by reference");
                return SerializerType.Invalid;
            }
            /* Arrays have to be processed first because it's possible for them to meet other conditions
             * below and be processed wrong. */
            else if (objectTr.IsArray)
            {
                if (objectTr.IsMultidimensionalArray())
                {
                    CodegenSession.LogError($"{errorPrefix}{objectTr.Name} is an unsupported type. Multidimensional arrays are not supported");
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
            else if (objectTd.InheritsFrom<NetworkBehaviour>())
            {
                return SerializerType.NetworkBehaviour;
            }
            else if (objectTr.Name == typeof(System.Nullable<>).Name)
            {
                GenericInstanceType git = objectTr as GenericInstanceType;
                if (git.GenericArguments.Count != 1)
                    return SerializerType.Invalid;
                else
                    return SerializerType.Nullable;
            }
            //Invalid type. This must be called after trying to generate everything but class.
            else if (!GeneratorHelper.IsValidSerializeType(objectTd))
            {
                return SerializerType.Invalid;
            }
            //If here then the only type left is struct or class.
            else if (objectTr.IsClassOrStruct())
            {
                return SerializerType.ClassOrStruct;
            }
            //Unknown type.
            else
            {
                CodegenSession.LogError($"{errorPrefix}{objectTr.Name} is an unsupported type. Mostly because we don't know what the heck it is. Please let us know so we can fix this.");
                return SerializerType.Invalid;
            }
        }


        /// <summary>
        /// Returns if objectTypeRef is an invalid type, which cannot be serialized.
        /// </summary>
        /// <param name="objectTd"></param>
        /// <returns></returns> 
        private static bool IsValidSerializeType(TypeDefinition objectTd)
        {
            string errorText = $"{objectTd.Name} is not a supported type. Use a supported type or provide a custom serializer";
            //Unable to determine type, cannot generate for.
            if (objectTd == null)
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Component.
            if (objectTd.InheritsFrom<UnityEngine.Component>())
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Unity Object.
            if (objectTd.Is(typeof(UnityEngine.Object)))
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //ScriptableObject.
            if (objectTd.Is(typeof(UnityEngine.ScriptableObject)))
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Has generic parameters.
            if (objectTd.HasGenericParameters)
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Is an interface.
            if (objectTd.IsInterface)
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Is abstract.
            if (objectTd.IsAbstract)
            {
                CodegenSession.LogError(errorText);
                return false;
            }

            //If here type is valid.
            return true;
        }


    }


}