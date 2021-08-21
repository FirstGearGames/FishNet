using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Object.Helping;


namespace FishNet.CodeGenerating.Helping
{

    public class AttributeHelper
    {
        #region Reflection references.
        private string ServerAttribute_FullName;
        private string ClientAttribute_FullName;
        private string ServerRpcAttribute_FullName;
        private string ObserversRpcAttribute_FullName;
        private string TargetRpcAttribute_FullName;
        private string SyncVarAttribute_FullName;
        private string SyncObjectAttribute_FullName;
        #endregion   

        internal bool ImportReferences()
        {
            ServerAttribute_FullName = typeof(ServerAttribute).FullName;
            ClientAttribute_FullName = typeof(ClientAttribute).FullName;
            ServerRpcAttribute_FullName = typeof(ServerRpcAttribute).FullName;
            ObserversRpcAttribute_FullName = typeof(ObserversRpcAttribute).FullName;
            TargetRpcAttribute_FullName = typeof(TargetRpcAttribute).FullName;
            SyncVarAttribute_FullName = typeof(SyncVarAttribute).FullName;
            SyncObjectAttribute_FullName = typeof(SyncObjectAttribute).FullName;

            return true;
        }

        /// <summary>
        /// Returns type of Rpc attributeFullName is for.
        /// </summary>
        /// <param name="attributeFullName"></param>
        /// <returns></returns>
        public RpcType GetRpcAttributeType(string attributeFullName)
        {
            if (attributeFullName == ServerRpcAttribute_FullName)
                return RpcType.Server;
            else if (attributeFullName == ObserversRpcAttribute_FullName)
                return RpcType.Observers;
            else if (attributeFullName == TargetRpcAttribute_FullName)
                return RpcType.Target;
            else
                return RpcType.None;
        }


        /// <summary>
        /// Returns type of Rpc attributeFullName is for.
        /// </summary>
        /// <param name="attributeFullName"></param>
        /// <returns></returns>
        internal QolAttributeType GetQolAttributeType(string attributeFullName)
        {
            if (attributeFullName == ServerAttribute_FullName)
                return QolAttributeType.Server;
            else if (attributeFullName == ClientAttribute_FullName)
                return QolAttributeType.Client;
            else
                return QolAttributeType.None;
        }


        /// <summary>
        /// Returns if attribute if a SyncVarAttribute.
        /// </summary>
        /// <param name="attributeFullName"></param>
        /// <returns></returns>
        public bool IsSyncVarAttribute(string attributeFullName)
        {
            return (attributeFullName == SyncVarAttribute_FullName);
        }

        /// <summary>
        /// Returns if attribute if a SyncObjectAttribute.
        /// </summary>
        /// <param name="attributeFullName"></param>
        /// <returns></returns>
        public bool IsSyncObjectAttribute(string attributeFullName)
        {
            return (attributeFullName == SyncObjectAttribute_FullName);
        }
    }

}