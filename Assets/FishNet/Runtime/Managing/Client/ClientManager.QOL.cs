using UnityEngine;

namespace FishNet.Managing.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Last tick the local client had received data.
        /// </summary>
        public uint LastPacketLocalTick { get; private set; }
        #endregion

    }


}
