
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using MonoFN.Cecil;
using UnityEngine;

namespace FishNet.CodeGenerating.Extension
{


    internal static class TypeDefinitionExtensions
    {
        /// <summary>
        /// Returns a method in the next base class.
        /// </summary>
        public static MethodReference GetMethodReferenceInBase(this TypeDefinition td, string methodName)
        {
            if (td == null)
            {
                CodegenSession.LogError($"TypeDefinition is null.");
                return null;
            }
            if (td.BaseType == null)
            {
                CodegenSession.LogError($"BaseType for {td.FullName} is null.");
                return null;
            }

            TypeDefinition baseTd = td.BaseType.CachedResolve();
            MethodDefinition baseMd = baseTd.GetMethod(methodName);
            //Not found.
            if (baseMd == null)
                return null;

            //Is generic.
            if (baseTd.HasGenericParameters)
            {
                TypeReference baseTr = td.BaseType;
                GenericInstanceType baseGit = (GenericInstanceType)baseTr;

                CodegenSession.ImportReference(baseMd.ReturnType);
                MethodReference mr = new MethodReference(methodName, baseMd.ReturnType)
                {
                    DeclaringType = baseGit,
                    CallingConvention = baseMd.CallingConvention,
                    HasThis = baseMd.HasThis,
                    ExplicitThis = baseMd.ExplicitThis,
                };
                return mr;
            }
            //Not generic.
            else
            {
                return CodegenSession.ImportReference(baseMd);
            }
        }

        /// <summary>
        /// Returns a method in any inherited classes. The first found method is returned.
        /// </summary>
        public static MethodDefinition GetMethodDefinitionInAnyBase(this TypeDefinition td, string methodName)
        {
            while (td != null)
            {
                foreach (MethodDefinition md in td.Methods)
                {
                    if (md.Name == methodName)
                        return md;
                }

                try
                {
                    td = td.GetNextBaseTypeDefinition();
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the next base type.
        /// </summary>
        internal static TypeDefinition GetNextBaseTypeDefinition(this TypeDefinition typeDef)
        {
            return (typeDef.BaseType == null) ? null : typeDef.BaseType.CachedResolve();
        }


    }


}