using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Object
{
    public delegate void ServerRpcDelegate(NetworkBehaviour obj, PooledReader reader, Channel channel, NetworkConnection sender);
    public delegate void ClientRpcDelegate(NetworkBehaviour obj, PooledReader reader, Channel channel);

}