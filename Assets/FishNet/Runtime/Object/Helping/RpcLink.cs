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
        /// True if link is for an ObserversRpc, false if for a TargetRpc.
        /// </summary>
        public bool ObserversRpc;

        public RpcLink(int objectId, byte componentIndex, uint rpcHash, bool observersRpc)
        {
            ObjectId = objectId;
            ComponentIndex = componentIndex;
            RpcHash = rpcHash;
            ObserversRpc = observersRpc;
        }
    }
    #endregion

}