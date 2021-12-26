using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        /// <summary>
        /// Spawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="go">GameObject instance to spawn.</param>
        /// <param name="ownerConnection">Connection to give ownership to.</param>
        public void Spawn(GameObject go, NetworkConnection ownerConnection = null)
        {
            if (go == null)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"GameObject cannot be spawned because it is null.");
                return;
            }

            NetworkObject nob = go.GetComponent<NetworkObject>();
            Objects.Spawn(nob, ownerConnection);
        }

        /// <summary>
        /// Despawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="go">GameObject instance to despawn.</param>
        public void Despawn(GameObject go)
        {
            if (go == null)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"GameObject cannot be despawned because it is null.");
                return;
            }

            NetworkObject nob = go.GetComponent<NetworkObject>();
            Despawn(nob);
        }

        /// <summary>
        /// Despawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="networkObject">NetworkObject instance to despawn.</param>
        public void Despawn(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"NetworkObject cannot be despawned because it is null.");
                return;
            }
            Objects.Despawn(networkObject, true);
        }
    }


}
