using FishNet.Object.Helping;

namespace FishNet.Object
{


    internal struct RpcLinkType
    {
        /// <summary>
        /// Index of link.
        /// </summary>
        public ushort LinkIndex;
        /// <summary>
        /// Type of Rpc link is for.
        /// </summary>
        public RpcType RpcType;

        public RpcLinkType(ushort linkIndex, RpcType rpcType)
        {
            LinkIndex = linkIndex;
            RpcType = rpcType;
        }
    }

}