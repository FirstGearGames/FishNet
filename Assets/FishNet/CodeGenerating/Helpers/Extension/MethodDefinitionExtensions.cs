using MonoFN.Cecil;

namespace FishNet.CodeGenerating.Helping.Extension
{

    public static class MethodDefinitionExtensions
    {
        /// <summary>
        /// Returns the ParameterDefinition index from end of parameters.
        /// </summary>
        /// <param name="md"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static ParameterDefinition GetEndParameter(this MethodDefinition md, int index)
        {
            //Not enough parameters.
            if (md.Parameters.Count < (index + 1))
                return null;

            return md.Parameters[md.Parameters.Count - (index + 1)];
        }

    }


}