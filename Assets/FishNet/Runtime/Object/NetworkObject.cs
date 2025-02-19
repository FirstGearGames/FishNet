﻿using System;
using FishNet.Managing;
using FishNet.Connection;
using UnityEngine;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FishNet.Utility.Performance;
using FishNet.Component.Ownership;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishNet.Object
{
    public class NetworkObjectIdComparer : IEqualityComparer<NetworkObject>
    {
        public bool Equals(NetworkObject valueA, NetworkObject valueB)
        {
            bool aNull = (valueA is null);
            bool bNull = (valueB is null);
            //One null, one isn't.
            if (aNull != bNull)
                return false;
            //Both null.
            if (aNull && bNull)
                return true;

            //If here neither are null.
            return (valueA.ObjectId == valueB.ObjectId);
        }

        public int GetHashCode(NetworkObject obj)
        {
            return obj.ObjectId;
        }
    }

    [DefaultExecutionOrder(short.MinValue + 1)]
    [DisallowMultipleComponent]
    public partial class NetworkObject : MonoBehaviour, IOrderable, IExposedAssetGuid
    {
        #region Public.
        /// <summary>
        /// True if this object is nested.
        /// This value is automatically applied for prefabs and scene objects during serialization. However, if changing parents at runtime use NetworkObject.SetParent().
        /// </summary>
        [field: SerializeField, HideInInspector]
        public bool IsNested { get; private set; }

        /// <summary>
        /// NetworkConnection which predicted spawned this object.
        /// </summary>
        public NetworkConnection PredictedSpawner { get; private set; } = NetworkManager.EmptyConnection;
        /// <summary>
        /// True if this NetworkObject was active during edit. Will be true if placed in scene during edit, and was in active state on run.
        /// </summary>
        [System.NonSerialized]
        internal bool ActiveDuringEdit;

        /// <summary>
        /// Returns if this object was placed in the scene during edit-time.
        /// </summary>
        /// <returns></returns>
        public bool IsSceneObject => (SceneId != NetworkObject.UNSET_SCENEID_VALUE);

        /// <summary>
        /// ComponentIndex for this NetworkBehaviour.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public byte ComponentIndex { get; private set; }

        /// <summary>
        /// Unique Id for this NetworkObject. This does not represent the object owner.
        /// </summary>
        public int ObjectId { get; private set; } = NetworkObject.UNSET_OBJECTID_VALUE;

        /// <summary>
        /// True if this NetworkObject is deinitializing. Will also be true until Initialize is called. May be false until the object is cleaned up if object is destroyed without using Despawn.
        /// </summary>
        internal bool IsDeinitializing { get; private set; } = true;

        /// <summary>
        /// PredictedSpawn component on this object. Will be null if not added manually.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public PredictedSpawn PredictedSpawn { get; private set; }

        /// <summary>
        /// PredictedOwner component on this object. Will be null if not added manually.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public PredictedOwner PredictedOwner { get; private set; }

        /// <summary>
        /// Current Networkbehaviours.
        /// </summary>
        [HideInInspector]
        public List<NetworkBehaviour> NetworkBehaviours;
        /// <summary>
        /// NetworkBehaviour on the root of a NetworkObject parenting this instance. Value will be null if there was no parent during serialization.
        /// </summary>
        /// <remarks>This API is for internal use and may change at any time.</remarks>
        [HideInInspector]
        public NetworkBehaviour InitializedParentNetworkBehaviour;

        /// <summary>
        /// Nested NetworkObjects that existed during initialization.
        /// </summary>
        /// <remarks>This API is for internal use and may change at any time.</remarks>
        [HideInInspector]
        public List<NetworkObject> InitializedNestedNetworkObjects = new();

        /// <summary>
        /// Returns a cached collection containing initialized and runtime nested NetworkObjects.
        /// </summary>
        /// <param name="recursive">True to also return nested NetworkObjects beyond direct children of this object.</param>
        internal List<NetworkObject> RetrieveNestedNetworkObjects(bool recursive)
        {
            List<NetworkObject> cache = CollectionCaches<NetworkObject>.RetrieveList();
            cache.AddRange(InitializedNestedNetworkObjects);
            foreach (NetworkBehaviour nb in RuntimeChildNetworkBehaviours)
                cache.Add(nb.NetworkObject);

            if (recursive)
            {
                int count = cache.Count;
                for (int i = 0; i < count; i++)
                {
                    List<NetworkObject> recursiveCache = cache[i].RetrieveNestedNetworkObjects(recursive: true);
                    cache.AddRange(recursiveCache);
                    CollectionCaches<NetworkObject>.Store(recursiveCache);
                }
            }

            return cache;
        }

        /// <summary>
        /// NetworkBehaviour parenting this object when set at runtime using NetworkObject/NetworkBehaviour.SetParent.
        /// This is exposed only for low-level use and may change without notice.
        /// </summary>
        [HideInInspector]
        public NetworkBehaviour RuntimeParentNetworkBehaviour;
        /// <summary>
        /// NetworkObjects which are made child at runtime using NetworkObject.SetParent.
        /// This is exposed only for low-level use and may change without notice.
        /// </summary>
        [HideInInspector]
        public List<NetworkBehaviour> RuntimeChildNetworkBehaviours;
        /// <summary>
        /// NetworkBehaviour parenting this instance. This value prioritizes the runtime value, then initialized value.
        /// This is exposed only for low-level use and may change without notice.
        /// </summary>
        internal NetworkBehaviour CurrentParentNetworkBehaviour
        {
            get
            {
                if (RuntimeParentNetworkBehaviour != null)
                    return RuntimeParentNetworkBehaviour;
                if (InitializedParentNetworkBehaviour != null)
                    return InitializedParentNetworkBehaviour;

                return null;
            }
        }

        /// <summary>
        /// Current state of the NetworkObject.
        /// </summary>
        [System.NonSerialized]
        internal NetworkObjectState State = NetworkObjectState.Unset;
        #endregion

        #region Serialized.
        /// <summary>
        /// True if the object will always initialize as a networked object. When false the object will not automatically initialize over the network. Using Spawn() on an object will always set that instance as networked.
        /// To check if server or client has been initialized on this object use IsXYZInitialized.
        /// </summary>
        [Obsolete("Use Get/SetIsNetworked.")]
        public bool IsNetworked
        {
            get => GetIsNetworked();
            private set => SetIsNetworked(value);
        }

        /// <summary>
        /// Returns IsNetworked value.
        /// </summary>
        /// <returns></returns>
        public bool GetIsNetworked() => _isNetworked;

        /// <summary>
        /// Sets IsNetworked value. This method must be called before Start.
        /// </summary>
        /// <param name="value">New IsNetworked value.</param>
        public void SetIsNetworked(bool value)
        {
            _isNetworked = value;
        }

        [Tooltip("True if the object will always initialize as a networked object. When false the object will not automatically initialize over the network. Using Spawn() on an object will always set that instance as networked.")]
        [SerializeField]
        private bool _isNetworked = true;

        /// <summary>
        /// True if the object can be spawned at runtime; this is generally false for scene prefabs you do not spawn.
        /// </summary>
        [Obsolete("Use GetIsSpawnable.")] //Remove on V5.
        public bool IsSpawnable => _isSpawnable;

        /// <summary>
        /// Gets the current IsSpawnable value.
        /// </summary>
        /// <returns></returns>
        public bool GetIsSpawnable() => _isSpawnable;

        /// <summary>
        /// Sets IsSpawnable value.
        /// </summary>
        /// <param name="value">Next value.</param>
        public void SetIsSpawnable(bool value) => _isSpawnable = value;

        [Tooltip("True if the object can be spawned at runtime; this is generally false for scene prefabs you do not spawn.")]
        [SerializeField]
        private bool _isSpawnable = true;

        /// <summary>
        /// True to make this object global, and added to the DontDestroyOnLoad scene. This value may only be set for instantiated objects, and can be changed if done immediately after instantiating.
        /// </summary>
        public bool IsGlobal
        {
            get => _isGlobal;
            private set => _isGlobal = value;
        }

        /// <summary>
        /// Sets IsGlobal value.
        /// </summary>
        /// <param name="value">New global value.</param>
        public void SetIsGlobal(bool value)
        {
            if (IsNested && !CurrentParentNetworkBehaviour.NetworkObject.IsGlobal)
            {
                NetworkManager.LogWarning($"Object {gameObject.name} cannot change IsGlobal because it is nested and the parent NetorkObject is not global.");
                return;
            }

            if (!IsDeinitializing)
            {
                NetworkManager.LogWarning($"Object {gameObject.name} cannot change IsGlobal as it's already initialized. IsGlobal may only be changed immediately after instantiating.");
                return;
            }

            if (IsSceneObject)
            {
                NetworkManager.LogWarning($"Object {gameObject.name} cannot have be global because it is a scene object. Only instantiated objects may be global.");
                return;
            }

            _networkObserverInitiliazed = false;
            IsGlobal = value;
        }

        [Tooltip("True to make this object global, and added to the DontDestroyOnLoad scene. This value may only be set for instantiated objects, and can be changed if done immediately after instantiating.")]
        [SerializeField]
        private bool _isGlobal;

        /// <summary>
        /// Order to initialize this object's callbacks when spawned with other NetworkObjects in the same tick. Default value is 0, negative values will execute callbacks first.
        /// </summary>
        public sbyte GetInitializeOrder() => _initializeOrder;

        /// <summary>
        /// This is for internal use. Returns the order to initialize the object.
        /// </summary>
        public int Order => _initializeOrder;

        [Tooltip("Order to initialize this object's callbacks when spawned with other NetworkObjects in the same tick. Default value is 0, negative values will execute callbacks first.")]
        [Range(sbyte.MinValue, sbyte.MaxValue)]
        [SerializeField]
        private sbyte _initializeOrder = 0;
        /// <summary>
        /// True to keep this object spawned when the owner disconnects.
        /// </summary>
        internal bool PreventDespawnOnDisconnect => _preventDespawnOnDisconnect;
        [Tooltip("True to keep this object spawned when the owner disconnects.")]
        [SerializeField]
        private bool _preventDespawnOnDisconnect;
        /// <summary>
        /// How to handle this object when it despawns. Scene objects are never destroyed when despawning.
        /// </summary>
        [SerializeField]
        [Tooltip("How to handle this object when it despawns. Scene objects are never destroyed when despawning.")]
        private DespawnType _defaultDespawnType = DespawnType.Destroy;

        /// <summary>
        /// True to use configured ObjectPool rather than destroy this NetworkObject when being despawned. Scene objects are never destroyed.
        /// </summary>
        public DespawnType GetDefaultDespawnType() => _defaultDespawnType;

        /// <summary>
        /// Sets DespawnType value.
        /// </summary>
        /// <param name="despawnType">Default despawn type for this NetworkObject.</param>
        public void SetDefaultDespawnType(DespawnType despawnType)
        {
            _defaultDespawnType = despawnType;
        }
        #endregion

        #region Private.
        /// <summary>
        /// True if disabled NetworkBehaviours have been initialized.
        /// </summary>
        private bool _disabledNetworkBehavioursInitialized;
        /// <summary>
        /// Becomes true once initialized values are set.
        /// </summary>
        [SerializeField, HideInInspector]
        private bool _initializedValusSet;
        #endregion

        #region Const.
        /// <summary>
        /// Value used when the ObjectId has not been set.
        /// </summary>
        public const int UNSET_SCENEID_VALUE = 0;
        /// <summary>
        /// Value used when the ObjectId has not been set.
        /// </summary>
        public const int UNSET_OBJECTID_VALUE = ushort.MaxValue;
        /// <summary>
        /// Value used when the PrefabId has not been set.
        /// </summary>
        public const int UNSET_PREFABID_VALUE = ushort.MaxValue;
        #endregion

        #region Editor Debug.
#if UNITY_EDITOR
        private int _editorOwnerId;
#endif
        #endregion

        /// <summary>
        /// Outputs data about this NetworkObject to string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Name [{gameObject.name}] Id [{ObjectId}]";
        }

        protected virtual void Awake()
        {
            _isStatic = gameObject.isStatic;

            /* If networkBehaviours are not yet initialized then do so now.
             * After initializing at least 1 networkBehaviour will always exist
             * as emptyNetworkBehaviour is added automatically when none are present. */
            if (!_initializedValusSet)
            {
                bool isNested = false;
                //Make sure there are no networkObjects above this since initializing will trickle down.
                Transform parent = transform.parent;
                while (parent != null)
                {
                    if (parent.TryGetComponent<NetworkObject>(out _))
                    {
                        isNested = true;
                        break;
                    }

                    parent = parent.parent;
                }

                //If not nested then init
                if (!isNested)
                    SetInitializedValues(parentNob: null, force: false);
            }

            SetChildDespawnedState();
        }

        protected virtual void Start()
        {
            TryStartDeactivation();
        }

        private void OnDisable()
        {
            /* If deinitializing and an owner exist
             * then remove object from owner. */
            if (IsDeinitializing && Owner.IsValid)
                Owner.RemoveObject(this);
            /* If not nested then check to despawn this OnDisable.
             * A nob may become disabled without being despawned if it's
             * beneath another deinitializing nob. This can be true even while
             * not nested because users may move a nob under another at runtime.
             *
             * This object must also be activeSelf, meaning that it became disabled
             * because a parent was. If not activeSelf then it's possible the
             * user simply deactivated the object themselves. */
            else if (IsServerStarted && !IsNested && gameObject.activeSelf)
            {
                bool canDespawn = false;
                Transform nextParent = transform.parent;
                while (nextParent != null)
                {
                    if (nextParent.TryGetComponent(out NetworkObject pNob))
                    {
                        /* If pNob is not the same as ParentNetworkObject
                         * then that means this object was moved around. It could be
                         * that this was previously a child of something else
                         * or that was given a parent later on in it's life cycle.
                         ^
                         * When this occurs do not send a despawn for this object.
                         * Rather, let it destroy from unity callbacks which will force
                        * the proper destroy/stop cycle. */
                        if (pNob != InitializedParentNetworkBehaviour)
                            break;
                        //If nob is deinitialized then this one cannot exist.
                        if (pNob.IsDeinitializing)
                        {
                            canDespawn = true;
                            break;
                        }
                    }

                    nextParent = nextParent.parent;
                }

                if (canDespawn)
                    Despawn();
            }
        }

        private void OnDestroy()
        {
            //The object never initialized for use.
            if (!_initializedValusSet)
                return;

            /* There is however chance the object can get destroyed before deinitializing
             * as clientHost. If not clientHost its safe to skip deinitializing again.
             * But if clientHost, check if the client has deinitialized. If not then do
             * so now for the client side. */
            bool exitMethod = false;
            
            /* If already deinitializing then FishNet is in the process of,
             * or has finished cleaning up this object. */
            //callStopNetwork = (ServerManager.Objects.GetFromPending(ObjectId) == null);
            if (IsDeinitializing)
            {

                if (IsHostStarted)
                {
                    if (!_onStartClientCalled)
                        exitMethod = true;
                }
                else
                {
                    exitMethod = true;
                }
            }

            if (Owner.IsValid)
                Owner.RemoveObject(this);
            if (NetworkObserver != null)
                NetworkObserver.Deinitialize(destroyed: true);
            
            if (exitMethod)
            {
                NetworkBehaviour_OnDestroy();
                return;
            }

            if (NetworkManager != null)
            {
                bool isServerStarted = IsServerStarted;
                bool isClientStarted = IsClientStarted;
                //Was destroyed without going through the proper methods.
                if (isServerStarted)
                {
                    Deinitialize_Prediction(true);
                    NetworkManager.ServerManager.Objects.NetworkObjectUnexpectedlyDestroyed(this, true);

                    InvokeStopCallbacks(asServer: true, invokeSyncTypeCallbacks: true);
                }

                if (isClientStarted)
                {
                    Deinitialize_Prediction(false);
                    NetworkManager.ClientManager.Objects.NetworkObjectUnexpectedlyDestroyed(this, false);

                    InvokeStopCallbacks(false, true);
                }
            }

            /* If owner exist then remove object from owner.
             * This has to be called here as well OnDisable because
             * the OnDisable will only remove the object if
             * deinitializing. This is because the object shouldn't
             * be removed from owner if the object is simply being
             * disabled, but not deinitialized. But in the scenario
             * the object is unexpectedly destroyed, which is how we
             * arrive here, the object needs to be removed from owner. */
            if (Owner.IsValid)
                Owner.RemoveObject(this);

            Observers.Clear();
            if (NetworkBehaviours.Count > 0)
            {
                NetworkBehaviour thisNb = NetworkBehaviours[0];
                /* A null check must also be run on the RuntimeChildNbs because the collection is stored
                 * when an object is destroyed, and if the other object OnDestroy runs before this one deinitializes/destroys
                 * then the collection will be null. */
                if (RuntimeParentNetworkBehaviour != null && RuntimeParentNetworkBehaviour.NetworkObject.RuntimeChildNetworkBehaviours != null)
                    RuntimeParentNetworkBehaviour.NetworkObject.RuntimeChildNetworkBehaviours.Remove(thisNb);
            }

            IsDeinitializing = true;

            SetDeinitializedStatus();

            NetworkBehaviour_OnDestroy();

            StoreCollections();

            void NetworkBehaviour_OnDestroy()
            {
                foreach (NetworkBehaviour nb in NetworkBehaviours)
                    nb.NetworkBehaviour_OnDestroy();
            }
        }

        /// <summary>
        /// Initializes NetworkBehaviours if they are disabled.
        /// </summary>
        private void InitializeNetworkBehavioursIfDisabled()
        {
            if (_disabledNetworkBehavioursInitialized)
                return;
            _disabledNetworkBehavioursInitialized = true;

            for (int i = 0; i < NetworkBehaviours.Count; i++)
                NetworkBehaviours[i].InitializeIfDisabled();
        }

        /// <summary>
        /// Makes children of this NetworkObject global if this object is global.
        /// </summary>
        private void SetChildGlobalState()
        {
            if (!IsGlobal)
                return;

            for (int i = 0; i < InitializedNestedNetworkObjects.Count; i++)
                InitializedNestedNetworkObjects[i].SetIsGlobal(true);
        }

        /// <summary>
        /// Sets Despawned on child NetworkObjects if they are not enabled.
        /// </summary>
        private void SetChildDespawnedState()
        {
            NetworkObject nob;
            for (int i = 0; i < InitializedNestedNetworkObjects.Count; i++)
            {
                nob = InitializedNestedNetworkObjects[i];
                if (!nob.gameObject.activeSelf)
                    nob.State = NetworkObjectState.Despawned;
            }
        }

        internal void TryStartDeactivation()
        {
            if (!GetIsNetworked())
                return;

            //Global.
            if (IsGlobal && !IsSceneObject && !IsNested)
                DontDestroyOnLoad(gameObject);

            if (NetworkManager == null || (!NetworkManager.IsClientStarted && !NetworkManager.IsServerStarted))
            {
                //ActiveDuringEdit is only used for scene objects.
                if (IsSceneObject)
                    ActiveDuringEdit = true;
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Sets IsClient or IsServer to isActive.
        /// </summary>
        internal void SetInitializedStatus(bool isInitialized, bool asServer)
        {
            if (asServer)
                IsServerInitialized = isInitialized;
            else
                IsClientInitialized = isInitialized;
        }

        /// <summary>
        /// Sets IsServerInitialized and IsClientInitialized as false;
        /// </summary>
        private void SetDeinitializedStatus()
        {
            IsServerInitialized = false;
            IsClientInitialized = false;
        }

        /// <summary>
        /// Preinitializes this object for the network.
        /// </summary>
        /// <param name="networkManager"></param>
        internal void InitializeEarly(NetworkManager networkManager, int objectId, NetworkConnection owner, bool asServer)
        {
            //Only initialize this bit once even if clientHost.
            if (!networkManager.DoubleLogic(asServer))
            {
                State = NetworkObjectState.Spawned;
                InitializeNetworkBehavioursIfDisabled();
                IsDeinitializing = false;
                //QOL references.
                NetworkManager = networkManager;
                ServerManager = networkManager.ServerManager;
                ClientManager = networkManager.ClientManager;
                ObserverManager = networkManager.ObserverManager;
                TransportManager = networkManager.TransportManager;
                TimeManager = networkManager.TimeManager;
                SceneManager = networkManager.SceneManager;
                PredictionManager = networkManager.PredictionManager;
                RollbackManager = networkManager.RollbackManager;

                SetOwner(owner);

                if (ObjectId != NetworkObject.UNSET_OBJECTID_VALUE)
                    NetworkManager.LogError($"Object was initialized twice without being reset. Object {this.ToString()}");

                ObjectId = objectId;

                /* This must be called at the beginning
                 * so that all conditions are handled by the observer
                 * manager prior to the preinitialize call on networkobserver.
                 * The method called is dependent on NetworkManager being set. */
                AddDefaultNetworkObserverConditions();
            }

            for (int i = 0; i < NetworkBehaviours.Count; i++)
                NetworkBehaviours[i].InitializeEarly(this, asServer);

            /* NetworkObserver uses some information from
             * NetworkBehaviour so it must be preinitialized
             * after NetworkBehaviours are. */
            if (asServer)
            {
                if (networkManager.TryGetInstance(out _hashGrid))
                {
                    _hashGridPosition = _hashGrid.GetHashGridPosition(this);
                    HashGridEntry = _hashGrid.GetGridEntry(this);
                }

                NetworkObserver.Initialize(this);
            }

            _networkObserverInitiliazed = true;

            InitializePredictionEarly(networkManager, asServer);
            //Add to connections objects. Collection is a hashset so this can be called twice for clientHost.
            if (owner != null)
                owner.AddObject(this);
        }

        private void TimeManager_Update()
        {
            TimeManager_OnUpdate_Prediction();
        }

        /// <summary>
        /// Sets this NetworkObject as a child of another at runtime.
        /// </summary>
        /// <param name="nb">NetworkBehaviour to use as root. Use null to remove parenting.</param>
        public void SetParent(NetworkBehaviour nb)
        {
            if (!CanChangeParent(true))
                return;
            if (IsInvalidParent(nb))
                return;

            UpdateParent(nb);
        }

        /// <summary>
        /// Sets this NetworkObject as a child of another at runtime.
        /// </summary>
        /// <param name="nob">NetworkObject to use as root. Use null to remove parenting.</param>
        public void SetParent(NetworkObject nob)
        {
            if (!CanChangeParent(true))
                return;
            if (nob == null)
            {
                UnsetParent();
                return;
            }

            //No networkbehaviour.
            if (nob.NetworkBehaviours.Count == 0)
            {
                NetworkManager.LogWarning($"{nob.name} is not a valid parent because it does not have any NetworkBehaviours. Consider adding {nameof(EmptyNetworkBehaviour)} to {nob.name} to resolve this problem.");
                return;
            }

            NetworkBehaviour newParent = nob.NetworkBehaviours[0];
            UpdateParent(newParent);
        }

        /// <summary>
        /// Unsets this NetworkObject's parent at runtime.
        /// </summary>
        public void UnsetParent()
        {
            UpdateParent(null);
        }

        /// <summary>
        /// Updates which NetworkBehaviour to use as a parent.
        /// </summary>
        /// 
        private void UpdateParent(NetworkBehaviour newParent)
        {
            NetworkBehaviour thisNb;

            if (NetworkBehaviours.Count == 0)
            {
                NetworkManager.LogWarning($"{gameObject.name} cannot have it's parent updated because it does not have any NetworkBehaviours. Consider adding {nameof(EmptyNetworkBehaviour)} to {gameObject.name} to resolve this problem.");
                return;
            }
            else
            {
                //Always use the first to make life easier on everyone.
                thisNb = NetworkBehaviours[0];
            }

            //If current is set then remove from as child.
            if (RuntimeParentNetworkBehaviour != null)
                RuntimeParentNetworkBehaviour.NetworkObject.RuntimeChildNetworkBehaviours.Remove(thisNb);

            //If no new parent, then parent is being removed.
            if (newParent == null)
            {
                RuntimeParentNetworkBehaviour = null;
                transform.SetParent(null);
            }
            //Being set to something.
            else
            {
                RuntimeParentNetworkBehaviour = newParent;
                newParent.NetworkObject.RuntimeChildNetworkBehaviours.Add(thisNb);
                transform.SetParent(newParent.transform);
            }

            /* Rebuild observers since root changed.
             *
             * This only occurs if this nob is network spawned.
             * If not spawned the rebuild will occur after the
             * user calls Spawn on the nob/object. */
            if (NetworkManager != null)
                NetworkManager.ServerManager.Objects.RebuildObservers(this);
        }

        /// <summary>
        /// Returns if this NetworkObject can change it's parent.
        /// </summary>
        private bool CanChangeParent(bool logFailure)
        {
            if (IsSceneObject)
                return true;
            //No limitations on nobs without initialized parents.
            if (InitializedParentNetworkBehaviour == null)
                return true;

            if (logFailure)
                NetworkManager.LogWarning($"{this.ToString()} cannot have it's parent changed because it's nested. Only nested scene objects may change their parent runtime.");

            return false;
        }

        /// <summary>
        /// True if the NetworkObject specified cannot be used as a parent.
        /// </summary>
        /// <param name="nb"></param>
        /// <returns></returns>
        private bool IsInvalidParent(NetworkBehaviour nb)
        {
            /* Scene objects could face destruction if the user
             * childs them to an instantiated object that gets despawned.
             * If that occurs, the user is at fault. However a destroyed
             * scene object should be fine, it just won't spawn later given
             * it's been destroyed. Allow scene objects to change parents freely. */
            if (IsSceneObject)
                return false;

            //Setting to already current runtime parent. No need to make a change.
            if (nb == RuntimeParentNetworkBehaviour)
                return true;
            //Trying to parent a non-global to a global.
            if (nb.NetworkObject.IsGlobal && !IsGlobal)
            {
                NetworkManager.LogWarning($"{nb.NetworkObject.name} is a global NetworkObject but {gameObject.name} is not. Only global NetworkObjects can be set as a child of another global NetworkObject.");
                return true;
            }

            //Setting to self.
            if (nb.NetworkObject == this)
            {
                NetworkManager.LogWarning($"{gameObject.name} cannot be set as a child of itself.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a NetworkBehaviour and serializes it's components.
        /// </summary>
        internal T AddAndSerialize<T>() where T : NetworkBehaviour //runtimeNB, might need to be public for users.
        {
            int startingLength = NetworkBehaviours.Count;
            T result = gameObject.AddComponent<T>();

            //Add to network behaviours.
            NetworkBehaviours.Add(result);
            //Serialize values and return.
            result.SerializeComponents(this, (byte)startingLength);
            return result;
        }

        /// <summary>
        /// Sets values as they are during initialization, such as componentId, NetworkBehaviour Ids, and more.
        /// Starts with a 0 componentId.
        /// </summary>
        internal void SetInitializedValues(NetworkObject parentNob, bool force = false)
        {
            byte componentId = 0;
            SetInitializedValues(parentNob, ref componentId, force);
        }

        /// <summary>
        /// Sets values as they are during initialization, such as componentId, NetworkBehaviour Ids, and more.
        /// </summary>
        /// <param name="componentId">ComponentId to start from for the NetworkObject.</param>
        internal void SetInitializedValues(NetworkObject parentNob, ref byte componentId, bool force = false)
        {
            if (!ApplicationState.IsPlaying())
            {
                Debug.LogError($"Method {nameof(SetInitializedValues)} should only be called at runtime.");
                return;
            }

            /* If NetworkBehaviours is null then all collections are.
             * Set values for each collection. */
            if (force || !_initializedValusSet)
            {
                /* This only runs when playing, so it's safe to return existing to the pool. */
                StoreCollections();

                RetrieveCollections();

                _initializedValusSet = true;
            }

            SerializeTransformProperties();
            SetIsNestedThroughTraversal();
            /* This method can be called by the developer initializing prefabs, the prefab collection doing it automatically,
             * or when the networkobject is modified or added to an object.
             *
             * Prefab collections generally contain all prefabs, meaning they will not only call this on the topmost
             * networkobject but also each child, as the child would be it's own prefab in the collection. This assumes
             * that is, the child is a nested prefab.
             *
             * Because of this potential a check must be done where if the componentIndex is 0 we must look
             * for a networkobject above this one. If there is a networkObject above this one then we know the prefab
             * is being initialized individually, not part of a recursive check. In this case exit early
             * as the parent would have already resolved the needed information. */

            //If first componentIndex make sure there's no more than maximum allowed nested nobs.
            if (componentId == 0)
            {
                /* It's not possible to be nested while also having a componentIndex of 0.
                 * This would mean that the networkObject is being initialized outside of a
                 * recursive check. We only want to initialize recursively, or when not nested. */
                if (IsNested)
                    return;

                if (GetComponentsInChildren<NetworkObject>(true).Length > NetworkBehaviour.MAXIMUM_NETWORKBEHAVIOURS)
                {
                    NetworkManagerExtensions.LogError($"The number of child NetworkObjects on {gameObject.name} exceeds the maximum of {NetworkBehaviour.MAXIMUM_NETWORKBEHAVIOURS}.");
                    return;
                }
            }

            NetworkBehaviours.Clear();

            if (TryGetComponent(out PredictedSpawn ps))
                PredictedSpawn = ps;
            if (TryGetComponent(out PredictedOwner po))
                PredictedOwner = po;

            ComponentIndex = componentId;

            /* Since the parent being passed in should have already
             * added an empty nb if one didn't exist it's safe to
             * pull the first nb. If this value is null then something went
             * wrong. */
            if (parentNob != null)
            {
                /* Try to add an emptyNetworkBehaviour to this objects parent
                 * if one does not already exist. This is so this networkObject can
                 * identify it's parent properly. */
                AddEmptyNetworkBehaviour(parentNob, transform.parent, true);

                if (!transform.parent.TryGetComponent(out NetworkBehaviour parentNb))
                    NetworkManagerExtensions.LogError($"A NetworkBehaviour is expected to exist on {parentNob.name} but does not.");
                else
                    InitializedParentNetworkBehaviour = parentNb;
            }

            //Transforms which can be searched for networkbehaviours.
            List<Transform> transformCache = CollectionCaches<Transform>.RetrieveList();
            InitializedNestedNetworkObjects.Clear();

            transformCache.Add(transform);
            for (int z = 0; z < transformCache.Count; z++)
            {
                Transform currentT = transformCache[z];
                for (int i = 0; i < currentT.childCount; i++)
                {
                    Transform t = currentT.GetChild(i);
                    /* If contains a nob then do not add to transformsCache.
                     * Do add to ChildNetworkObjects so it can be initialized when
                     * parent is. */
                    if (t.TryGetComponent(out NetworkObject childNob))
                    {
                        /* Make sure both objects have the same value for
                         * IsSceneObject. It's possible the user instantiated
                         * an object and placed it beneath a scene object
                         * before the scene initialized. They may also
                         * add a scene object under an instantiated, even though
                         * this almost certainly will break things. */
                        if (IsSceneObject == childNob.IsSceneObject)
                            InitializedNestedNetworkObjects.Add(childNob);
                    }
                    else
                    {
                        transformCache.Add(t);
                    }
                }
            }

            //Iterate all cached transforms and get networkbehaviours.
            List<NetworkBehaviour> nbCache = CollectionCaches<NetworkBehaviour>.RetrieveList();
            //
            List<NetworkBehaviour> nbCache2 = CollectionCaches<NetworkBehaviour>.RetrieveList();
            for (int i = 0; i < transformCache.Count; i++)
            {
                nbCache2.Clear();
                transformCache[i].GetNetworkBehavioursNonAlloc(ref nbCache2);
                nbCache.AddRange(nbCache2);
            }

            /* If there's no NBs then add an empty one.
             * All NetworkObjects must have at least 1 NetworkBehaviour
             * to allow nesting. */
            if (nbCache.Count == 0)
            {
                NetworkBehaviour addedNb = AddEmptyNetworkBehaviour(this, transform, false);
                if (addedNb != null)
                    nbCache.Add(addedNb);
            }

            //Copy to array.
            int nbCount = nbCache.Count;
            //
            for (int i = 0; i < nbCount; i++)
            {
                NetworkBehaviour nb = nbCache[i];
                NetworkBehaviours.Add(nb);
                nb.SerializeComponents(this, (byte)i);
            }

            CollectionCaches<Transform>.Store(transformCache);
            CollectionCaches<NetworkBehaviour>.Store(nbCache);
            CollectionCaches<NetworkBehaviour>.Store(nbCache2);

            //Tell children nobs to update their NetworkBehaviours.
            foreach (NetworkObject item in InitializedNestedNetworkObjects)
            {
                componentId++;
                item.SetInitializedValues(this, ref componentId, force);
            }

            //Update global states to that of this one.
            SetChildGlobalState();
        }

        /// <summary>
        /// Adds EmptyNetworkBehaviour a target if it has no NetworkBehaviours. Updates a NetworkObject to contain the added behaviour.
        /// </summary>
        /// <typeparam name="addToNetworkBehaviours">If true an added NetworkBehaviour will be adeded to NetworkBehaviours, and initialized.</typeparam>
        /// <returns>Added NetworkBehaviour, or first NetworkBehaviour on the target if adding was not required.</returns>
        private NetworkBehaviour AddEmptyNetworkBehaviour(NetworkObject nob, Transform target, bool addToNetworkBehaviours)
        {
            NetworkBehaviour result;
            //Add to target if it does not have a NB yet.
            if (!target.TryGetComponent(out result))
            {
                //Already at maximum.
                if (nob.NetworkBehaviours.Count == NetworkBehaviour.MAXIMUM_NETWORKBEHAVIOURS)
                {
                    Debug.LogError($"NetworkObject {this.ToString()} already has a maximum of {NetworkBehaviour.MAXIMUM_NETWORKBEHAVIOURS}. {nameof(EmptyNetworkBehaviour)} cannot be added. Nested spawning will likely fail for this object.");
                    return null;
                }

                result = target.gameObject.AddComponent<EmptyNetworkBehaviour>();
                if (addToNetworkBehaviours)
                {
                    nob.NetworkBehaviours.Add(result);
                    result.SerializeComponents(nob, (byte)(nob.NetworkBehaviours.Count - 1));
                }
            }

            return result;
        }

        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        internal void Initialize(bool asServer, bool invokeSyncTypeCallbacks)
        {
            SetInitializedStatus(true, asServer);
            InvokeStartCallbacks(asServer, invokeSyncTypeCallbacks);
        }

        /// <summary>
        /// Returns if a deinitialize call can process.
        /// </summary>
        internal bool CanDeinitialize(bool asServer)
        {
            if (NetworkManager == null)
                return false;
            else if (asServer && !IsServerInitialized)
                return false;
            else if (!asServer && !IsClientInitialized)
                return false;

            return true;
        }

        /// <summary>
        /// Called to prepare this object to be destroyed or disabled.
        /// </summary>
        internal void Deinitialize(bool asServer)
        {
            if (!CanDeinitialize(asServer))
                return;

            Deinitialize_Prediction(asServer);

            InvokeStopCallbacks(asServer, true);
            for (int i = 0; i < NetworkBehaviours.Count; i++)
                NetworkBehaviours[i].Deinitialize(asServer);

            bool asServerOnly = (asServer && !IsClientInitialized);

            if (asServer)
            {
                if (NetworkObserver != null)
                    NetworkObserver.Deinitialize(destroyed: false);
                IsDeinitializing = true;
            }
            else
            {
                //Client only.
                bool asClientOnly = !NetworkManager.IsServerStarted;
                if (asClientOnly)
                    IsDeinitializing = true;

                RemoveClientRpcLinkIndexes();
            }

            if (!asServer ||  asServerOnly)
                PredictedSpawner = NetworkManager.EmptyConnection;

            SetInitializedStatus(false, asServer);

            if (asServer)
                Observers.Clear();
        }

        /// <summary>
        /// Resets the state of this NetworkObject.
        /// This is used internally and typically with custom object pooling.
        /// </summary>
        public void ResetState(bool asServer)
        {
            int count = NetworkBehaviours.Count;
            for (int i = 0; i < count; i++)
                NetworkBehaviours[i].ResetState(asServer);

            ResetState_Prediction(asServer);
            ResetState_Observers(asServer);

            State = NetworkObjectState.Unset;
            SetOwner(NetworkManager.EmptyConnection);
            if (NetworkObserver != null)
                NetworkObserver.Deinitialize(false);

            //QOL references.
            NetworkManager = null;
            ServerManager = null;
            ClientManager = null;
            ObserverManager = null;
            TransportManager = null;
            TimeManager = null;
            SceneManager = null;
            RollbackManager = null;
            //Misc sets.
            ObjectId = NetworkObject.UNSET_OBJECTID_VALUE;
        }

        /// <summary>
        /// Removes ownership from all clients.
        /// </summary>
        public void RemoveOwnership(bool includeNested = false)
        {
            GiveOwnership(null, asServer: true, includeNested);
        }

        /// <summary>
        /// Gives ownership to newOwner.
        /// </summary>
        /// <param name="newOwner"></param>
        public void GiveOwnership(NetworkConnection newOwner) => GiveOwnership(newOwner, asServer: true, includeNested: false);

        /// <summary>
        /// Gives ownership to newOwner.
        /// </summary>
        /// <param name="newOwner"></param>
        public void GiveOwnership(NetworkConnection newOwner, bool asServer) => GiveOwnership(newOwner, asServer, includeNested: false);

        /// <summary>
        /// Gives ownership to newOwner.
        /// </summary>
        /// <param name="newOwner"></param>
        //Remove at --- In V5 make IncludeNested required.
        internal void GiveOwnership(NetworkConnection newOwner, bool asServer, bool includeNested = false)
        {
            /* Additional asServer checks. */
            if (asServer)
            {
                if (!NetworkManager.IsServerStarted)
                {
                    NetworkManager.LogWarning($"Ownership cannot be given for object {gameObject.name}. Only server may give ownership.");
                    return;
                }

                //If the same owner don't bother sending a message, just ignore request.
                if (newOwner == Owner)
                    return;

                if (newOwner != null && newOwner.IsActive && !newOwner.LoadedStartScenes(true))
                {
                    NetworkManager.LogWarning($"Ownership has been transfered to ConnectionId {newOwner.ClientId} but this is not recommended until after they have loaded start scenes. You can be notified when a connection loads start scenes by using connection.OnLoadedStartScenes on the connection, or SceneManager.OnClientLoadStartScenes.");
                }
            }

            bool activeNewOwner = (newOwner != null && newOwner.IsActive);

            //Set prevOwner, disallowing null.
            NetworkConnection prevOwner = Owner;
            if (prevOwner == null)
                prevOwner = NetworkManager.EmptyConnection;

            SetOwner(newOwner);
            /* Only modify objects if asServer or not
             * host. When host, server would
             * have already modified objects
             * collection so there is no need
             * for client to as well. */
            if (asServer || !NetworkManager.IsHostStarted)
            {
                if (activeNewOwner)
                    newOwner.AddObject(this);
                if (prevOwner != newOwner)
                    prevOwner.RemoveObject(this);
            }

            //After changing owners invoke callbacks.
            InvokeManualOwnershipChange(prevOwner, asServer);

            //If asServer send updates to clients as needed.
            if (asServer)
            {
                if (activeNewOwner)
                    ServerManager.Objects.RebuildObservers(this, newOwner, false);

                PooledWriter writer = WriterPool.Retrieve();
                writer.WritePacketIdUnpacked(PacketId.OwnershipChange);
                writer.WriteNetworkObject(this);
                writer.WriteNetworkConnection(Owner);
                //If sharing then send to all observers.
                if (NetworkManager.ServerManager.ShareIds)
                {
                    NetworkManager.TransportManager.SendToClients((byte)Channel.Reliable, writer.GetArraySegment(), this);
                }
                //Only sending to old / new.
                else
                {
                    if (prevOwner.IsActive)
                        NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, writer.GetArraySegment(), prevOwner);
                    if (activeNewOwner)
                        NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, writer.GetArraySegment(), newOwner);
                }

                writer.Store();

                if (prevOwner.IsActive)
                    ServerManager.Objects.RebuildObservers(prevOwner);
            }

            if (includeNested)
            {
                List<NetworkObject> allNested = RetrieveNestedNetworkObjects(recursive: true);

                foreach (NetworkObject nob in allNested)
                    nob.GiveOwnership(newOwner, asServer, includeNested: true);

                CollectionCaches<NetworkObject>.Store(allNested);
            }
        }

        /// <summary>
        /// Initializes a predicted object for client.
        /// </summary>
        internal void InitializePredictedObject_Server(NetworkConnection predictedSpawner)
        {
            PredictedSpawner = predictedSpawner;
        }

        /// <summary>
        /// Initializes a predicted object for client.
        /// </summary>
        internal void InitializePredictedObject_Client(NetworkManager manager, int objectId, NetworkConnection owner, NetworkConnection predictedSpawner)
        {
            PredictedSpawner = predictedSpawner;
            InitializeEarly(manager, objectId, owner, false);
        }

        /// <summary>
        /// Deinitializes this predicted spawned object.
        /// </summary>
        internal void DeinitializePredictedObject_Client()
        {
            Deinitialize(asServer: false);
        }

        /// <summary>
        /// Sets the owner of this object.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="allowNull"></param>
        private void SetOwner(NetworkConnection owner)
        {
            Owner = owner;
        }

        /// <summary>
        /// Returns changed properties between this transform and values.
        /// </summary>
        internal TransformPropertiesFlag GetTransformChanges(TransformProperties stp)
        {
            Transform t = transform;
            return GetTransformChanges(t, stp.Position, stp.Rotation, stp.Scale);
        }

        /// <summary>
        /// Returns changed properties between this transform and a prefab.
        /// </summary>
        internal TransformPropertiesFlag GetTransformChanges(GameObject prefab)
        {
            Transform prefabT = prefab.transform;
            return GetTransformChanges(transform, prefabT.localPosition, prefabT.localRotation, prefabT.localScale);
        }

        /// <summary>
        /// Returns changed properties between a transform and values.
        /// </summary>
        private TransformPropertiesFlag GetTransformChanges(Transform t, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            TransformPropertiesFlag tpf = TransformPropertiesFlag.Unset;
            if (t.localPosition != localPosition)
                tpf |= TransformPropertiesFlag.Position;
            if (t.localRotation != localRotation)
                tpf |= TransformPropertiesFlag.Rotation;
            if (t.localScale != localScale)
                tpf |= TransformPropertiesFlag.LocalScale;

            return tpf;
        }

        /// <summary>
        /// Sets IsNested and returns the result.
        /// </summary>
        /// <returns></returns>
        private bool SetIsNestedThroughTraversal()
        {
            Transform parent = transform.parent;
            //Iterate long as parent isn't null, and isnt self.
            while (parent != null && parent != transform)
            {
                if (parent.TryGetComponent<NetworkObject>(out _))
                {
                    IsNested = true;
                    return IsNested;
                }

                parent = parent.parent;
            }

            //No NetworkObject found in parents, meaning this is not nested.
            IsNested = false;
            return IsNested;
        }

        /// <summary>
        /// Serializes TransformProperties to current transform properties.
        /// Returns if serialized value changed.
        /// </summary>
        internal void SerializeTransformProperties()
        {
            SerializedTransformProperties = new(transform.localPosition, transform.localRotation, transform.localScale);
        }

        /// <summary>
        /// Stores collections to caches.
        /// </summary>
        private void StoreCollections()
        {
            CollectionCaches<NetworkBehaviour>.StoreAndDefault(ref NetworkBehaviours);
            CollectionCaches<NetworkObject>.StoreAndDefault(ref InitializedNestedNetworkObjects);
            CollectionCaches<NetworkBehaviour>.StoreAndDefault(ref RuntimeChildNetworkBehaviours);
        }

        private void RetrieveCollections()
        {
            NetworkBehaviours = CollectionCaches<NetworkBehaviour>.RetrieveList();
            InitializedNestedNetworkObjects = CollectionCaches<NetworkObject>.RetrieveList();
            RuntimeChildNetworkBehaviours = CollectionCaches<NetworkBehaviour>.RetrieveList();
        }

        #region Editor.
#if UNITY_EDITOR
        /// <summary>
        /// Removes duplicate NetworkObject components on this object returning the removed count.
        /// </summary>
        /// <returns></returns>
        internal int RemoveDuplicateNetworkObjects()
        {
            NetworkObject[] nobs = GetComponents<NetworkObject>();
            for (int i = 1; i < nobs.Length; i++)
                DestroyImmediate(nobs[i]);

            return (nobs.Length - 1);
        }

        private void OnValidate()
        {
            //SetIsNestedThroughTraversal();
            //SceneUpdateNetworkBehaviours();
            ReferenceIds_OnValidate();

            if (IsGlobal && IsSceneObject)
                NetworkManagerExtensions.LogWarning($"Object {gameObject.name} will have it's IsGlobal state ignored because it is a scene object. Instantiated copies will still be global. This warning is informative only.");
        }

        //Unity callback.
        private void Reset()
        {
            //SetIsNestedThroughTraversal();
            //SceneUpdateNetworkBehaviours();
            ReferenceIds_Reset();
        }

        // private void SceneUpdateNetworkBehaviours()
        // {
        //     if (!gameObject.scene.IsValid())
        //         return;
        //
        //     if (IsNested)
        //         return;
        //
        //     byte componentIndex = 0;
        //     UpdateNetworkBehaviours(null, ref componentIndex);
        // }

        private void OnDrawGizmosSelected()
        {
            _editorOwnerId = (Owner == null) ? -1 : Owner.ClientId;
        }
#endif
        #endregion
    }
}