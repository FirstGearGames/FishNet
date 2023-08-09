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
        /// <summary>
        /// Average number of replicates in queue for the past x received replicates.
        /// </summary>
        private MovingAverage _replicateQueueAverage;
        /// <summary>
        /// Last tick replicateQueueAverage was updated.
        /// </summary>
        private uint _lastAverageQueueAddTick;

        internal void Prediction_Initialize(NetworkManager manager, bool asServer)
        {
            if (asServer)
            {
                int movingAverageCount = (int)Mathf.Max((float)manager.TimeManager.TickRate * 0.25f, 3f);
                _replicateQueueAverage = new MovingAverage(movingAverageCount);
            }
        }


        /// <summary>
        /// Adds to the average number of queued replicates.
        /// </summary>
        internal void AddAverageQueueCount(ushort value, uint tick)
        {
            /* If have not added anything to the averages for several ticks
             * then reset average. */
            if ((tick - _lastAverageQueueAddTick) > _replicateQueueAverage.SampleSize)
                _replicateQueueAverage.Reset();
            _lastAverageQueueAddTick = tick;

            _replicateQueueAverage.ComputeAverage((float)value);
        }

        /// <summary>
        /// Returns the highest queue count after resetting it.
        /// </summary>
        /// <returns></returns>
        internal ushort GetAndResetAverageQueueCount()
        {
            if (_replicateQueueAverage == null)
                return 0;

            int avg = (int)(_replicateQueueAverage.Average);
            if (avg < 0)
                avg = 0;

            return (ushort)avg;
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
            GetAndResetAverageQueueCount();
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
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            //Do not send states to clientHost.
            if (IsLocalClient)
                return;
#endif

            TimeManager tm = NetworkManager.TimeManager;
            uint ticksBehind = (IsLocalClient) ? 0 : PacketTick.LocalTickDifference(tm);
            //if (ticksBehind > 0)
            //    return;
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

                uint clientReplicateTick;
                //If client has performed a replicate.
                if (!ReplicateTick.IsUnset)
                    clientReplicateTick = ReplicateTick.Value(NetworkManager.TimeManager);
                /* If not then use what is estimated to be the clients
                 * current tick along with desired prediction queue count.
                 * This should be just about the same as if the client used replicate,
                 * but even if it's not it doesn't matter because the client
                 * isn't replicating himself, just reconciling and replaying other objects. */
                else
                    clientReplicateTick = (PacketTick.Value(NetworkManager.TimeManager) + NetworkManager.PredictionManager.QueuedInputs);
                //Estimated replicate tick on the client.
                stateWriter.WriteTickUnpacked(clientReplicateTick);
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
            GetAndResetAverageQueueCount();
            StorePredictionStateWriters();
            ReplicateTick.Reset();
        }
#endif

    }


}
