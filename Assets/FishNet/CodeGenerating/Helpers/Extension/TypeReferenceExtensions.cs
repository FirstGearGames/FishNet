using MonoFN.Cecil;
using MonoFN.Cecil.Rocks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping.Extension
{

    internal static class TypeReferenceExtensions
    {

        /// <summary>
        /// Returns if typeRef is a class or struct.
        /// </summary>
        internal static bool IsClassOrStruct(this TypeReference typeRef)
        {
            TypeDefinition typeDef = typeRef.CachedResolve();
            return (!typeDef.IsPrimitive && (typeDef.IsClass || typeDef.IsValueType));
        }

        /// <summary>
        /// Gets all public fields in typeRef and base type.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static IEnumerable<FieldDefinition> FindAllPublicFields(this TypeReference typeRef)
        {
            return FindAllPublicFields(typeRef.CachedResolve());
        }

        /// <summary>
        /// Gets a Resolve favoring cached results first.
        /// </summary>
        internal static TypeDefinition CachedResolve(this TypeReference typeRef)
        {
            return CodegenSession.GeneralHelper.GetTypeReferenceResolve(typeRef);
        }

        /// <summary>
        /// Finds public fields in type and base type
        /// </summary>
        /// <param name="variable"></param>
        /// <returns></returns>
        public static IEnumerable<FieldDefinition> FindAllPublicFields(this TypeDefinition typeDefinition)
        {
            while (typeDefinition != null)
            {
                foreach (FieldDefinition field in typeDefinition.Fields)
                {
                    if (field.IsStatic || field.IsPrivate)
                        continue;

                    if (field.IsNotSerialized)
                        continue;

                    yield return field;
                }

                try
                {
                    typeDefinition = typeDefinition.BaseType?.CachedResolve();
                }
                catch
                {
                    break;
                }
            }
        }


        /// <summary>
        /// Returns a method within the base type of typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        public static MethodDefinition GetMethodInBase(this TypeReference typeRef, string methodName)
        {
            TypeDefinition td = typeRef.CachedResolve().GetNextBaseClass();
            while (td != null)
            {
                Debug.LogWarning(td.Name);
                foreach (MethodDefinition md in td.Methods)
                {
                    Debug.Log("X " + md.Name);
                    if (md.Name == methodName)
                    {
                        return md;

                        //MethodReference method = md;
                        //if (typeRefCopy.IsGenericInstance)
                        //{
                        //    var baseTypeInstance = (GenericInstanceType)typeRef;
                        //    method = method.MakeHostInstanceGeneric(baseTypeInstance);
                        //}

                        //return method;
                    }
                }

                try
                {
                    td = td.GetNextBaseClass();
                }
                /* This may occur when inheriting from a class
                 * in another assembly. */
                catch (AssemblyResolutionException)
                {
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns if a typeRef is type.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsType(this TypeReference typeRef, Type type)
        {
            if (type.IsGenericType)
                return typeRef.GetElementType().FullName == type.FullName;
            else
                return typeRef.FullName == type.FullName;
        }



        /// <summary>
        /// Returns if typeRef is a multidimensional array.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static bool IsMultidimensionalArray(this TypeReference typeRef)
        {
            return typeRef is ArrayType arrayType && arrayType.Rank > 1;
        }


        /// <summary>
        /// Returns if typeRef can be resolved.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static bool CanBeResolved(this TypeReference typeRef)
        {
            while (typeRef != null)
            {
                if (typeRef.Scope.Name == "Windows")
                {
                    return false;
                }

                if (typeRef.Scope.Name == "mscorlib")
                {
                    TypeDefinition resolved = typeRef.CachedResolve();
                    return resolved != null;
                }

                try
                {
                    typeRef = typeRef.CachedResolve().BaseType;
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Creates a generic type out of another type, if needed.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static TypeReference ConvertToGenericIfNeeded(this TypeDefinition type)
        {
            if (type.HasGenericParameters)
            {
                // get all the generic parameters and make a generic instance out of it
                var genericTypes = new TypeReference[type.GenericParameters.Count];
                for (int i = 0; i < type.GenericParameters.Count; i++)
                {
                    genericTypes[i] = type.GenericParameters[i].GetElementType();
                }

                return type.MakeGenericInstanceType(genericTypes);
            }
            else
            {
                return type;
            }
        }

    }

}