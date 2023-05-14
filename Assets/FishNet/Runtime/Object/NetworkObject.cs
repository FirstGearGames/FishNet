﻿using FishNet.Managing;
using FishNet.Connection;
using UnityEngine;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FishNet.Utility.Performance;
using System;
using FishNet.Managing.Object;
using FishNet.Component.Ownership;
using FishNet.Component.Observing;
using FishNet.Serializing.Helping;
using FishNet.Component.Transforming;
using FishNet.Utility.Extension;
using FishNet.Object.Prediction;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishNet.Object
{
    [DisallowMultipleComponent]
    public partial class NetworkObject : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// True if was nested during scene setup or within a prefab.
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
#if PREDICTION_V2
        /// <summary>
        /// True if this object uses prediciton methods.
        /// </summary>
        public bool UsePrediction => _usePrediction;
        [Tooltip("True if this object uses prediction methods.")]
        [SerializeField]
        private bool _usePrediction;
        /// <summary>
        /// Object containing graphics when using prediction. This should be child of the predicted root.
        /// </summary>
        [Tooltip("Object containing graphics when using prediction. This should be child of the predicted root.")]
        [SerializeField]
        private Transform _graphicalObject;
        /// <summary>
        /// True to use state and input forwarding. This is ideal with games where you want all clients and server to run the same inputs. False to only use prediction on the owner, and synchronize to spectators using other means such as a NetworkTransform.
        /// </summary>
        [Tooltip("True to use state and input forwarding. This is ideal with games where you want all clients and server to run the same inputs. False to only use prediction on the owner, and synchronize to spectators using other means such as a NetworkTransform.")]
        [SerializeField]
        private bool _useStates = true;
        /// <summary>
        /// How many ticks to interpolate graphics on objects owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.
        /// </summary>
        [Tooltip("How many ticks to interpolate graphics on objects owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.")]
        [Range(1, byte.MaxValue)]
        [SerializeField]
        private byte _ownerInterpolation = 1;
        /// <summary>
        /// True to enable teleport threshhold.
        /// </summary>
        [Tooltip("True to enable teleport threshhold.")]
        [SerializeField]
        private bool _enableTeleport;
        /// <summary>
        /// Distance the graphical object must be from root to teleport rather than smooth over time.
        /// </summary>
        [Tooltip("Distance the graphical object must be from root to teleport rather than smooth over time.")]
        [Range(0f, short.MaxValue)]
        [SerializeField]
        private float _ownerTeleportThreshold = 1f;
        ///// <summary>
        ///// How far into the future to predict inputs. This is best set to 0f for player controllers, but may provide more real time physics interactions with force driven rigidbodies.
        ///// </summary>
        //[Tooltip("How far into the future to predict inputs. This is best set to 0f for player controllers, but may provide more real time physics interactions with force driven rigidbodies.")]
        //[Range(0f, 1f)]
        //[SerializeField]
        //private float _futurePredictionTime = 0f;
        ///// <summary>
        ///// True if this object is set to use future prediction.
        ///// </summary>
        //internal bool PredictsFuture => (_futurePredictionTime > 0f);
        /// <summary>
        /// True to use a flat amount of interpolation for graphics. This is ideal for controllers that will not carry velocity, such as setting velocity directly when there is input.
        /// False to adapt interpolation based on a variety of factors. This can be beneficial when velocities are affected by forces and may change irratically.
        /// </summary>
        internal bool SpectatorAdaptiveInterpolation => _spectatorAdaptiveInterpolation;
        [Tooltip("True to use a flat amount of interpolation for graphics. This is ideal for controllers that will not carry velocity, such as setting velocity directly when there is input." +
            "False to adapt interpolation based on a variety of factors. This can be beneficial when velocities are affected by forces and may change irratically.")]
        [SerializeField]
        private bool _spectatorAdaptiveInterpolation;
        /// <summary>
        /// How many ticks to interpolate graphics on objects not owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.
        /// </summary>
        [Tooltip("How many ticks to interpolate graphics on objects not owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.")]
        [Range(1, byte.MaxValue)]
        [SerializeField]
        private byte _spectatorInterpolation = 1;
        /// <summary>
        /// How to favor smoothing for non-owned objects.
        /// </summary>
        [Tooltip("How to favor smoothing for non-owned objects. Accuracy will keep graphics more real-time while gradual will keep them further in the past to handle desynchronizations better at the cost of visuals being behind.")]
        [SerializeField]
        private AdaptiveSmoothingType _adaptiveSmoothingType = AdaptiveSmoothingType.Accuracy;
        /// <summary>
        /// Custom settings for smoothing data.
        /// </summary>
        [Tooltip("Custom settings for smoothing data.")]
        [SerializeField]
        private AdaptiveInterpolationSmoothingData _customSmoothingData = _mixedSmoothingData;
        /// <summary>
        /// Preview of selected preconfigured smoothing data. This is only used for the inspector.
        /// </summary>
        [SerializeField]
        private AdaptiveInterpolationSmoothingData _preconfiguredSmoothingDataPreview;
#endif
        /// <summary>
        /// Returns if this object was placed in the scene during edit-time.
        /// </summary>
        /// <returns></returns>
        public bool IsSceneObject => (SceneId > 0);
        /// <summary>
        /// ComponentIndex for this NetworkBehaviour.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public byte ComponentIndex { get; private set; }
        /// <summary>
        /// Unique Id for this NetworkObject. This does not represent the object owner.
        /// </summary>
        public int ObjectId { get; private set; }
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
        /// 
        /// </summary>
        [field: SerializeField, HideInInspector]
        private NetworkBehaviour[] _networkBehaviours;
        /// <summary>
        /// NetworkBehaviours within the root and children of this object.
        /// </summary>        
        public NetworkBehaviour[] NetworkBehaviours
        {
            get => _networkBehaviours;
            private set => _networkBehaviours = value;
        }
        /// <summary>
        /// NetworkObject parenting this instance. The parent NetworkObject will be null if there was no parent during serialization.
        /// </summary>
        [field: SerializeField, HideInInspector]
        public NetworkObject ParentNetworkObject { get; private set; }
        /// <summary>
        /// NetworkObjects nested beneath this one. Recursive NetworkObjects may exist within each entry of this field.
        /// </summary> 
        [field: SerializeField, HideInInspector]
        public List<NetworkObject> ChildNetworkObjects { get; private set; } = new List<NetworkObject>();
        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        internal TransformProperties SerializedTransformProperties = new TransformProperties();
        /// <summary>
        /// Current state of the NetworkObject.
        /// </summary>
        [System.NonSerialized]
        internal NetworkObjectState State = NetworkObjectState.Unset;
        #endregion

        #region Serialized.
        /// <summary>
        /// True if the object will always initialize as a networked object. When false the object will not automatically initialize over the network. Using Spawn() on an object will always set that instance as networked.
        /// </summary>
        public bool IsNetworked
        {
            get => _isNetworked;
            private set => _isNetworked = value;
        }
        /// <summary>
        /// Sets IsNetworked value. This method must be called before Start.
        /// </summary>
        /// <param name="value">New IsNetworked value.</param>
        public void SetIsNetworked(bool value)
        {
            IsNetworked = value;
        }
        [Tooltip("True if the object will always initialize as a networked object. When false the object will not automatically initialize over the network. Using Spawn() on an object will always set that instance as networked.")]
        [SerializeField]
        private bool _isNetworked = true;
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
            if (IsNested)
            {
                NetworkManager.StaticLogWarning($"Object {gameObject.name} cannot change IsGlobal because it is nested. Only root objects may be set global.");
                return;
            }
            if (!IsDeinitializing)
            {
                NetworkManager.StaticLogWarning($"Object {gameObject.name} cannot change IsGlobal as it's already initialized. IsGlobal may only be changed immediately after instantiating.");
                return;
            }
            if (IsSceneObject)
            {
                NetworkManager.StaticLogWarning($"Object {gameObject.name} cannot have be global because it is a scene object. Only instantiated objects may be global.");
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
        [Tooltip("Order to initialize this object's callbacks when spawned with other NetworkObjects in the same tick. Default value is 0, negative values will execute callbacks first.")]
        [SerializeField]
        private sbyte _initializeOrder = 0;
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
        #endregion

        #region Const.
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

        protected virtual void Awake()
        {
            _isStatic = gameObject.isStatic;
            SetChildDespawnedState();
#if PREDICTION_V2
            Prediction_Awake();
#endif
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
            else if (IsServer && !IsNested && gameObject.activeSelf)
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
                        if (pNob != ParentNetworkObject)
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
            //Does this need to be here? I'm thinking no, remove it and examine later. //todo
            if (Owner.IsValid)
                Owner.RemoveObject(this);

            NetworkObserver?.Deinitialize(true);
            //Already being deinitialized by FishNet.
            if (IsDeinitializing)
                return;

            if (NetworkManager != null)
            {
                //Was destroyed without going through the proper methods.
                if (NetworkManager.IsServer)
                    NetworkManager.ServerManager.Objects.NetworkObjectUnexpectedlyDestroyed(this, true);
                if (NetworkManager.IsClient)
                    NetworkManager.ClientManager.Objects.NetworkObjectUnexpectedlyDestroyed(this, false);
            }

            /* When destroyed unexpectedly it's
             * impossible to know if this occurred on
             * the server or client side, so send callbacks
             * for both. */
            if (IsServer)
                InvokeStopCallbacks(true);
            if (IsClient)
                InvokeStopCallbacks(false);

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
            IsDeinitializing = true;

            SetActiveStatus(false);
            //Do not need to set state if being destroyed.
            //Don't need to reset sync types if object is being destroyed.
        }

#if PREDICTION_V2
        private void Update()
        {
            Prediction_Update();
        }
#endif

        /// <summary>
        /// Initializes NetworkBehaviours if they are disabled.
        /// </summary>
        private void InitializeNetworkBehavioursIfDisabled()
        {
            if (_disabledNetworkBehavioursInitialized)
                return;
            _disabledNetworkBehavioursInitialized = true;

            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InitializeIfDisabled();
        }

        /// <summary>
        /// Sets Despawned on child NetworkObjects if they are not enabled.
        /// </summary>
        private void SetChildDespawnedState()
        {
            NetworkObject nob;
            for (int i = 0; i < ChildNetworkObjects.Count; i++)
            {
                nob = ChildNetworkObjects[i];
                if (!nob.gameObject.activeSelf)
                    nob.State = NetworkObjectState.Despawned;
            }
        }

        /// <summary>
        /// Deactivates this NetworkObject during it's start cycle if conditions are met.
        /// </summary>
        internal void TryStartDeactivation()
        {
            if (!IsNetworked)
                return;

            //Global.
            if (IsGlobal && !IsSceneObject)
                DontDestroyOnLoad(gameObject);

            if (NetworkManager == null || (!NetworkManager.IsClient && !NetworkManager.IsServer))
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
        private void SetActiveStatus(bool isActive, bool server)
        {
            if (server)
                IsServer = isActive;
            else
                IsClient = isActive;
        }
        /// <summary>
        /// Sets IsClient and IsServer to isActive.
        /// </summary>
        private void SetActiveStatus(bool isActive)
        {
            IsServer = isActive;
            IsClient = isActive;
        }
        /// <summary>
        /// Preinitializes this object for the network.
        /// </summary>
        /// <param name="networkManager"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Preinitialize_Internal(NetworkManager networkManager, int objectId, NetworkConnection owner, bool asServer)
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
                ObjectId = objectId;

                /* This must be called at the beginning
                 * so that all conditions are handled by the observer
                 * manager prior to the preinitialize call on networkobserver. 
                 * The method called is dependent on NetworkManager being set. */
                AddDefaultNetworkObserverConditions();
            }

            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].Preinitialize_Internal(this, asServer);

            /* NetworkObserver uses some information from
             * NetworkBehaviour so it must be preinitialized
             * after NetworkBehaviours are. */
            if (asServer)
            {
                _hashGrid = networkManager.GetInstance<HashGrid>(false);
                if (_hashGrid != null)
                {
                    _hashGridPosition = _hashGrid.GetHashGridPosition(this);                    
                    HashGridEntry = _hashGrid.GetGridEntry(_hashGridPosition);
                }
                NetworkObserver.Initialize(this);
            }
            _networkObserverInitiliazed = true;

#if PREDICTION_V2
            Prediction_Preinitialize(networkManager, asServer);
#endif
            //Add to connections objects. Collection is a hashset so this can be called twice for clientHost.
            owner?.AddObject(this);
        }

        /// <summary>
        /// Adds a NetworkBehaviour and serializes it's components.
        /// </summary>
        internal T AddAndSerialize<T>() where T : NetworkBehaviour //runtimeNB, might need to be public for users.
        {
            int startingLength = NetworkBehaviours.Length;
            T result = gameObject.AddComponent<T>();
            //Add to network behaviours.
            Array.Resize(ref _networkBehaviours, startingLength + 1);
            _networkBehaviours[startingLength] = result;
            //Serialize values and return.
            result.SerializeComponents(this, (byte)startingLength);
            return result;
        }

        /// <summary>
        /// Updates NetworkBehaviours and initializes them with serialized values.
        /// </summary>
        internal void UpdateNetworkBehaviours(NetworkObject parentNob, ref byte componentIndex) //runtimeNB, might need to be public for users.
        {
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
            if (componentIndex == 0)
            {
                //Not possible for index to be 0 and nested.
                if (IsNested)
                    return;
                byte maxNobs = 255;
                if (GetComponentsInChildren<NetworkObject>(true).Length > maxNobs)
                {
                    Debug.LogError($"The number of child NetworkObjects on {gameObject.name} exceeds the maximum of {maxNobs}.");
                    return;
                }
            }

            PredictedSpawn = GetComponent<PredictedSpawn>();
            ComponentIndex = componentIndex;
            ParentNetworkObject = parentNob;

            //Transforms which can be searched for networkbehaviours.
            List<Transform> transformCache = CollectionCaches<Transform>.Retrieve();
            ChildNetworkObjects.Clear();

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
                            ChildNetworkObjects.Add(childNob);
                    }
                    else
                    {
                        transformCache.Add(t);
                    }
                }
            }

            //Iterate all cached transforms and get networkbehaviours.
            List<NetworkBehaviour> nbCache = CollectionCaches<NetworkBehaviour>.Retrieve();
            //
            List<NetworkBehaviour> nbCache2 = CollectionCaches<NetworkBehaviour>.Retrieve();
            for (int i = 0; i < transformCache.Count; i++)
            {
                nbCache2.Clear();
                transformCache[i].GetNetworkBehavioursNonAlloc(ref nbCache2);
                nbCache.AddRange(nbCache2);
            }

            //Copy to array.
            int nbCount = nbCache.Count;
            NetworkBehaviours = new NetworkBehaviour[nbCount];
            //
            for (int i = 0; i < nbCount; i++)
            {
                NetworkBehaviours[i] = nbCache[i];
                NetworkBehaviours[i].SerializeComponents(this, (byte)i);
            }

            CollectionCaches<Transform>.Store(transformCache);
            CollectionCaches<NetworkBehaviour>.Store(nbCache);
            CollectionCaches<NetworkBehaviour>.Store(nbCache2);

            //Tell children nobs to update their NetworkBehaviours.
            foreach (NetworkObject item in ChildNetworkObjects)
            {
                componentIndex++;
                item.UpdateNetworkBehaviours(this, ref componentIndex);
            }
        }


        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Initialize(bool asServer, bool invokeSyncTypeCallbacks)
        {
            InitializeCallbacks(asServer, invokeSyncTypeCallbacks);
        }

        /// <summary>
        /// Called to prepare this object to be destroyed or disabled.
        /// </summary>
        internal void Deinitialize(bool asServer)
        {
#if PREDICTION_V2
            Prediction_Deinitialize(asServer);
#endif
            InvokeStopCallbacks(asServer);
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].Deinitialize(asServer);

            if (asServer)
            {
                IsDeinitializing = true;
            }
            else
            {
                ClientManager.Connection.LevelOfDetails.Remove(this);
                //Client only.
                if (!NetworkManager.IsServer)
                    IsDeinitializing = true;

                RemoveClientRpcLinkIndexes();
            }

            SetActiveStatus(false, asServer);
            if (asServer)
                Observers.Clear();
        }

        /// <summary>
        /// Resets states for object to be pooled.
        /// </summary>
        /// <param name="asServer">True if performing as server.</param>
        public void ResetForObjectPool()
        {
            int count = NetworkBehaviours.Length;
            for (int i = 0; i < count; i++)
                NetworkBehaviours[i].ResetForObjectPool();

            State = NetworkObjectState.Unset;
            SetOwner(NetworkManager.EmptyConnection);
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
            ObjectId = 0;
            ClientInitialized = false;
        }

        /// <summary>
        /// Removes ownership from all clients.
        /// </summary>
        public void RemoveOwnership()
        {
            GiveOwnership(null, true);
        }
        /// <summary>
        /// Gives ownership to newOwner.
        /// </summary>
        /// <param name="newOwner"></param>
        public void GiveOwnership(NetworkConnection newOwner)
        {
            GiveOwnership(newOwner, true);
        }
        /// <summary>
        /// Gives ownership to newOwner.
        /// </summary>
        /// <param name="newOwner"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GiveOwnership(NetworkConnection newOwner, bool asServer)
        {
            /* Additional asServer checks. */
            if (asServer)
            {
                if (!NetworkManager.IsServer)
                {
                    NetworkManager.LogWarning($"Ownership cannot be given for object {gameObject.name}. Only server may give ownership.");
                    return;
                }

                //If the same owner don't bother sending a message, just ignore request.
                if (newOwner == Owner && asServer)
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
            if (asServer || !NetworkManager.IsHost)
            {
                if (activeNewOwner)
                    newOwner.AddObject(this);
                if (prevOwner.IsValid && prevOwner != newOwner)
                    prevOwner.RemoveObject(this);
            }

            //After changing owners invoke callbacks.
            InvokeOwnership(prevOwner, asServer);

            //If asServer send updates to clients as needed.
            if (asServer)
            {
                if (activeNewOwner)
                    ServerManager.Objects.RebuildObservers(this, newOwner);

                using (PooledWriter writer = WriterPool.GetWriter())
                {
                    writer.WritePacketId(PacketId.OwnershipChange);
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
                }

                if (prevOwner.IsActive)
                    ServerManager.Objects.RebuildObservers(prevOwner);
            }
        }


        /// <summary>
        /// Initializes a predicted object for client.
        /// </summary>
        internal void InitializePredictedObject_Server(NetworkManager manager, NetworkConnection predictedSpawner)
        {
            NetworkManager = manager;
            PredictedSpawner = predictedSpawner;
        }


        /// <summary>
        /// Initializes a predicted object for client.
        /// </summary>
        internal void PreinitializePredictedObject_Client(NetworkManager manager, int objectId, NetworkConnection owner, NetworkConnection predictedSpawner)
        {
            PredictedSpawner = predictedSpawner;
            Preinitialize_Internal(manager, objectId, owner, false);
        }

        /// <summary>
        /// Deinitializes this predicted spawned object.
        /// </summary>
        internal void DeinitializePredictedObject_Client()
        {
            /* For the time being we're just going to disable the object because
             * deinitializing instead could present a lot of problems.
             * For example: if client deinitializes rpc links are unregistered,
             * and if server had a rpc on the way already the link would
             * not be found. This would cause the reader length to be wrong
             * resulting in packet corruption. */
            gameObject.SetActive(false);
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
        /// Returns if this NetworkObject is a scene object, and has changed.
        /// </summary>
        /// <returns></returns>
        internal ChangedTransformProperties GetTransformChanges(TransformProperties stp)
        {
            ChangedTransformProperties ctp = ChangedTransformProperties.Unset;
            if (transform.localPosition != stp.Position)
                ctp |= ChangedTransformProperties.LocalPosition;
            if (transform.localRotation != stp.Rotation)
                ctp |= ChangedTransformProperties.LocalRotation;
            if (transform.localScale != stp.LocalScale)
                ctp |= ChangedTransformProperties.LocalScale;

            return ctp;
        }

        /// <summary>
        /// Returns if this NetworkObject is a scene object, and has changed.
        /// </summary>
        /// <returns></returns>
        internal ChangedTransformProperties GetTransformChanges(GameObject prefab)
        {
            Transform t = prefab.transform;
            ChangedTransformProperties ctp = ChangedTransformProperties.Unset;
            if (transform.position != t.position)
                ctp |= ChangedTransformProperties.LocalPosition;
            if (transform.rotation != t.rotation)
                ctp |= ChangedTransformProperties.LocalRotation;
            if (transform.localScale != t.localScale)
                ctp |= ChangedTransformProperties.LocalScale;

            return ctp;
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

        private void OnValidate()
        {
            SetIsNestedThroughTraversal();
            SceneUpdateNetworkBehaviours();
            ReferenceIds_OnValidate();
#if PREDICTION_V2
            Prediction_OnValidate();
#endif

            if (IsGlobal && IsSceneObject)
                Debug.LogWarning($"Object {gameObject.name} will have it's IsGlobal state ignored because it is a scene object. Instantiated copies will still be global. This warning is informative only.");
        }

        private void Reset()
        {
            SetIsNestedThroughTraversal();
            SerializeTransformProperties();
            SceneUpdateNetworkBehaviours();
            ReferenceIds_Reset();
        }

        private void SceneUpdateNetworkBehaviours()
        {
            //In a scene.
            if (!string.IsNullOrEmpty(gameObject.scene.name))
            {
                if (IsNested)
                    return;

                byte componentIndex = 0;
                UpdateNetworkBehaviours(null, ref componentIndex);
            }

        }
        private void OnDrawGizmosSelected()
        {
            _editorOwnerId = (Owner == null) ? -1 : Owner.ClientId;
            SerializeTransformProperties();
        }

        /// <summary>
        /// Serializes TransformProperties to current transform properties.
        /// </summary>
        private void SerializeTransformProperties()
        {
            /* Use this method to set scene data since it doesn't need to exist outside 
            * the editor and because its updated regularly while selected. */
            //If a scene object.
            if (!EditorApplication.isPlaying && !string.IsNullOrEmpty(gameObject.scene.name))
            {
                SerializedTransformProperties = new TransformProperties(
                    transform.localPosition, transform.localRotation, transform.localScale);
            }
        }
#endif
#endregion
        }

}

