using FishNet.Managing;
using FishNet.Connection;
using UnityEngine;
using FishNet.Managing.Logging;

namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        /// <summary>
        /// True if the NetworkObject for this NetworkBehaviour is deinitializing.
        /// </summary>
        public bool Deinitializing => (NetworkObject == null) ? true : NetworkObject.Deinitializing;
        /// <summary>
        /// NetworkManager for this object.
        /// </summary>
        public NetworkManager NetworkManager => (NetworkObject == null) ? null : NetworkObject.NetworkManager;
        /// <summary>
        /// True if the client is active and authenticated.
        /// </summary>
        public bool IsClient => (NetworkObject == null) ? false : NetworkObject.IsClient;
        /// <summary>
        /// True if only the client is active, and authenticated.
        /// </summary>
        public bool IsClientOnly => (NetworkObject == null) ? false : (!NetworkObject.IsServer && NetworkObject.IsClient);
        /// <summary>
        /// True if server is active.
        /// </summary>
        public bool IsServer => (NetworkObject == null) ? false : NetworkObject.IsServer;
        /// <summary>
        /// True if only the server is active.
        /// </summary>
        public bool IsServerOnly => (NetworkObject == null) ? false : (NetworkObject.IsServer && !NetworkObject.IsClient);
        /// <summary>
        /// True if client and server are active.
        /// </summary>
        public bool IsHost => (NetworkObject == null) ? false : (NetworkObject.IsServer && NetworkObject.IsClient);
        /// <summary>
        /// True if the local client is the owner of this object.
        /// </summary>
        public bool IsOwner => (NetworkObject == null) ? false : NetworkObject.IsOwner;
        /// <summary>
        /// Owner of this object.
        /// </summary>
        public NetworkConnection Owner => (NetworkObject == null) ? null : NetworkObject.Owner;
        /// <summary>
        /// True if there is an owner.
        /// </summary>
        public bool OwnerIsValid => (NetworkObject == null) ? false : NetworkObject.OwnerIsValid;
        /// <summary>
        /// ClientId for this NetworkObject owner.
        /// </summary>
        public int OwnerId => (NetworkObject == null) ? -1 : NetworkObject.OwnerId;
        /// <summary>
        /// The local connection of the client calling this method.
        /// </summary>
        public NetworkConnection LocalConnection => (NetworkObject == null) ? new NetworkConnection() : NetworkObject.LocalConnection;
        /// <summary>
        /// Returns if a connection is the owner of this object.
        /// Internal use.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public bool CompareOwner(NetworkConnection connection)
        {
            return (NetworkObject.Owner == connection);
        }
        /// <summary>
        /// Despawns this NetworkObject. Can only be called on the server.
        /// </summary>
        public void Despawn()
        {
            if (!IsNetworkObjectNull(true))
                NetworkObject.Despawn();
        }
        /// <summary>
        /// Spawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="go">GameObject instance to spawn.</param>
        /// <param name="ownerConnection">Connection to give ownership to.</param>
        public void Spawn(GameObject go, NetworkConnection ownerConnection = null)
        {
            if (IsNetworkObjectNull(true))
                return;
            NetworkObject.Spawn(go, ownerConnection);
        }
        /// <summary>
        /// Returns if NetworkObject is null.
        /// </summary>
        /// <param name="warn">True to throw a warning if null.</param>
        /// <returns></returns>
        private bool IsNetworkObjectNull(bool warn)
        {
            bool isNull = (NetworkObject == null);
            if (isNull && warn)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"NetworkObject is null. This can occur if this object is not spawned, or initialized yet.");
            }

            return isNull;
        }
        /// <summary>
        /// Removes ownership from all clients.
        /// </summary>
        public void RemoveOwnership()
        {
            NetworkObject?.GiveOwnership(null, true);
        }
        /// <summary>
        /// Gives ownership to newOwner.
        /// </summary>
        /// <param name="newOwner"></param>
        public void GiveOwnership(NetworkConnection newOwner)
        {
            NetworkObject?.GiveOwnership(newOwner, true);
        }
    }


}