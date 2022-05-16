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
    public sealed partial class NetworkObject : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// True if this object has been initialized on the client side.
        /// This is set true right before client callbacks.
        /// </summary>
        public bool ClientInitialized { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        private bool _isClient;
        /// <summary>
        /// True if the client is active and authenticated.
        /// </summary>
        public bool IsClient
        {
            /* This needs to use a special check when
             * player is acting as host. Clients won't
             * set IsClient until they receive the spawn message
             * but the user may expect this true after client
             * gains observation but before client gets spawn. */
            get
            {
                if (IsServer)
                    return (NetworkManager == null) ? false : NetworkManager.IsClient;
                else
                    return _isClient;
            }

            private set => _isClient = value;
        }
        
        /// <summary>
        /// True if only the client is active and authenticated.
        /// </summary>
        public bool IsClientOnly => (IsClient && !IsServer);
        /// <summary>
        /// True if server is active.
        /// </summary>
        public bool IsServer { get; private set; }
        /// <summary>
        /// True if only the server is active.
        /// </summary>
        public bool IsServerOnly => (IsServer && !IsClient);
        /// <summary>
        /// True if client and server are active.
        /// </summary>
        public bool IsHost => (IsClient && IsServer);
        /// <summary>
        /// True if client nor server are active.
        /// </summary>
        public bool IsOffline => (!IsClient && !IsServer);
        /// <summary>
        /// True if the local client is the owner of this object.
        /// </summary>
        public bool IsOwner
        {
            get
            {
                /* ClientInitialized becomes true when this
                 * NetworkObject has been initialized on the client side.
                 *
                 * This value is used to prevent IsOwner from returning true
                 * when running as host.
                 * 
                 * EG: server will set owner when it spawns the object.
                 * If IsOwner is checked before the object spawns on the
                 * client-host then it would also return true, since the
                 * Owner reference would be the same as what was set by server.
                 * This is however bad when the client hasn't initialized the object
                 * yet because it gives a false sense of initialization.
                 * 
                 * Wait for client-host being initialized before potentially
                 * returning true. */
                if (!ClientInitialized)
                    return false;

                ////perf see how this can be removed.
                ////ClientInitialized is only used for this.
                //if (!IsClient || !ClientInitialized || !Owner.IsValid || (NetworkManager == null))
                //    return false;

                return Owner.IsLocalClient;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        private NetworkConnection _owner;
        /// <summary>
        /// Owner of this object.
        /// </summary>
        public NetworkConnection Owner
        {
            get
            {
                //Ensures a null Owner is never returned.
                if (_owner == null)
                    return FishNet.Managing.NetworkManager.EmptyConnection;

                return _owner;
            }
            private set { _owner = value; }
        }
        /// <summary>
        /// True if there is an owner.
        /// </summary>
        [Obsolete("Use Owner.IsValid instead.")] //Remove on 2022/06/01
        public bool OwnerIsValid => (Owner == null) ? false : Owner.IsValid;
        /// <summary>
        /// True if there is an owner and their connect is active. This will return false if there is no owner, or if the connection is disconnecting.
        /// </summary>
        [Obsolete("Use Owner.IsValid instead.")] //Remove on 2022/06/01
        public bool OwnerIsActive => (Owner == null) ? false : Owner.IsActive;
        /// <summary>
        /// ClientId for this NetworkObject owner.
        /// </summary>
        public int OwnerId => (!Owner.IsValid) ? -1 : Owner.ClientId;
        /// <summary>
        /// True if the object is initialized for the network.
        /// </summary>
        public bool IsSpawned => (!IsDeinitializing && ObjectId >= 0);
        /// <summary>
        /// The local connection of the client calling this method.
        /// </summary>
        public NetworkConnection LocalConnection => (NetworkManager == null) ? new NetworkConnection() : NetworkManager.ClientManager.Connection;
        /// <summary>
        /// NetworkManager for this object.
        /// </summary>
        public NetworkManager NetworkManager { get; private set; }
        /// <summary>
        /// ServerManager for this object.
        /// </summary>
        public ServerManager ServerManager { get; private set; }
        /// <summary>
        /// ClientManager for this object.
        /// </summary>
        public ClientManager ClientManager { get; private set; }
        /// <summary>
        /// TransportManager for this object.
        /// </summary>
        public TransportManager TransportManager { get; private set; }
        /// <summary>
        /// TimeManager for this object.
        /// </summary>
        public TimeManager TimeManager { get; private set; }
        /// <summary>
        /// SceneManager for this object.
        /// </summary>
        public SceneManager SceneManager { get; private set; }
        /// <summary>
        /// RollbackManager for this object.
        /// </summary>
        public RollbackManager RollbackManager { get; private set; }


        #endregion

        /// <summary>
        /// Despawns this NetworkObject. Only call from the server.
        /// </summary>
        /// <param name="destroyInstantiated">True to also destroy the object if it was instantiated. False will only disable the object.</param>
        public void Despawn()
        {
            NetworkObject nob = this;
            NetworkManager.ServerManager.Despawn(nob);
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
        /// Spawns an object over the network. Only call from the server.
        /// </summary>
        public void Spawn(NetworkObject nob, NetworkConnection ownerConnection = null)
        {
            if (!CanSpawnOrDespawn(true))
                return;
            NetworkManager.ServerManager.Spawn(nob, ownerConnection);
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
                {
                    if (NetworkManager.StaticCanLog(LoggingType.Warning))
                        Debug.LogWarning($"Cannot despawn {gameObject.name}, NetworkManager reference is null. This may occur if the object is not spawned or initialized.");
                }
            }
            else if (IsDeinitializing)
            {
                canExecute = false;
                if (warn)
                {
                    if (NetworkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Cannot despawn {gameObject.name}, it is already deinitializing.");
                }
            }

            return canExecute;
        }

    }

}

