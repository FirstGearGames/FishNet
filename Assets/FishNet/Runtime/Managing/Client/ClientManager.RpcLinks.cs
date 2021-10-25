using FishNet.Serializing;
using System;
using FishNet.Transporting;
using System.Linq;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

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
            _startingLinkIndex = (ushort)(1 + (ushort)Enum.GetValues(typeof(PacketId)).Cast<PacketId>().Max());
        }

    }


}
