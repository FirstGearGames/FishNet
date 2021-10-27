using MonoFN.Cecil;
using System;

namespace FishNet.CodeGenerating.Helping.Extension
{

    internal static class ParameterDefinitionExtensions
    {
        /// <summary>
        /// Returns if parameterDef is Type.
        /// </summary>
        /// <param name="parameterDef"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool Is(this ParameterDefinition parameterDef, Type type)
        {
            return parameterDef.ParameterType.FullName == type.FullName;
        }


    }


}