using FishNet.Managing;
using FishNet.Transporting;
using GameKit.Utilities;

namespace FishNet.Serializing.Helping
{

    internal static class Broadcasts
    {
        /// <summary>
        /// Writes a broadcast to writer.
        /// </summary>
        internal static PooledWriter WriteBroadcast<T>(NetworkManager networkManager, PooledWriter writer, T message, ref Channel channel)
        {
            writer.WritePacketId(PacketId.Broadcast);
            writer.WriteUInt16(typeof(T).FullName.GetStableHashU16());
            //Write data to a new writer.
            PooledWriter dataWriter = WriterPool.Retrieve();
            dataWriter.Write<T>(message);
            //Write length of data.
            writer.WriteLength(dataWriter.Length);
            //Write data.
            writer.WriteArraySegment(dataWriter.GetArraySegment());
            //Update channel to reliable if needed.
            networkManager.TransportManager.CheckSetReliableChannel(writer.Length, ref channel);

            dataWriter.Store();

            return writer;
        }
    }

}