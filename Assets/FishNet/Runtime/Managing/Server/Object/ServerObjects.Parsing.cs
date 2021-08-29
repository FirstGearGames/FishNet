using FishNet.Managing.Object;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using UnityEngine;
using System;

namespace FishNet.Managing.Server.Object
{
    public partial class ServerObjects : ManagedObjects
    {

        /// <summary>
        /// Parses a ServerRpc.
        /// </summary>
        /// <param name="data"></param>
        internal void ParseServerRpc(PooledReader reader, int senderClientId, int dataLength)
        {
            NetworkConnection conn;
            if (!NetworkManager.ServerManager.Clients.TryGetValue(senderClientId, out conn))
                Debug.LogWarning($"NetworkConnection not found for connection {senderClientId}.");

            int startPosition = reader.Position;
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            if (nb != null)
            {
                nb.OnServerRpc(reader, conn);
            }
            else
            {
                if (dataLength == -1)
                {
                    Debug.LogWarning($"NetworkBehaviour could not be found for ObserversRpc.");
                }
                else
                {
                    reader.Position = startPosition;
                    reader.Skip(Math.Min(dataLength, reader.Remaining));
                }
            }
        }
    }

}