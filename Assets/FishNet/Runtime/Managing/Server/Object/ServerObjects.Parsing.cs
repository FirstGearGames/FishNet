using FishNet.Managing.Object;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using UnityEngine;
using System;
using FishNet.Transporting;
using FishNet.Managing.Utility;

namespace FishNet.Managing.Server
{
    public partial class ServerObjects : ManagedObjects
    {

        /// <summary>
        /// Parses a ServerRpc.
        /// </summary>
        /// <param name="data"></param>
        internal void ParseServerRpc(PooledReader reader, int senderClientId, Channel channel)
        {
            NetworkConnection conn;
            if (!NetworkManager.ServerManager.Clients.TryGetValue(senderClientId, out conn))
            {
                if (base.NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"NetworkConnection not found for connection {senderClientId}.");
            }

            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            int dataLength = Packets.GetPacketLength(PacketId.TargetRpc, reader, channel);
            if (nb != null)
                nb.OnServerRpc(reader, conn, channel);
            else
                SkipDataLength(PacketId.ServerRpc, reader, dataLength);
        }
    }

}