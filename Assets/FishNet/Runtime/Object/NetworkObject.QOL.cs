using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// True if the client is running and authenticated.
        /// </summary>
        public bool IsClient => NetworkManager.IsClient;
        /// <summary>
        /// True if client only.
        /// </summary>
        public bool IsClientOnly => (!IsServer && IsClient);
        /// <summary>
        /// True if the server is running.
        /// </summary>
        public bool IsServer => NetworkManager.IsServer;
        /// <summary>
        /// True if server only.
        /// </summary>
        public bool IsServerOnly => (IsServer && !IsClient);
        /// <summary>
        /// True if client and server are active.
        /// </summary>
        public bool IsHost => (IsServer && IsClient);
        /// <summary>
        /// True if the owner of this object. Only contains value on clients.
        /// </summary>
        public bool IsOwner => (NetworkManager == null || !OwnerIsValid || !IsClient) ? false : (NetworkManager.ClientManager.Connection == Owner);
        /// <summary> 
        /// True if the owner is a valid connection.
        /// </summary>
        public bool OwnerIsValid => (Owner == null) ? false : (Owner.IsValid);
        /// <summary>
        /// ClientId for this NetworkObject owner. Only visible to server.
        /// </summary>
        public int OwnerId => (!OwnerIsValid) ? -1 : Owner.ClientId;
        /// <summary>
        /// Returns if this object is spawned.
        /// </summary>
        public bool IsSpawned => (!Deinitializing && ObjectId >= 0);
        /// <summary>
        /// Returns the local connection for the client calling this method.
        /// </summary>
        public NetworkConnection LocalConnection => (NetworkManager == null) ? new NetworkConnection() : NetworkManager.ClientManager.Connection;
        #endregion

        /// <summary>
        /// Despawns this NetworkObject. Only call from the server.
        /// </summary>
        public void Despawn()
        {
            NetworkManager.ServerManager.Despawn(this);
        }
        /// <summary>
        /// Spawns an object over the network. Only call from the server.
        /// </summary>
        public void Spawn(GameObject go, NetworkConnection ownerConnection = null)
        {
            if (!CanSpawnOrDespawn(true))
                return;
            NetworkManager.ServerManager.Spawn(go, ownerConnection);
        }

        /// <summary>
        /// Returns if Spawn or Despawn can be called.
        /// </summary>
        /// <param name="warn">True to warn if not able to execute spawn or despawn.</param>
        /// <returns></returns>
        internal bool CanSpawnOrDespawn(bool warn)
        {
            bool canExecute = true;

            if (NetworkManager == null)
            {
                canExecute = false;
                if (warn)
                    Debug.LogWarning($"Cannot despawn {gameObject.name}, NetworkManager reference is null. This may occur if the object is not spawned or initialized.");
            }
            else if (!IsServer)
            {
                canExecute = false;
                if (warn)
                    Debug.LogWarning($"Cannot spawn or despawn {gameObject.name}, server is not active.");
            }
            else if (Deinitializing)
            {
                canExecute = false;
                if (warn)
                    Debug.LogWarning($"Cannot despawn {gameObject.name}, it is already deinitializing.");
            }

            return canExecute;
        }

        /// <summary>
        /// Sends a Broadcast to observers for this NetworkObject.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="networkObject"></param>
        /// <param name="message"></param>
        /// <param name="requireAuthenticated">True if the broadcast can only go to an authenticated connection.</param>
        /// <param name="channel"></param>
        public void Broadcast<T>(T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (NetworkManager == null)
            {
                Debug.LogWarning($"Cannot send broadcast from {gameObject.name}, NetworkManager reference is null. This may occur if the object is not spawned or initialized.");
                return;
            }

            NetworkManager.ServerManager.Broadcast(Observers, message, requireAuthenticated, channel);
        }
    }

}

