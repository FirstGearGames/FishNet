using Mono.Cecil;
using FishNet.Connection;

namespace FishNet.CodeGenerating.Helping
{
    internal class ConnectionHelper
    {
        #region Reflection references.
        private TypeReference NetworkConnection_TypeRef;
        #endregion

        /// <summary>
        /// Imports references needed by this helper.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        internal bool ImportReferences()
        {         
            NetworkConnection_TypeRef = CodegenSession.Module.ImportReference(typeof(NetworkConnection));

            return true;
        }

    }
}