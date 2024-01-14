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


    }


}
