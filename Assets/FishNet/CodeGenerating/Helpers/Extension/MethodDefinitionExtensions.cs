//using Mono.Cecil;

//namespace FishNet.CodeGenerating.Helping.Extension
//{

//    public static class MethodDefinitionExtensions
//    {
//        /// <summary>
//        /// Adds a parameter to methodDef.
//        /// </summary>
//        /// <typeparam name="T"></typeparam>
//        /// <param name="methodDef"></param>
//        /// <param name="name"></param>
//        /// <param name="attributes"></param>
//        /// <returns></returns>
//        public static ParameterDefinition AddParam<T>(this MethodDefinition methodDef, string name, ParameterAttributes attributes = ParameterAttributes.None)
//        {
//            return AddParam(methodDef, methodDef.Module.ImportReference(typeof(T)), name, attributes);
//        }

//        /// <summary>
//        /// Adds a parameter to methodDef.
//        /// </summary>
//        /// <param name="methodDef"></param>
//        /// <param name="typeRef"></param>
//        /// <param name="name"></param>
//        /// <param name="attributes"></param>
//        /// <returns></returns>
//        public static ParameterDefinition AddParam(this MethodDefinition methodDef, TypeReference typeRef, string name, ParameterAttributes attributes = ParameterAttributes.None)
//        {
//            var param = new ParameterDefinition(name, attributes, typeRef);
//            methodDef.Parameters.Add(param);
//            return param;
//        }

//    }


//}