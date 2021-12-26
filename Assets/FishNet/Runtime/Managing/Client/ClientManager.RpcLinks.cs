using FishNet.Transporting;
using System;
using System.Linq;
using UnityEngine;

namespace FishNet.Managing.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Starting packetId value for RPCLinks.
        /// </summary>
        private ushort _startingLinkIndex;
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        private void InitializeOnceRpcLinks()
        {
            /* Brute force enum values. 
             * Linq Last/Max lookup throws for IL2CPP. */
            ushort highestValue = 0;
            Array pidValues = Enum.GetValues(typeof(PacketId));
            foreach (PacketId pid in pidValues)
                highestValue = Math.Max(highestValue, (ushort)pid);

            highestValue += 1;
            _startingLinkIndex = highestValue;
        }

    }


}
