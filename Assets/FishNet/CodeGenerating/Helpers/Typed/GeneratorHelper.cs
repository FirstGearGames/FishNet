using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using Mono.Cecil;
using System.Collections.Generic;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{


    internal static class GeneratorHelper
    {
        /// <summary>
        /// Gets what objectTypeRef will be serialized as.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <param name="writer"></param>
        /// <param name="objectTypeDef"></param>
        /// <param name="diagnostics"></param>
        /// <returns></returns>
        internal static SerializerType GetSerializerType(TypeReference objectTypeRef, bool writer, out TypeDefinition objectTypeDef)
        {
            string errorPrefix = (writer) ? "CreateWrite: " : "CreateRead: ";
            objectTypeDef = null;

            /* Check if already has a serializer. */
            if (writer)
            {
                if (CodegenSession.WriterHelper.GetFavoredWriteMethodReference(objectTypeRef, true) != null)
                {
                    CodegenSession.LogError($"Writer already exist for {objectTypeRef.FullName}.");
                    return SerializerType.Invalid;
                }
            }
            else
            {
                if (CodegenSession.ReaderHelper.GetFavoredReadMethodReference(objectTypeRef, true) != null)
                {
                    CodegenSession.LogError($"Reader already exist for {objectTypeRef.FullName}.");
                    return SerializerType.Invalid;
                }
            }
            
            objectTypeDef = objectTypeRef.Resolve();
            //Invalid typeDef.
            if (objectTypeDef == null)
            {
                CodegenSession.LogError($"{errorPrefix}{objectTypeDef.FullName} could not be resolved.");
                return SerializerType.Invalid;
            }
            //By reference.            
            if (objectTypeRef.IsByReference)
            {
                CodegenSession.LogError($"{errorPrefix}Cannot pass {objectTypeRef.Name} by reference");
                return SerializerType.Invalid;
            }
            /* Arrays have to be processed first because it's possible for them to meet other conditions
             * below and be processed wrong. */
            else if (objectTypeRef.IsArray)
            {
                if (objectTypeRef.IsMultidimensionalArray())
                {
                    CodegenSession.LogError($"{errorPrefix}{objectTypeRef.Name} is an unsupported type. Multidimensional arrays are not supported");
                    return SerializerType.Invalid;
                }
                else
                {
                    return SerializerType.Array;
                }
            }
            //Enum.
            else if (objectTypeDef.IsEnum)
            {
                return SerializerType.Enum;
            }
            //if (variableDefinition.Is(typeof(ArraySegment<>)))
            //{
            //    return GenerateArraySegmentReadFunc(objectTypeRef);
            //}
            else if (objectTypeDef.Is(typeof(List<>)))
            {
                return SerializerType.List;
            }
            else if (objectTypeDef.InheritsFrom<NetworkBehaviour>())
            {
                return SerializerType.NetworkBehaviour;
            }
            //Invalid type. This must be called after trying to generate everything but class.
            else if (!GeneratorHelper.IsValidSerializeType(objectTypeDef))
            {
                return SerializerType.Invalid;
            }
            //If here then the only type left is struct or class.
            else if ((!objectTypeDef.IsPrimitive && (objectTypeDef.IsClass || objectTypeDef.IsValueType)))
            {
                return SerializerType.ClassOrStruct;
            }
            //Unknown type.
            else
            {
                CodegenSession.LogError($"{errorPrefix}{objectTypeRef.Name} is an unsupported type. Mostly because we don't know what the heck it is. Please let us know so we can fix this.");
                return SerializerType.Invalid;
            }
        }


        /// <summary>
        /// Returns if objectTypeRef is an invalid type, which cannot be serialized.
        /// </summary>
        /// <param name="objectTypeDef"></param>
        /// <returns></returns> 
        private static bool IsValidSerializeType(TypeDefinition objectTypeDef)
        {
            string errorText = $"{objectTypeDef.Name} is not a supported type. Use a supported type or provide a custom serializer";
            //Unable to determine type, cannot generate for.
            if (objectTypeDef == null)
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Component.
            if (objectTypeDef.InheritsFrom<UnityEngine.Component>())
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Unity Object.
            if (objectTypeDef.Is(typeof(UnityEngine.Object)))
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //ScriptableObject.
            if (objectTypeDef.Is(typeof(UnityEngine.ScriptableObject)))
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Has generic parameters.
            if (objectTypeDef.HasGenericParameters)
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Is an interface.
            if (objectTypeDef.IsInterface)
            {
                CodegenSession.LogError(errorText);
                return false;
            }
            //Is abstract.
            if (objectTypeDef.IsAbstract)
            {
                CodegenSession.LogError(errorText);
                return false;
            }

            //If here type is valid.
            return true;
        }


    }


}