﻿using FishNet.Object;
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
            * past the broadcast data. 
            *
            * Reliables also need length read in the instance a client
            * sends data to an object which server is despawning. Without
            * parsing length the remainer data from client will be corrupt. */
            PacketId pid = (PacketId)packetId;
            if (channel == Channel.Reliable ||
                pid == PacketId.Broadcast ||
                pid == PacketId.SyncVar
                )
            {
                return reader.ReadInt32();
            }
            //Unreliable purges remaining.
            else if (channel == Channel.Unreliable)
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
                if (reader.NetworkManager != null)
                    reader.NetworkManager.LogError(message);
                else
                    NetworkManager.StaticLogError(message);
            }

        }

    }


}