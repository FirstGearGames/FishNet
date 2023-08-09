using FishNet.Transporting;
using GameKit.Utilities;

namespace FishNet.Serializing.Helping
{

    internal static class Broadcasts
    {
        /// <summary>
        /// Writes a broadcast to writer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="writer"></param>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        internal static PooledWriter WriteBroadcast<T>(PooledWriter writer, T message, Channel channel)
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

            dataWriter.Store();

            return writer;
        }
    }

}