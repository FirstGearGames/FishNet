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
        /// Returns the proper OpCode to use for call methods.
        /// </summary>
        public static MonoFN.Cecil.Cil.OpCode GetCallOpCode(this MethodDefinition md)
        {
            if (md.Attributes.HasFlag(MethodAttributes.Virtual))
                return MonoFN.Cecil.Cil.OpCodes.Callvirt;
            else
                return MonoFN.Cecil.Cil.OpCodes.Call;
        }
        /// <summary>
        /// Returns the proper OpCode to use for call methods.
        /// </summary>
        public static MonoFN.Cecil.Cil.OpCode GetCallOpCode(this MethodReference mr, CodegenSession session)
        {
            return mr.CachedResolve(session).GetCallOpCode();
        }

        /// <summary>
        /// Adds otherMd parameters to thisMR and returns added parameters.
        /// </summary>
        public static List<ParameterDefinition> CreateParameters(this MethodReference thisMr, CodegenSession session, MethodDefinition otherMd)
        {
            return thisMr.CachedResolve(session).CreateParameters(session, otherMd);
        }
        /// <summary>
        /// Adds otherMr parameters to thisMR and returns added parameters.
        /// </summary>
        public static List<ParameterDefinition> CreateParameters(this MethodReference thisMr, CodegenSession session, MethodReference otherMr)
        {
            return thisMr.CachedResolve(session).CreateParameters(session, otherMr.CachedResolve(session));
        }

        /// <summary>
        /// Adds otherMd parameters to thisMd and returns added parameters.
        /// </summary>
        public static List<ParameterDefinition> CreateParameters(this MethodDefinition thisMd, CodegenSession session, MethodDefinition otherMd)
        {
            List<ParameterDefinition> results = new List<ParameterDefinition>();

            foreach (ParameterDefinition pd in otherMd.Parameters)
            {
                session.ImportReference(pd.ParameterType);
                int currentCount = thisMd.Parameters.Count;
                string name = (pd.Name + currentCount);
                ParameterDefinition parameterDef = new ParameterDefinition(name, pd.Attributes, pd.ParameterType);
                //Set any default values.
                parameterDef.Constant = pd.Constant;
                parameterDef.IsReturnValue = pd.IsReturnValue;
                parameterDef.IsOut = pd.IsOut;
                foreach (CustomAttribute item in pd.CustomAttributes)
                    parameterDef.CustomAttributes.Add(item);
                parameterDef.HasConstant = pd.HasConstant;
                parameterDef.HasDefault = pd.HasDefault;
                
                thisMd.Parameters.Add(parameterDef);

                results.Add(parameterDef);
            }

            return results;
        }

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