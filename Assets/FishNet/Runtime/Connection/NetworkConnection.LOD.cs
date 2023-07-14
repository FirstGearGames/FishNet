using FishNet.Object;
using GameKit.Utilities;
using System;
using System.Collections.Generic;

namespace FishNet.Connection
{

    /// <summary>
    /// A container for a connected client used to perform actions on and gather information for the declared client.
    /// </summary>
    public partial class NetworkConnection
    {

        public class LevelOfDetailData : IResettable
        {
            /// <summary>
            /// Current level of detail for a NetworkObject.
            /// </summary>
            public byte CurrentLevelOfDetail;
            /// <summary>
            /// Previous level of detail for a NetworkObject.
            /// </summary>
            public byte PreviousLevelOfDetail;

            internal void Update(byte lodLevel)
            {
                PreviousLevelOfDetail = CurrentLevelOfDetail;
                CurrentLevelOfDetail = lodLevel;
            }

            public void ResetState()
            {
                CurrentLevelOfDetail = 0;
                PreviousLevelOfDetail = 0;
            }

            public void InitializeState() { }

        }
        /// <summary>
        /// Level of detail for each NetworkObject.
        /// Since this is called frequently this field is intentionally not an accessor to increase performance.
        /// </summary>
        public Dictionary<NetworkObject, LevelOfDetailData> LevelOfDetails = new Dictionary<NetworkObject, LevelOfDetailData>(new NetworkObjectIdComparer());
        /// <summary>
        /// Number oftimes this connection may send a forced LOD update.
        /// </summary>
        internal int AllowedForcedLodUpdates;
        /// <summary>
        /// Last tick an LOD was sent.
        /// On client and clientHost this is LocalTick.
        /// On server only this is LastPacketTick for the connection.
        /// </summary>
        internal uint LastLevelOfDetailUpdate;
        /// <summary>
        /// Returns if the client has not sent an LOD update for expectedInterval.
        /// </summary>
        /// <returns></returns>
        internal bool IsLateForLevelOfDetail(uint expectedInterval)
        {
            //Local client is immune since server and client share ticks.
            if (IsLocalClient)
                return false;

            uint lastPacketTick = PacketTick.RemoteTick;
            return ((lastPacketTick - LastLevelOfDetailUpdate) > expectedInterval);
        }
        
        /// <summary>
        /// Number of level of detail update infractions for this connection.
        /// </summary>
        internal int LevelOfDetailInfractions;

        private void ResetStates_Lod()
        {
            foreach (LevelOfDetailData data in LevelOfDetails.Values)
                ResettableObjectCaches<LevelOfDetailData>.Store(data);

            LevelOfDetails.Clear();
        }
    }


}