#if UNITY_2020_3_OR_NEWER
using FishNet.CodeAnalysis.Annotations;
#endif
using FishNet.Component.ColliderRollback;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Logging;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using System;
using UnityEngine;

namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        /// <summary>
        /// True if the NetworkObject for this NetworkBehaviour is deinitializing.
        /// </summary>
        public bool IsDeinitializing => _networkObjectCache.IsDeinitializing;
        [Obsolete("Use IsDeinitializing instead.")]
        public bool Deinitializing => IsDeinitializing; //Remove on 2023/01/01.
        /// <summary>
        /// NetworkManager for this object.
        /// </summary>
        public NetworkManager NetworkManager => _networkObjectCache.NetworkManager;
        /// <summary>
        /// ServerManager for this object.
        /// </summary>
        public ServerManager ServerManager => _networkObjectCache.ServerManager;
        /// <summary>
        /// ClientManager for this object.
        /// </summary>
        public ClientManager ClientManager => _networkObjectCache.ClientManager;
        /// <summary>
        /// TransportManager for this object.
        /// </summary>
        public TransportManager TransportManager => _networkObjectCache.TransportManager;
        /// <summary>
        /// TimeManager for this object.
        /// </summary>
        public TimeManager TimeManager => _networkObjectCache.TimeManager;
        /// <summary>
        /// SceneManager for this object.
        /// </summary>
        public SceneManager SceneManager => _networkObjectCache.SceneManager;
        /// <summary>
        /// RollbackManager for this object.
        /// </summary>
        public RollbackManager RollbackManager => _networkObjectCache.RollbackManager;
        /// <summary>
        /// True if the client is active and authenticated.
        /// </summary>
        public bool IsClient => _networkObjectCache.IsClient;
        /// <summary>
        /// True if only the client is active and authenticated.
        /// </summary>
        public bool IsClientOnly => _networkObjectCache.IsClientOnly;
        /// <summary>
        /// True if server is active.
        /// </summary>
        public bool IsServer => _networkObjectCache.IsServer;
        /// <summary>
        /// True if only the server is active.
        /// </summary>
        public bool IsServerOnly => _networkObjectCache.IsServerOnly;
        /// <summary>
        /// True if client and server are active.
        /// </summary>
        public bool IsHost => _networkObjectCache.IsHost;
        /// <summary>
        /// True if client nor server are active.
        /// </summary>
        public bool IsOffline => _networkObjectCache.IsOffline;
        /// <summary>
        /// True if the local client is the owner of this object.
        /// </summary>
#if UNITY_2020_3_OR_NEWER
        [PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartServer")]
        [PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Awake")]
        [PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Start")]
#endif
        public bool IsOwner => _networkObjectCache.IsOwner;
        /// <summary>
        /// Owner of this object.
        /// </summary>
        public NetworkConnection Owner
        {
            get
            {
                //Ensures a null Owner is never returned.
                if (_networkObjectCache == null)
                    return FishNet.Managing.NetworkManager.EmptyConnection;

                return _networkObjectCache.Owner;
            }
        }
        /// <summary>
        /// True if there is an owner.
        /// </summary>
        /// </summary>
        [Obsolete("Use Owner.IsValid instead.")] //Remove on 2022/06/01
        public bool OwnerIsValid => _networkObjectCache.OwnerIsValid;
        /// <summary>
        /// True if there is an owner and their connect is active. This will return false if there is no owner, or if the connection is disconnecting.
        /// </summary>
        [Obsolete("Use Owner.IsActive instead.")] //Remove on 2022/06/01
        public bool OwnerIsActive => _networkObjectCache.OwnerIsActive;
        /// <summary>
        /// ClientId for this NetworkObject owner.
        /// </summary>
        public int OwnerId => _networkObjectCache.OwnerId;
        /// <summary>
        /// Unique Id for this _networkObjectCache. This does not represent the object owner.
        /// </summary>
        public int ObjectId => _networkObjectCache.ObjectId;
        /// <summary>
        /// The local connection of the client calling this method.
        /// </summary>
        public NetworkConnection LocalConnection => _networkObjectCache.LocalConnection;
        /// <summary>
        /// Returns if a connection is the owner of this object.
        /// Internal use.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public bool CompareOwner(NetworkConnection connection)
        {
            return (_networkObjectCache.Owner == connection);
        }
        /// <summary>
        /// Despawns this _networkObjectCache. Can only be called on the server.
        /// </summary>
        public void Despawn()
        {
            if (!IsNetworkObjectNull(true))
                _networkObjectCache.Despawn();
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
            _networkObjectCache.Spawn(go, ownerConnection);
        }
        /// <summary>
        /// Returns if NetworkObject is null.
        /// </summary>
        /// <param name="warn">True to throw a warning if null.</param>
        /// <returns></returns>
        private bool IsNetworkObjectNull(bool warn)
        {
            bool isNull = (_networkObjectCache == null);
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
            _networkObjectCache.GiveOwnership(null, true);
        }
        /// <summary>
        /// Gives ownership to newOwner.
        /// </summary>
        /// <param name="newOwner"></param>
        public void GiveOwnership(NetworkConnection newOwner)
        {
            _networkObjectCache.GiveOwnership(newOwner, true);
        }
    }


}