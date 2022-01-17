using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Managing.Utility
{

    public class Packets
    {
        /// <summary>
        /// Returns written data length for packet.
        /// </summary>
        internal static int GetPacketLength(PacketId packetId, PooledReader reader, Channel channel)
        {
            return (channel == Channel.Reliable || packetId == PacketId.Broadcast) ?
                (int)UnreliablePacketLength.ReliableOrBroadcast - 1 : reader.ReadInt16();
        }

    }


}