using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using GameKit.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        #region Internal.
        /// <summary>
        /// How many ticks between each LOD update.
        /// </summary>
        public uint LevelOfDetailInterval => NetworkManager.TimeManager.TimeToTicks(0.5d, TickRounding.RoundUp);
        #endregion

        #region Private.
        /// <summary>
        /// Positions of the player objects.
        /// </summary>
        private List<Vector3> _objectsPositionsCache = new List<Vector3>();
        /// <summary>
        /// Next index within Spawned to update the LOD on.
        /// </summary>
        private int _nextLodNobIndex;
        #endregion

        /// <summary>
        /// Sends a level of update if conditions are met.
        /// </summary>
        /// <param name="forceFullUpdate">True to force send a full update immediately.
        /// This may be useful when teleporting clients.
        /// This must be called by on the server by using ServerManager.ForceLodUpdate(NetworkConnection).
        /// </param>
        internal void TrySendLodUpdate(uint localTick, bool forceFullUpdate)
        {
            
        }


    }


}
