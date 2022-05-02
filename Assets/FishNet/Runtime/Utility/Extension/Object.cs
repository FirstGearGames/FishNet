using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Utility.Extension
{

    public static class ObjectFN
    {
        /// <summary>
        /// Spawns an object over the network using InstanceFinder. Only call from the server.
        /// </summary>
        public static void Spawn(this NetworkObject nob, NetworkConnection owner = null)
        {
            InstanceFinder.ServerManager.Spawn(nob, owner);
        }
        /// <summary>
        /// Spawns an object over the network using InstanceFinder. Only call from the server.
        /// </summary>
        public static void Spawn(this GameObject go, NetworkConnection owner = null)
        {
            InstanceFinder.ServerManager.Spawn(go, owner);
        }


    }

}