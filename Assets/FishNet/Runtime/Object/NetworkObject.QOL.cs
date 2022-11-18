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
                 * when running as host; primarily in Update or Tick callbacks
                 * where IsOwner would be true as host but OnStartClient has
                 * not called yet.
                 * 
                 * EG: server will set owner when it spawns the object.
                 * If IsOwner is checked before the object spawns on the
                 * client-host then it would also return true, since the
                 * Owner reference would be the same as what was set by server.
                 *
                 * This is however bad when the client hasn't initialized the object
                 * yet because it gives a false sense of execution order. 
                 * As a result, Update or Ticks may return IsOwner as true well before OnStartClient
                 * is called. Many users rightfully create code with the assumption the client has been
                 * initialized by the time IsOwner is true.
                 * 
                 * This is a double edged sword though because now IsOwner would return true
                 * within OnStartNetwork for clients only, but not for host given the client
                 * side won't be initialized yet as host. As a work around CodeAnalysis will
                 * inform users to instead use base.Owner.IsLocalClient within OnStartNetwork. */
                if (!ClientInitialized)
                    return false;

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
        /// ObserverManager for this object.
        /// </summary>
        public ObserverManager ObserverManager { get; private set; }
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
        /// Returns a NetworkBehaviour on this NetworkObject.
        /// </summary>
        /// <param name="componentIndex">ComponentIndex of the NetworkBehaviour.</param>
        /// <param name="error">True to error if not found.</param>
        /// <returns></returns>
        public NetworkBehaviour GetNetworkBehaviour(byte componentIndex, bool error)
        {
            if (componentIndex >= NetworkBehaviours.Length)
            {
                if (error)
                {
                    bool staticLog = (NetworkManager == null);
                    string errMsg = $"ComponentIndex of {componentIndex} is out of bounds on {gameObject.name} [id {ObjectId}]. This may occur if you have modified your gameObject/prefab without saving it, or the scene.";

                    if (staticLog && NetworkManager.StaticCanLog(LoggingType.Error))
                        Debug.LogError(errMsg);
                    else if (!staticLog && NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError(errMsg);
                }
            }

            return NetworkBehaviours[componentIndex];
        }

        /// <summary>
        /// Despawns a GameObject. Only call from the server.
        /// </summary>
        /// <param name="go">GameObject to despawn.</param>
        /// <param name="despawnType">What happens to the object after being despawned.</param>
        public void Despawn(GameObject go, DespawnType? despawnType = null)
        {
            NetworkManager.ServerManager.Despawn(go, despawnType);
        }
        /// <summary>
        /// Despawns  a NetworkObject. Only call from the server.
        /// </summary>
        /// <param name="nob">NetworkObject to despawn.</param>
        /// <param name="despawnType">What happens to the object after being despawned.</param>
        public void Despawn(NetworkObject nob, DespawnType? despawnType = null)
        {
            NetworkManager.ServerManager.Despawn(nob, despawnType);
        }
        /// <summary>
        /// Despawns this NetworkObject. Only call from the server.
        /// </summary>
        /// <param name="despawnType">What happens to the object after being despawned.</param>
        public void Despawn(DespawnType? despawnType = null)
        {
            NetworkObject nob = this;
            NetworkManager.ServerManager.Despawn(nob, despawnType);
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

        /// <summary>
        /// Takes ownership of this object and child network objects, allowing immediate control.
        /// </summary>
        /// <param name="caller">Connection to give ownership to.</param>
        public void SetLocalOwnership(NetworkConnection caller)
        {
            NetworkConnection prevOwner = Owner;
            SetOwner(caller);

            int count;
            count = NetworkBehaviours.Length;
            for (int i = 0; i < count; i++)
                NetworkBehaviours[i].OnOwnershipClient(prevOwner);
            count = ChildNetworkObjects.Count;
            for (int i = 0; i < count; i++)
                ChildNetworkObjects[i].SetLocalOwnership(caller);
        }

        #region Registered components
        /// <summary>
        /// Invokes an action when a specified component becomes registered. Action will invoke immediately if already registered.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <param name="handler">Action to invoke.</param>
        public void RegisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component => NetworkManager.RegisterInvokeOnInstance<T>(handler);
        /// <summary>
        /// Removes an action to be invoked when a specified component becomes registered.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <param name="handler">Action to invoke.</param>
        public void UnregisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component => NetworkManager.UnregisterInvokeOnInstance<T>(handler);
        /// <summary>
        /// Returns class of type if found within CodegenBase classes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetInstance<T>() where T : UnityEngine.Component => NetworkManager.GetInstance<T>();
        /// <summary>
        /// Registers a new component to this NetworkManager.
        /// </summary>
        /// <typeparam name="T">Type to register.</typeparam>
        /// <param name="component">Reference of the component being registered.</param>
        /// <param name="replace">True to replace existing references.</param>
        public void RegisterInstance<T>(T component, bool replace = true) where T : UnityEngine.Component => NetworkManager.RegisterInstance<T>(component, replace);
        /// <summary>
        /// Unregisters a component from this NetworkManager.
        /// </summary>
        /// <typeparam name="T">Type to unregister.</typeparam>
        public void UnregisterInstance<T>() where T : UnityEngine.Component => NetworkManager.UnregisterInstance<T>();
        #endregion

    }

}

