using FishNet.CodeGenerating.Helping.Extension;
using MonoFN.Cecil;
using MonoFN.Cecil.Rocks;
using MonoFN.Collections.Generic;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Extension
{


    internal static class MethodDefinitionExtensions
    {

        /// <summary>
        /// Returns a method reference while considering if declaring type is generic.
        /// </summary>
        public static MethodReference GetMethodReference(this MethodDefinition md, CodegenSession session)
        {
            MethodReference methodRef = session.ImportReference(md);

            //Is generic.
            if (md.DeclaringType.HasGenericParameters)
            {
                GenericInstanceType git = methodRef.DeclaringType.MakeGenericInstanceType();
                MethodReference result = new MethodReference(md.Name, md.ReturnType)
                {
                    HasThis = md.HasThis,
                    ExplicitThis = md.ExplicitThis,
                    DeclaringType = git,
                    CallingConvention = md.CallingConvention,
                };
                foreach (ParameterDefinition pd in md.Parameters)
                {
                    session.ImportReference(pd.ParameterType);
                    result.Parameters.Add(pd);
                }
                return result;
            }
            else
            {
                return methodRef;
            }
        }


        /// <summary>
        /// Returns a method reference for a generic method.
        /// </summary>
        public static MethodReference GetMethodReference(this MethodDefinition md, CodegenSession session, TypeReference typeReference)
        {
            MethodReference methodRef = session.ImportReference(md);
            return methodRef.GetMethodReference(session, typeReference);
        }


        /// <summary>
        /// Returns a method reference for a generic method.
        /// </summary>
        public static MethodReference GetMethodReference(this MethodDefinition md, CodegenSession session, TypeReference[] typeReferences)
        {
            MethodReference methodRef = session.ImportReference(md);
            return methodRef.GetMethodReference(session, typeReferences);
        }


    }


}