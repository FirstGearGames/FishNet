using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Serializing;
using GameKit.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Cached expected level of detail value.
        /// </summary>
        private uint _cachedLevelOfDetailInterval;
        /// <summary>
        /// Cached value of UseLod.
        /// </summary>
        private bool _cachedUseLod;
        #endregion

        /// <summary>
        /// Parses a received network LOD update.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseNetworkLODUpdate(PooledReader reader, NetworkConnection conn)
        {
            
        }


    }


}
