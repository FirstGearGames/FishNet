using FishNet.Transporting;
using System;
using System.Linq;

namespace FishNet.Managing.Client
{
    public partial class ClientManager
    {
        #region Private.
        /// <summary>
        /// Starting packetId value for RPCLinks.
        /// </summary>
        private ushort _startingLinkIndex = 0;
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
