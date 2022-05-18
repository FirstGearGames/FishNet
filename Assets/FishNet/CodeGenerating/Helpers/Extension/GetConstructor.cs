using FishNet.CodeGenerating.Helping.Extension;
using MonoFN.Cecil;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{
    public static class Constructors
    {

        /// <summary>
        /// Gets the first constructor that optionally has, or doesn't have parameters.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static MethodDefinition GetFirstConstructor(this TypeReference typeRef, bool requireParameters)
        {
            return typeRef.CachedResolve().GetFirstConstructor(requireParameters);
        }
        /// <summary>
        /// Gets the first constructor that optionally has, or doesn't have parameters.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static MethodDefinition GetFirstConstructor(this TypeDefinition typeDef, bool requireParameters)
        {

            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                if (methodDef.IsConstructor && methodDef.IsPublic)
                {
                    if (requireParameters && methodDef.Parameters.Count > 0)
                        return methodDef;
                    else if (!requireParameters && methodDef.Parameters.Count == 0)
                        return methodDef;
                }

            }

            return null;
        }

        /// <summary>
        /// Gets the first public constructor with no parameters.
        /// </summary>
        /// <returns></returns>
        public static MethodDefinition GetConstructor(this TypeReference typeRef)
        {
            return typeRef.CachedResolve().GetConstructor();
        }
        /// <summary>
        /// Gets the first public constructor with no parameters.
        /// </summary>
        /// <returns></returns>
        public static MethodDefinition GetConstructor(this TypeDefinition typeDef)
        {
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                if (methodDef.IsConstructor && methodDef.IsPublic && methodDef.Parameters.Count == 0)
                    return methodDef;
            }

            return null;
        }

        /// <summary>
        /// Gets all constructors on typeDef.
        /// </summary>
        /// <returns></returns>
        public static List<MethodDefinition> GetConstructors(this TypeDefinition typeDef)
        {
            List<MethodDefinition> lst = new List<MethodDefinition>();
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                if (methodDef.IsConstructor)
                    lst.Add(methodDef);
            }

            return lst;
        }


        /// <summary>
        /// Gets constructor which has arguments.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static MethodDefinition GetConstructor(this TypeReference typeRef, Type[] arguments)
        {
            return typeRef.CachedResolve().GetConstructor(arguments);
        }

        /// <summary>
        /// Gets constructor which has arguments.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        public static MethodDefinition GetConstructor(this TypeDefinition typeDef, Type[] arguments)
        {
            Type[] argsCopy = (arguments == null) ? new Type[0] : arguments;
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                if (methodDef.IsConstructor && methodDef.IsPublic && methodDef.Parameters.Count == argsCopy.Length)
                {
                    bool match = true;
                    for (int i = 0; i < argsCopy.Length; i++)
                    {
                        if (methodDef.Parameters[0].ParameterType.FullName != argsCopy[i].FullName)
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return methodDef;
                }
            }
            return null;
        }


        /// <summary>
        /// Gets constructor which has arguments.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static MethodDefinition GetConstructor(this TypeReference typeRef, TypeReference[] arguments)
        {
            return typeRef.CachedResolve().GetConstructor(arguments);
        }

        /// <summary>
        /// Gets constructor which has arguments.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        public static MethodDefinition GetConstructor(this TypeDefinition typeDef, TypeReference[] arguments)
        {
            TypeReference[] argsCopy = (arguments == null) ? new TypeReference[0] : arguments;
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                if (methodDef.IsConstructor && methodDef.IsPublic && methodDef.Parameters.Count == argsCopy.Length)
                {
                    bool match = true;
                    for (int i = 0; i < argsCopy.Length; i++)
                    {
                        if (methodDef.Parameters[0].ParameterType.FullName != argsCopy[i].FullName)
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return methodDef;
                }
            }
            return null;
        }

        /// <summary>
        /// Resolves the constructor with parameterCount for typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        public static MethodDefinition GetConstructor(this TypeReference typeRef, int parameterCount)
        {
            return typeRef.CachedResolve().GetConstructor(parameterCount);
        }


        /// <summary>
        /// Resolves the constructor with parameterCount for typeRef.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        public static MethodDefinition GetConstructor(this TypeDefinition typeDef, int parameterCount)
        {
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                if (methodDef.IsConstructor && methodDef.IsPublic && methodDef.Parameters.Count == parameterCount)
                    return methodDef;
            }
            return null;
        }
    }


}