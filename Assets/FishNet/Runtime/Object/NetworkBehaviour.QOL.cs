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
        /// NetworkManager for this object. The NetworkManager is a link to all things related to the network.
        /// </summary>
        public NetworkManager NetworkManager => (NetworkObject == null) ? null : NetworkObject.NetworkManager;
        /// <summary>
        /// True if acting as a client.
        /// </summary>
        public bool IsClient => (NetworkObject == null) ? false : NetworkObject.IsClient;
        /// <summary>
        /// True if client only.
        /// </summary>
        public bool IsClientOnly => (NetworkObject == null) ? false : (!NetworkObject.IsServer && NetworkObject.IsClient);
        /// <summary>
        /// True if acting as the server.
        /// </summary>
        public bool IsServer => (NetworkObject == null) ? false : NetworkObject.IsServer;
        /// <summary>
        /// True if server only.
        /// </summary>
        public bool IsServerOnly => (NetworkObject == null) ? false : (NetworkObject.IsServer && !NetworkObject.IsClient);
        /// <summary>
        /// True if acting as a client and the server.
        /// </summary>
        public bool IsHost => (NetworkObject == null) ? false : (NetworkObject.IsServer && NetworkObject.IsClient);
        /// <summary>
        /// True if the owner of this object. Only contains value on clients.
        /// </summary>
        public bool IsOwner => (NetworkObject == null) ? false : NetworkObject.IsOwner;
        /// <summary>
        /// Owner of this object. Will be null if there is no owner. Owner is only visible to all players.
        /// </summary>
        public NetworkConnection Owner => (NetworkObject == null) ? null : NetworkObject.Owner;
        /// <summary>
        /// True if the owner is a valid connection.
        /// </summary>
        public bool OwnerIsValid => (NetworkObject == null) ? false : NetworkObject.OwnerIsValid;
        /// <summary>
        /// ClientId for this NetworkObject owner. Only visible to server.
        /// </summary>
        public int OwnerId => (NetworkObject == null) ? -1 : NetworkObject.OwnerId;
        /// <summary>
        /// Returns the local connection for the client calling this method.
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
        /// Despawns this NetworkObject. If server despawn will also occur on clients.
        /// </summary>
        public void Despawn()
        {
            if (!IsNetworkObjectNull(true))
                NetworkObject.Despawn();                
        }
        /// <summary>
        /// Spawns an object over the network.
        /// </summary>
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
    }


}