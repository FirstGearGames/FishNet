using MonoFN.Cecil;
using MonoFN.Cecil.Rocks;
using System;
using System.Collections.Generic;


namespace FishNet.CodeGenerating.Helping.Extension
{

    internal static class TypeReferenceExtensions
    {

        /// <summary>
        /// Gets all public fields in typeRef and base type.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static IEnumerable<FieldDefinition> FindAllPublicFields(this TypeReference typeRef)
        {
            return FindAllPublicFields(typeRef.Resolve());
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
                    typeDefinition = typeDefinition.BaseType?.Resolve();
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
        public static MethodReference GetMethodInBaseType(this TypeReference typeRef, string methodName)
        {
            TypeDefinition typedef = typeRef.Resolve();
            TypeReference typeRefCopy = typeRef;
            while (typedef != null)
            {
                foreach (MethodDefinition md in typedef.Methods)
                {
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
                    TypeReference parent = typedef.BaseType;
                    typeRefCopy = parent;
                    typedef = parent?.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
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
                    TypeDefinition resolved = typeRef.Resolve();
                    return resolved != null;
                }

                try
                {
                    typeRef = typeRef.Resolve().BaseType;
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