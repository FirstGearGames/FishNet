using FishNet.Managing.Object;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using UnityEngine;
using System;
using FishNet.Transporting;

namespace FishNet.Managing.Server.Object
{
    public partial class ServerObjects : ManagedObjects
    {

        /// <summary>
        /// Parses a ServerRpc.
        /// </summary>
        /// <param name="data"></param>
        internal void ParseServerRpc(PooledReader reader, int senderClientId, int dataLength, Channel channel)
        {
            NetworkConnection conn;
            if (!NetworkManager.ServerManager.Clients.TryGetValue(senderClientId, out conn))
            {
                if (base.NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"NetworkConnection not found for connection {senderClientId}.");
            }

            int startPosition = reader.Position;
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            if (nb != null)
                nb.OnServerRpc(reader, conn, channel);
            else
                SkipDataLength(PacketId.ServerRpc, reader, startPosition, dataLength);
        }
    }

}