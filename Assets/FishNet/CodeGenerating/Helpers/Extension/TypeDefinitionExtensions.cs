using MonoFN.Cecil;
using System;
using System.Linq;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping.Extension
{


    internal static class TypeDefinitionExtensions
    {
        /// <summary>
        /// Returns if typeDef or any of it's parents inherit from NetworkBehaviour.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static bool InheritsNetworkBehaviour(this TypeDefinition typeDef)
        {
            TypeDefinition copyTypeDef = typeDef;
            while (copyTypeDef != null)
            {
                //Base is a network behaviour.
                if (copyTypeDef.BaseType != null && copyTypeDef.BaseType.FullName == CodegenSession.ObjectHelper.NetworkBehaviour_FullName)
                    return true;

                copyTypeDef = GetNextBaseClass(copyTypeDef);
            }

            //Fall through, network behaviour not found.
            return false;
        }

        /// <summary>
        /// Returns if the BaseType for TypeDef exist and is not NetworkBehaviour,
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static bool CanProcessBaseType(this TypeDefinition typeDef)
        {
            return (typeDef.BaseType != null && typeDef.BaseType.FullName != CodegenSession.ObjectHelper.NetworkBehaviour_FullName);
        }
        /// <summary>
        /// Returns if the BaseType for TypeDef exist and is not NetworkBehaviour,
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static TypeDefinition GetNextBaseClassToProcess(TypeDefinition typeDef)
        {
            if (typeDef.BaseType != null && typeDef.BaseType.FullName != CodegenSession.ObjectHelper.NetworkBehaviour_FullName)
                return typeDef.BaseType.Resolve();
            else
                return null;
        }

        /// <summary>
        /// Gets the next base type for typeDef.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static TypeDefinition GetNextBaseClass(TypeDefinition typeDef)
        {
            return (typeDef.BaseType == null) ? null : typeDef.BaseType.Resolve();
        }
        /// <summary>
        /// Returns if typeDef is static (abstract, sealed).
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal static bool IsStatic(this TypeDefinition typeDef)
        {
            //Combing flags in a single check some reason doesn't work right with HasFlag.
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

                copyTd = GetNextBaseClass(copyTd);
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
                    baseTypeRef = baseTypeRef.Resolve().BaseType;
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
            TypeDefinition climbTypeDef = typeDef;

            while (climbTypeDef != null)
            {
                if (climbTypeDef.Interfaces.Any(i => i.InterfaceType.Is<TInterface>()))
                    return true;

                try
                {
                    if (climbTypeDef.BaseType != null)
                        climbTypeDef = climbTypeDef.BaseType.Resolve();
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