using MonoFN.Cecil;
using MonoFN.Cecil.Cil;

namespace FishNet.CodeGenerating.Helping.Extension
{

    internal static class MethodDefinitionExtensions
    {
        /// <summary>
        /// Clears the method content and returns ret.
        /// </summary>
        internal static void ClearMethodWithRet(this MethodDefinition md, ModuleDefinition importReturnModule = null)
        {
            md.Body.Instructions.Clear();
            ILProcessor processor = md.Body.GetILProcessor();
            processor.Add(CodegenSession.ObjectHelper.CreateRetDefault(md, importReturnModule));
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
        internal static VariableDefinition CreateVariable(this MethodDefinition methodDef, System.Type variableType)
        {
            return CreateVariable(methodDef, CodegenSession.GeneralHelper.GetTypeReference(variableType));
        }
    }


}