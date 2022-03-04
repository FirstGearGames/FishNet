using FishNet.Connection;
using FishNet.Managing.Object;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Runtime.CompilerServices;

namespace FishNet.Managing.Server
{
    public partial class ServerObjects : ManagedObjects
    {

        /// <summary>
        /// Parses a ReplicateRpc.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ParseReplicateRpc(PooledReader reader, NetworkConnection conn, Channel channel)
        {
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            int dataLength = Packets.GetPacketLength((ushort)PacketId.ServerRpc, reader, channel);

            if (nb != null)
                nb.OnReplicateRpc(null, reader, conn, channel);
            else
                SkipDataLength((ushort)PacketId.ServerRpc, reader, dataLength);
        }

        /// <summary>
        /// Parses a ServerRpc.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ParseServerRpc(PooledReader reader, NetworkConnection conn, Channel channel)
        {
            NetworkBehaviour nb = reader.ReadNetworkBehaviour();
            int dataLength = Packets.GetPacketLength((ushort)PacketId.ServerRpc, reader, channel);

            if (nb != null)
                nb.OnServerRpc(reader, conn, channel);
            else
                SkipDataLength((ushort)PacketId.ServerRpc, reader, dataLength);
        }
    }

}