using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Serializing;
using FishNet.Utility.Constant;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object.Prediction.Delegating
{
    [APIExclude]
    public delegate void ReplicateRpcDelegate(NetworkBehaviour obj, PooledReader reader, NetworkConnection sender);
    [APIExclude]
    public delegate void ReconcileRpcDelegate(NetworkBehaviour obj, PooledReader reader);

}