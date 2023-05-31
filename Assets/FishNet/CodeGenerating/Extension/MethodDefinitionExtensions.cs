using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Cecil.Rocks;
using MonoFN.Collections.Generic;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Extension
{


    internal static class MethodDefinitionExtensions
    {
        public const MethodAttributes PUBLIC_VIRTUAL_ATTRIBUTES = (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig);
        public const MethodAttributes PROTECTED_VIRTUAL_ATTRIBUTES = (MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig);

        /// <summary>
        /// Returns a custom attribute.
        /// </summary>
        public static CustomAttribute GetCustomAttribute(this MethodDefinition md, string attributeFullName)
        {
            if (md == null)
                return null;

            foreach (CustomAttribute item in md.CustomAttributes)
            {
                if (item.AttributeType.FullName == attributeFullName)
                    return item;
            }

            //Not found.
            return null;
        }

        /// <summary>
        /// Clears the method content and returns ret.
        /// </summary>
        internal static void ClearMethodWithRet(this MethodDefinition md, CodegenSession session, ModuleDefinition importReturnModule = null)
        {
            md.Body.Instructions.Clear();
            ILProcessor processor = md.Body.GetILProcessor();
            processor.Add(session.GetClass<GeneralHelper>().CreateRetDefault(md, importReturnModule));
        }

        /// <summary>
        /// Returns the ParameterDefinition index from end of parameters.
        /// </summary>
        /// <param name="md"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        internal static ParameterDefinition GetEndParameter(this MethodDefinition md, int index)
        {
            //Not enough parameters.
            if (md.Parameters.Count < (index + 1))
                return null;

            return md.Parameters[md.Parameters.Count - (index + 1)];
        }


        /// <summary>
        /// Creates a variable type within the body and returns it's VariableDef.
        /// </summary>
        internal static VariableDefinition CreateVariable(this MethodDefinition methodDef, TypeReference variableTypeRef)
        {
            VariableDefinition variableDef = new VariableDefinition(variableTypeRef);
            methodDef.Body.Variables.Add(variableDef);
            return variableDef;
        }

        /// <summary>
        /// Creates a variable type within the body and returns it's VariableDef.
        /// </summary>
        internal static VariableDefinition CreateVariable(this MethodDefinition methodDef, CodegenSession session, System.Type variableType)
        {
            return CreateVariable(methodDef, session.GetClass<GeneralHelper>().GetTypeReference(variableType));
        }

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

        /// <summary>
        /// Makes a method definition public.
        /// </summary>
        public static void SetPublicAttributes(this MethodDefinition md)
        {
            md.Attributes = PUBLIC_VIRTUAL_ATTRIBUTES;
        }
        public static void SetProtectedAttributes(this MethodDefinition md)
        {
            md.Attributes = PROTECTED_VIRTUAL_ATTRIBUTES;
        }
    }


}