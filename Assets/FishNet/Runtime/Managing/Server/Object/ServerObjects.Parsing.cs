using FishNet.Managing.Object;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using UnityEngine;

namespace FishNet.Managing.Server.Object
{
    public partial class ServerObjects : ManagedObjects
    {

        /// <summary>
        /// Parses a ServerRpc.
        /// </summary>
        /// <param name="data"></param>
        internal void ParseServerRpc(PooledReader reader, int senderClientId)
        {
            NetworkConnection conn;
            if (!NetworkManager.ServerManager.Clients.TryGetValue(senderClientId, out conn))
                Debug.LogError($"NetworkConnection not found for connection {senderClientId}.");

            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            if (nb != null)
                nb.OnServerRpc(reader, conn);
            else
                Debug.LogWarning($"NetworkBehaviour not found for ServerRpc.");
        }
    }

}