﻿using FishNet.CodeGenerating.Extension;
using MonoFN.Cecil;
using MonoFN.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping.Extension
{


    internal static class TypeDefinitionExtensionsOld
    {

        /// <summary>
        /// Creates a GenericInstanceType and adds parameters.
        /// </summary>
        internal static GenericInstanceType CreateGenericInstanceType(this TypeDefinition type, Collection<GenericParameter> parameters)
        {
            GenericInstanceType git = new GenericInstanceType(type);
            foreach (GenericParameter gp in parameters)
                git.GenericArguments.Add(gp);

            return git;
        }

        /// <summary>
        /// Finds public fields in type and base type
        /// </summary>
        /// <param name="variable"></param>
        /// <returns></returns>
        public static IEnumerable<FieldDefinition> FindAllPublicFields(this TypeDefinition typeDef, bool ignoreStatic, bool ignoreNonSerialized, System.Type[] excludedBaseTypes = null, string[] excludedAssemblyPrefixes = null)
        {
            while (typeDef != null)
            {
                if (IsExcluded(typeDef, excludedBaseTypes, excludedAssemblyPrefixes))
                    break;

                foreach (FieldDefinition fd in typeDef.Fields)
                {
                    if (ignoreStatic && fd.IsStatic)
                        continue;
                    if (fd.IsPrivate)
                        continue;
                    if (ignoreNonSerialized && fd.IsNotSerialized)
                        continue;
                    if (CodegenSession.GeneralHelper.CodegenExclude(fd))
                        continue;

                    yield return fd;
                }

                try { typeDef = typeDef.BaseType?.CachedResolve(); }
                catch { break; }
            }
        }

        /// <summary>
        /// Finds public properties on typeDef and all base types which have a public get/set accessor.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        public static IEnumerable<PropertyDefinition> FindAllPublicProperties(this TypeDefinition typeDef, bool excludeGenerics = true, System.Type[] excludedBaseTypes = null, string[] excludedAssemblyPrefixes = null)
        {
            while (typeDef != null)
            {
                if (IsExcluded(typeDef, excludedBaseTypes, excludedAssemblyPrefixes))
                    break;

                foreach (PropertyDefinition pd in typeDef.Properties)
                {
                    //Missing get or set method.
                    if (pd.GetMethod == null || pd.SetMethod == null)
                        continue;
                    //Get or set is private.
                    if (pd.GetMethod.IsPrivate || pd.SetMethod.IsPrivate)
                        continue;
                    if (excludeGenerics && pd.GetMethod.ReturnType.IsGenericParameter)
                        continue;
                    if (CodegenSession.GeneralHelper.CodegenExclude(pd))
                        continue;

                    yield return pd;
                }

                try { typeDef = typeDef.BaseType?.CachedResolve(); }
                catch { break; }
            }


        }

        /// <summary>
        /// Returns if typeDef is excluded.
        /// </summary>
        private static bool IsExcluded(TypeDefinition typeDef, System.Type[] excludedBaseTypes = null, string[] excludedAssemblyPrefixes = null)
        {
            if (excludedBaseTypes != null)
            {
                foreach (System.Type t in excludedBaseTypes)
                {
                    if (typeDef.FullName == t.FullName)
                        return true;
                }
            }
            if (excludedAssemblyPrefixes != null)
            {
                foreach (string s in excludedAssemblyPrefixes)
                {
                    int len = s.Length;
                    string tdAsmName = typeDef.Module.Assembly.FullName;
                    if (tdAsmName.Length >= len && tdAsmName.Substring(0, len).ToLower() == s.ToLower())
                        return true;
                }
            }

            //Fall through, not excluded.
            return false;
        }


        /// <summary>
        /// Returns if typeDef is excluded.
        /// </summary>
        public static bool IsExcluded(this TypeDefinition typeDef, string excludedAssemblyPrefix)
        {

            int len = excludedAssemblyPrefix.Length;
            string tdAsmName = typeDef.Module.Assembly.FullName;
            if (tdAsmName.Length >= len && tdAsmName.Substring(0, len).ToLower() == excludedAssemblyPrefix.ToLower())
                return true;

            //Fall through, not excluded.
            return false;
        }

        /// <summary>
        /// Returns if typeDef or any of it's parents inherit from NetworkBehaviour.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static bool InheritsNetworkBehaviour(this TypeDefinition typeDef)
        {
            string nbFullName = CodegenSession.NetworkBehaviourHelper.FullName;

            TypeDefinition copyTd = typeDef;
            while (copyTd != null)
            {
                if (copyTd.FullName == nbFullName)
                    return true;

                copyTd = copyTd.GetNextBaseTypeDefinition();
            }

            //Fall through, network behaviour not found.
            return false;
        }

        /// <summary>
        /// Returns a nested TypeDefinition of name.
        /// </summary>
        internal static TypeDefinition GetNestedType(this TypeDefinition typeDef, string name)
        {
            foreach (TypeDefinition nestedTd in typeDef.NestedTypes)
            {
                if (nestedTd.Name == name)
                    return nestedTd;
            }

            return null;
        }

        /// <summary>
        /// Returns if the BaseType for TypeDef exist and is not NetworkBehaviour,
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static bool CanProcessBaseType(this TypeDefinition typeDef)
        {
            return (typeDef != null && typeDef.BaseType != null && typeDef.BaseType.FullName != CodegenSession.NetworkBehaviourHelper.FullName);
        }
        /// <summary>
        /// Returns if the BaseType for TypeDef exist and is not NetworkBehaviour,
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static TypeDefinition GetNextBaseClassToProcess(this TypeDefinition typeDef)
        {
            if (typeDef.BaseType != null && typeDef.BaseType.FullName != CodegenSession.NetworkBehaviourHelper.FullName)
                return typeDef.BaseType.CachedResolve();
            else
                return null;
        }

        internal static TypeDefinition GetLastBaseClass(this TypeDefinition typeDef)
        {
            TypeDefinition copyTd = typeDef;
            while (copyTd.BaseType != null)
                copyTd = copyTd.BaseType.CachedResolve();

            return copyTd;
        }

        /// <summary>
        /// Searches for a type in current and inherited types.
        /// </summary>
        internal static TypeDefinition GetClassInInheritance(this TypeDefinition typeDef, string typeFullName)
        {
            TypeDefinition copyTd = typeDef;
            do
            {
                if (copyTd.FullName == typeFullName)
                    return copyTd;

                if (copyTd.BaseType != null)
                    copyTd = copyTd.BaseType.CachedResolve();
                else
                    copyTd = null;

            } while (copyTd != null);

            //Not found.
            return null;
        }

        /// <summary>
        /// Searches for a type in current and inherited types.
        /// </summary>
        internal static TypeDefinition GetClassInInheritance(this TypeDefinition typeDef, TypeDefinition targetTypeDef)
        {
            if (typeDef == null)
                return null;

            TypeDefinition copyTd = typeDef;
            do
            {
                if (copyTd == targetTypeDef)
                    return copyTd;

                if (copyTd.BaseType != null)
                    copyTd = copyTd.BaseType.CachedResolve();
                else
                    copyTd = null;

            } while (copyTd != null);

            //Not found.
            return null;
        }



        /// <summary>
        /// Returns if typeDef is static (abstract, sealed).
        /// </summary>
        internal static bool IsStatic(this TypeDefinition typeDef)
        {
            //Combining flags in a single check some reason doesn't work right with HasFlag.
            return (typeDef.Attributes.HasFlag(TypeAttributes.Abstract) && typeDef.Attributes.HasFlag(TypeAttributes.Sealed));
        }

        /// <summary>
        /// Gets an enum underlying type for typeDef.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static TypeReference GetEnumUnderlyingTypeReference(this TypeDefinition typeDef)
        {
            foreach (FieldDefinition field in typeDef.Fields)
            {
                if (!field.IsStatic)
                    return field.FieldType;
            }
            throw new ArgumentException($"Invalid enum {typeDef.FullName}");
        }

        /// <summary>
        /// Returns if typeDef is derived from type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static bool InheritsFrom<T>(this TypeDefinition typeDef)
        {
            return InheritsFrom(typeDef, typeof(T));
        }

        /// <summary>
        /// Returns if typeDef is derived from type.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static bool InheritsFrom(this TypeDefinition typeDef, Type type)
        {
            if (!typeDef.IsClass)
                return false;

            TypeDefinition copyTd = typeDef;
            while (copyTd.BaseType != null)
            {
                if (copyTd.BaseType.IsType(type))
                    return true;

                copyTd = copyTd.GetNextBaseTypeDefinition();
            }

            //Fall through.
            return false;
        }

        /// <summary>
        /// Adds a method to typeDef.
        /// </summary>
        /// <param name="typDef"></param>
        /// <param name="methodName"></param>
        /// <param name="attributes"></param>
        /// <returns></returns>
        internal static MethodDefinition AddMethod(this TypeDefinition typDef, string methodName, MethodAttributes attributes)
        {
            return AddMethod(typDef, methodName, attributes, typDef.Module.ImportReference(typeof(void)));
        }
        /// <summary>
        /// Adds a method to typeDef.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="methodName"></param>
        /// <param name="attributes"></param>
        /// <param name="typeReference"></param>
        /// <returns></returns>
        internal static MethodDefinition AddMethod(this TypeDefinition typeDef, string methodName, MethodAttributes attributes, TypeReference typeReference)
        {
            var method = new MethodDefinition(methodName, attributes, typeReference);
            typeDef.Methods.Add(method);
            return method;
        }


        /// <summary>
        /// Finds the first method by a given name.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        internal static MethodDefinition GetMethod(this TypeDefinition typeDef, string methodName)
        {
            return typeDef.Methods.FirstOrDefault(method => method.Name == methodName);
        }

        /// <summary>
        /// Finds the first method by a given name.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        internal static MethodDefinition GetMethod(this TypeDefinition typeDef, string methodName, Type[] types)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns if a type is a subclass of another.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="ClassTypeFullName"></param>
        /// <returns></returns>
        internal static bool IsSubclassOf(this TypeDefinition typeDef, string ClassTypeFullName)
        {
            if (!typeDef.IsClass) return false;

            TypeReference baseTypeRef = typeDef.BaseType;
            while (baseTypeRef != null)
            {
                if (baseTypeRef.FullName == ClassTypeFullName)
                {
                    return true;
                }

                try
                {
                    baseTypeRef = baseTypeRef.CachedResolve().BaseType;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a field reference by name.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static FieldReference GetField(this TypeDefinition typeDef, string fieldName)
        {
            if (typeDef.HasFields)
            {
                for (int i = 0; i < typeDef.Fields.Count; i++)
                {
                    if (typeDef.Fields[i].Name == fieldName)
                    {
                        return typeDef.Fields[i];
                    }
                }
            }

            return null;
        }


        /// <summary>
        /// Returns if the TypeDefinition implements TInterface.
        /// </summary>
        /// <typeparam name="TInterface"></typeparam>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        public static bool ImplementsInterface<TInterface>(this TypeDefinition typeDef)
        {
            for (int i = 0; i < typeDef.Interfaces.Count; i++)
            {
                if (typeDef.Interfaces[i].InterfaceType.Is<TInterface>())
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Returns if the TypeDefinition implements TInterface.
        /// </summary>
        /// <typeparam name="TInterface"></typeparam>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        public static bool ImplementsInterfaceRecursive<TInterface>(this TypeDefinition typeDef)
        {
            TypeDefinition climbTypeDef = typeDef;

            while (climbTypeDef != null)
            {
                if (climbTypeDef.Interfaces.Any(i => i.InterfaceType.Is<TInterface>()))
                    return true;

                try
                {
                    if (climbTypeDef.BaseType != null)
                        climbTypeDef = climbTypeDef.BaseType.CachedResolve();
                    else
                        climbTypeDef = null;
                }
                //Could not resolve assembly; can happen for assemblies being checked outside FishNet/csharp.
                catch (AssemblyResolutionException)
                {
                    break;
                }
            }

            return false;
        }
    }


}