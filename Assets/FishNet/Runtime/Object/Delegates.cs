using FishNet.Connection;
using FishNet.Serializing;

namespace FishNet.Object
{
    public delegate void ServerRpcDelegate(NetworkBehaviour obj, PooledReader reader, NetworkConnection sender);
    public delegate void ClientRpcDelegate(NetworkBehaviour obj, PooledReader reader);
    public delegate void SyncTypeDelegate(PooledReader reader);
}