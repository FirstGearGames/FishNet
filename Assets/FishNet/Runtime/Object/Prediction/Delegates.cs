using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object.Prediction.Delegating
{
    public delegate void ReplicateRpcDelegate(NetworkBehaviour obj, PooledReader reader, NetworkConnection sender);
    public delegate void ReconcileRpcDelegate(NetworkBehaviour obj, PooledReader reader);

}