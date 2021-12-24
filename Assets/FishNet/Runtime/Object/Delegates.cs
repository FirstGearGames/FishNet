using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object.Delegating
{
    public delegate void ServerRpcDelegate(NetworkBehaviour obj, PooledReader reader, Channel channel, NetworkConnection sender);
    public delegate void ClientRpcDelegate(NetworkBehaviour obj, PooledReader reader, Channel channel);

}