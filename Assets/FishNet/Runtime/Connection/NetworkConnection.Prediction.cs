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
        internal void Prediction_Initialize(NetworkManager manager, bool asServer) { }

#if !PREDICTION_V2
        /// <summary>
        /// Local tick when the connection last replicated.
        /// </summary>
        public uint LocalReplicateTick { get; internal set; }

        /// <summary>
        /// Resets NetworkConnection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Prediction_Reset() { }
#else
        /// <summary>
        /// Approximate replicate tick on the server for this connection.
        /// This also contains the last set value for local and remote.
        /// </summary>
        public EstimatedTick ReplicateTick { get; private set; } = new EstimatedTick();
        /// <summary>
        /// Writers for states.
        /// </summary>
        internal List<PooledWriter> PredictionStateWriters = new List<PooledWriter>();

        /// <summary>
        /// Writes a prediction state.
        /// </summary>
        /// <param name="data"></param>
        internal void WriteState(PooledWriter data)
        {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            //Do not send states to clientHost.
            if (IsLocalClient)
                return;
#endif

            TimeManager tm = NetworkManager.TimeManager;
            uint ticksBehind = (IsLocalClient) ? 0 : PacketTick.LocalTickDifference(tm);
            /* If it's been a really long while the client could just be setting up
             * or dropping. Only send if they've communicated within 5 seconds. */
            if (ticksBehind > (tm.TickRate * 5))
                return;

            int mtu = NetworkManager.TransportManager.GetLowestMTU((byte)Channel.Unreliable);
            PooledWriter stateWriter;
            int writerCount = PredictionStateWriters.Count;
            /* Conditions to create a new writer are:
             * - writer does not exist yet.
             * - data length + currentWriter length > mtu */
            if (writerCount == 0 || (data.Length + PredictionStateWriters[writerCount-1].Length) > mtu)
            {
                stateWriter = WriterPool.Retrieve(mtu);
                PredictionStateWriters.Add(stateWriter);
                stateWriter.Reserve(PredictionManager.STATE_HEADER_RESERVE_COUNT);
            }
            else
            {
                stateWriter = PredictionStateWriters[writerCount - 1];
            }

            stateWriter.WriteArraySegment(data.GetArraySegment());
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
            StorePredictionStateWriters();
            ReplicateTick.Reset();
        }
#endif

    }


}
