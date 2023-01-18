using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Managing.Object
{

    internal class DespawnWriter
    {
        #region Private.
        /// <summary>
        /// NetworkManager associated with this.
        /// </summary>
        private NetworkManager _networkManager;
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        public void Initialize(NetworkManager manager)
        {
            _networkManager = manager;
        }
    }

}