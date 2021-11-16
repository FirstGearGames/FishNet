using FishNet.Object.Helping;

namespace FishNet.Object
{

    #region Types.
    /// <summary>
    /// Lookup data for a RPC Link.
    /// </summary>
    internal struct RpcLink
    {
        /// <summary>
        /// ObjectId for link.
        /// </summary>
        public int ObjectId;
        /// <summary>
        /// NetworkBehaviour component index on ObjectId.
        /// </summary>
        public byte ComponentIndex;
        /// <summary>
        /// RpcHash for link.
        /// </summary>
        public uint RpcHash;
        /// <summary>
        /// Type of Rpc link is for.
        /// </summary>
        public RpcType RpcType;

        public RpcLink(int objectId, byte componentIndex, uint rpcHash, RpcType rpcType)
        {
            ObjectId = objectId;
            ComponentIndex = componentIndex;
            RpcHash = rpcHash;
            RpcType = rpcType;
        }
    }
    #endregion

}