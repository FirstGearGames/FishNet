using FishNet.Managing.Predicting;
using FishNet.Serializing;
using UnityEngine;

namespace FishNet.Connection
{

    /// <summary>
    /// A container for a connected client used to perform actions on and gather information for the declared client.
    /// </summary>
    public partial class NetworkConnection
    {
#if PREDICTION_V2
        internal PooledWriter PredictionStateWriter = WriterPool.GetWriter(1000);

        internal void WriteState(PooledWriter writer)
        {
            //Do not send states to clientHost.
            if (IsLocalClient)
                return;

            PooledWriter predictionWriter = PredictionStateWriter;
            //If no length this is the first write for the tick.
            if (predictionWriter.Length == 0)
            {
                /* If clients lastpackettick was not set this tick
                 * then the server doesn't know with absolute certainty
                 * if the data it would be sending to the client is for
                 * their lastpackettick. NetworkConnection.LocalTick guestimates
                 * client lastpackettick based on past time and lastpackettick but
                 * there's no way to know with certainty of the accuracy.
                 * Sending states with the wrong tick could drastically misalign
                 * the simulation for the client. Because of this only send states
                 * if received a packet from client this tick, giving us an exact
                 * tick the client is on. */
                if (!UpdatedLastPacketTick)
                    return;

                /* Reserve 5 for the amount of data written and packetId.
                 * This will be send as an unpacked int. This is done
                 * instead of copying into a new writer with length to save CPU perf
                 * at the cost of 4 bytes. */
                predictionWriter.Reserve(PredictionManager.STATE_HEADER_RESERVE_COUNT);
                predictionWriter.WriteTickUnpacked(LastReplicateTick);
                /* No need to send localTick here, it can be read from LastPacketTick that's included with every packet.
                 * Note: the LastPacketTick we're sending here is the last packet received from this connection.
                 * The server and client ALWAYS prefix their packets with their local tick, which is
                 * what we are going to use for the last packet tick from the server. */
            }

            predictionWriter.WriteArraySegment(writer.GetArraySegment());
        }

        internal uint LastReplicateTick;

        private void Prediction_Reset()
        {
            PredictionStateWriter.Reset();
        }
#endif

    }


}
