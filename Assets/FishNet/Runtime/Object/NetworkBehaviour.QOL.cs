#if UNITY_2020_3_OR_NEWER && UNITY_EDITOR_WIN
using FishNet.CodeAnalysis.Annotations;
#endif
using FishNet.Component.ColliderRollback;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Logging;
using FishNet.Managing.Observing;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using System;
using System.Collections.Generic;
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
        /// ObserverManager for this object.
        /// </summary>
        public ObserverManager ObserverManager => _networkObjectCache.ObserverManager;
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
        /// Observers for this NetworkBehaviour.
        /// </summary>
        public HashSet<NetworkConnection> Observers => _networkObjectCache.Observers;
        /// <summary>
        /// True if the local client is the owner of this object.
        /// </summary>
#if UNITY_2020_3_OR_NEWER && UNITY_EDITOR_WIN
        [PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartServer", "")]
        [PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartNetwork", " Use base.Owner.IsLocalClient instead.")]
        [PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Awake", "")]
        [PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Start", "")]
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
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public bool OwnerMatches(NetworkConnection connection)
        {
            return (_networkObjectCache.Owner == connection);
        }
        /// <summary>
        /// Despawns this _networkObjectCache. Can only be called on the server.
        /// </summary>
        /// <param name="cacheOnDespawnOverride">Overrides the default DisableOnDespawn value for this single despawn. Scene objects will never be destroyed.</param>
        public void Despawn(DespawnType? despawnType = null)
        {
            if (!IsNetworkObjectNull(true))
                _networkObjectCache.Despawn(despawnType);
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
        /// Spawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="nob">GameObject instance to spawn.</param>
        /// <param name="ownerConnection">Connection to give ownership to.</param>
        public void Spawn(NetworkObject nob, NetworkConnection ownerConnection = null)
        {
            if (IsNetworkObjectNull(true))
                return;
            _networkObjectCache.Spawn(nob, ownerConnection);
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