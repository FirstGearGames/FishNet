using FishNet.Managing;
using FishNet.Managing.Predicting;
using FishNet.Managing.Timing;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Connection
{

    /// <summary>
    /// A container for a connected client used to perform actions on and gather information for the declared client.
    /// </summary>
    public partial class NetworkConnection
    {
        private int _highestQueueCount;
        private uint _lastHighestQueueCountUpdateTick;
        internal void SetHighestQueueCount(int value, uint serverTick)
        {
            if (serverTick != _lastHighestQueueCountUpdateTick)
                _highestQueueCount = 0;
            _lastHighestQueueCountUpdateTick = serverTick;

            _highestQueueCount = Mathf.Max(_highestQueueCount, value);
        }
        /// <summary>
        /// Returns the highest queue count after resetting it.
        /// </summary>
        /// <returns></returns>
        internal int GetAndResetHighestQueueCount()
        {
            int value = _highestQueueCount;
            _highestQueueCount = 0;
            _lastHighestQueueCountUpdateTick = 0;
            return value;
        }

#if !PREDICTION_V2
        /// <summary>
        /// Local tick when the connection last replicated.
        /// </summary>
        public uint LocalReplicateTick { get; internal set; }

        /// <summary>
        /// Resets NetworkConnection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Prediction_Reset()
        {
            GetAndResetHighestQueueCount();
        }
#else
        /// <summary>
        /// Approximate replicate tick on the server for this connection.
        /// This also contains the last set value for local and remote.
        /// </summary>
        public EstimatedTick ReplicateTick;
        /// <summary>
        /// Writers for states.
        /// </summary>
        internal List<PooledWriter> PredictionStateWriters = new List<PooledWriter>();

        /// <summary>
        /// Writes a prediction state.
        /// </summary>
        /// <param name="writer"></param>
        internal void WriteState(PooledWriter writer)
        {
            //Do not send states to clientHost.
            if (IsLocalClient)
                return;

            TimeManager tm = NetworkManager.TimeManager;
            uint ticksBehind = PacketTick.LocalTickDifference(tm);
            if (ticksBehind > 0)
                return;
            /* If it's been a really long while the client could just be setting up
             * or dropping. Only send if they've communicated within 15 seconds. */
            if (ticksBehind > (tm.TickRate * 15))
                return;

            int mtu = NetworkManager.TransportManager.GetMTU((byte)Channel.Unreliable);
            PooledWriter stateWriter;
            int writerCount = PredictionStateWriters.Count;
            if (writerCount == 0 || (writer.Length + PredictionManager.STATE_HEADER_RESERVE_COUNT) > mtu)
            {
                stateWriter = WriterPool.Retrieve(mtu);
                PredictionStateWriters.Add(stateWriter);

                stateWriter.Reserve(PredictionManager.STATE_HEADER_RESERVE_COUNT);
                //Estimated replicate tick on the client.
                stateWriter.WriteTickUnpacked(ReplicateTick.Value(NetworkManager.TimeManager));
                /* No need to send localTick here, it can be read from LastPacketTick that's included with every packet.
                 * Note: the LastPacketTick we're sending here is the last packet received from this connection.
                 * The server and client ALWAYS prefix their packets with their local tick, which is
                 * what we are going to use for the last packet tick from the server. */
            }
            else
            {
                stateWriter = PredictionStateWriters[writerCount - 1];
            }

            stateWriter.WriteArraySegment(writer.GetArraySegment());
        }

        /// <summary>
        /// Stores prediction writers to be re-used later.
        /// </summary>
        internal void StorePredictionStateWriters()
        {
            for (int i = 0; i < PredictionStateWriters.Count; i++)
                WriterPool.Store(PredictionStateWriters[i]);

            PredictionStateWriters.Clear();
        }

        /// <summary>
        /// Resets NetworkConnection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Prediction_Reset()
        {
            GetAndResetHighestQueueCount();
            StorePredictionStateWriters();
            ReplicateTick.Reset();
        }
#endif

    }


}
