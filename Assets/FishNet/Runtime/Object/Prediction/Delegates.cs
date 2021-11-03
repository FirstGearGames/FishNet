using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(Constants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object.Prediction.Delegating
{
    public delegate void ReplicateDelegate(NetworkBehaviour obj, PooledReader reader, Channel channel, NetworkConnection sender);
    public delegate void ReconcileDelegate(NetworkBehaviour obj, PooledReader reader, Channel channel);

}