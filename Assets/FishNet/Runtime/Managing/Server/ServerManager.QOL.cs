using FishNet.Connection;
using UnityEngine;
using FishNet.Object;

namespace FishNet.Managing.Server
{
    public partial class ServerManager : MonoBehaviour
    {
        /// <summary>
        /// Spawns an object over the network. Only call from the server.
        /// </summary>
        /// <param name="go"></param>
        public void Spawn(GameObject go, NetworkConnection ownerConnection = null)
        {
            if (go == null)
            {
                Debug.LogError($"GameObject cannot be spawned because it is null.");
                return;
            }

            NetworkObject nob = go.GetComponent<NetworkObject>();
            Objects.Spawn(nob, ownerConnection);
        }

        /// <summary>
        /// Despawns an object over the network. Only call from the server.
        /// </summary>
        /// <param name="go"></param>
        public void Despawn(GameObject go)
        {
            if (go == null)
            {
                Debug.LogError($"GameObject cannot be despawned because it is null.");
                return;
            }

            NetworkObject nob = go.GetComponent<NetworkObject>();
            Despawn(nob);
        }

        /// <summary>
        /// Despawns an object over the network. Only call from the server.
        /// </summary>
        /// <param name="networkObject"></param>
        public void Despawn(NetworkObject networkObject)
        {
            Objects.Despawn(networkObject, true);
        }
    }


}
