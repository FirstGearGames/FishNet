using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Managing.Utility
{

    public class Packets
    {
        /// <summary>
        /// Returns written data length for packet.
        /// </summary>
        internal static int GetPacketLength(ushort packetId, PooledReader reader, Channel channel)
        {
            /* Broadcast is a special circumstance where data
            * will not be purged even if unreliable.
            * This is because a broadcast receiver may not
            * be set, which could be intentional. Because of this
            * length is always sent to skip
            * past the broadcast data. */
            if ((PacketId)packetId == PacketId.Broadcast)
            {
                return reader.ReadInt32();
            }
            //Reliables should never be missing. No length required.
            else if (channel == Channel.Reliable)
            {
                return (int)MissingObjectPacketLength.Reliable;
                //return reader.ReadInt32();
            }
            //Unreliable purges remaining.
            if (channel == Channel.Unreliable)
            {
                return (int)MissingObjectPacketLength.PurgeRemaiming;
            }
            /* Unhandled. This shouldn't be possible
             * since both reliable and unreliable is checked.
             * There are no other options. This is merely here
             * for a sanity check. */
            else
            {
                LogError($"Operation is unhandled for packetId {(PacketId)packetId} on channel {channel}.");
                return (int)MissingObjectPacketLength.PurgeRemaiming;
            }

            //Logs an error message.
            void LogError(string message)
            {
                bool canLog;
                if (reader.NetworkManager != null)
                    canLog = reader.NetworkManager.CanLog(Logging.LoggingType.Error);
                else
                    canLog = NetworkManager.StaticCanLog(Logging.LoggingType.Error);

                if (canLog)
                    Debug.LogError(message);
            }

        }

    }


}