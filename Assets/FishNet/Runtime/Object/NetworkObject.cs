using FishNet.Managing;
using FishNet.Connection;
using UnityEngine;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Managing.Logging;
using FishNet.Managing.Timing;
using FishNet.Utility;
using System.Collections.Generic;
using FishNet.Utility.Performance;
using System.Runtime.CompilerServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishNet.Object
{
    [DisallowMultipleComponent]
    public sealed partial class NetworkObject : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// True if this NetworkObject was active during edit. Will be true if placed in scene during edit, and was in active state on run.
        /// </summary>
        internal bool ActiveDuringEdit;
        /// <summary>
        /// Returns if this object was placed in the scene during edit-time.
        /// </summary>
        /// <returns></returns>
        public bool SceneObject => (SceneId > 0);
        /// <summary>
        /// Unique Id for this NetworkObject. This does not represent the object owner.
        /// </summary>
        public int ObjectId { get; private set; }
        /// <summary>
        /// True if this NetworkObject is deinitializing. Will also be true until Initialize is called. May be false until the object is cleaned up if object is destroyed without using Despawn.
        /// </summary>
        internal bool Deinitializing { get; private set; } = true;
        /// <summary>
        /// NetworkBehaviours within the root and children of this object.
        /// </summary>
        public NetworkBehaviour[] NetworkBehaviours { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        internal SceneTransformProperties SceneTransformProperties = new SceneTransformProperties();
        /// <summary>
        /// NetworkManager for this object.
        /// </summary>
        public NetworkManager NetworkManager { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// Default value for IsNetworked. True if this object is acting as a NetworkedObject. Using network Spawn() will always set this object as networked.
        /// </summary>
        [Tooltip("Default value for IsNetworked. True if this object is acting as a NetworkedObject. Using network Spawn() will always set this object as networked.")]
        [SerializeField]
        private bool _isNetworked = true;
        /// <summary>
        /// Default value for IsNetworked.True if this object is acting as a NetworkedObject.Using network Spawn() will always set this object as networked.
        /// </summary>
        public bool IsNetworked
        {
            get => _isNetworked;
            private set => _isNetworked = value;
        }
        /// <summary>
        /// Sets IsNetworked value.
        /// </summary>
        /// <param name="isNetworked"></param>
        internal void SetIsNetworked(bool isNetworked)
        {
            IsNetworked = isNetworked;
            for (int i = 0; i < ChildNetworkObjects.Count; i++)
                ChildNetworkObjects[i].SetIsNetworked(isNetworked);
        }
        /// <summary>
        /// NetworkObjects which are children of this one.
        /// </summary>
        [SerializeField, HideInInspector]
        internal List<NetworkObject> ChildNetworkObjects = new List<NetworkObject>();
        #endregion

        private void Awake()
        {
            /* Only run check when playing so nested nobs are not
             * destroyed on prefabs. */
            if (ApplicationState.IsPlaying())
            {
                //If this has a parent check for higher up network objects.
                Transform start = transform.root;
                if (start != null && start != transform)
                {
                    NetworkObject parentNob = start.GetComponentInParent<NetworkObject>();
                    //Disallow child network objects for now.
                    if (parentNob != null)
                    {
                        if (InstanceFinder.NetworkManager.CanLog(LoggingType.Common))
                            Debug.Log($"NetworkObject removed from object {gameObject.name}, child of {start.name}. This message is informative only and may be ignored.");
                        Destroy(this);
                    }
                }
            }
        }

        private void Start()
        {
            if (!IsNetworked)
                return;

            if (NetworkManager == null || (!NetworkManager.IsClient && !NetworkManager.IsServer))
            {
                //ActiveDuringEdit is only used for scene objects.
                if (SceneObject)
                    ActiveDuringEdit = true;
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            /* If deinitializing and an owner exist
             * then remove object from owner. */
            if (Deinitializing && Owner.IsValid)
                Owner.RemoveObject(this);
        }

        private void OnDestroy()
        {
            //Does this need to be here? I'm thinking no, remove it and examine later. //todo
            if (Owner.IsValid)
                Owner.RemoveObject(this);
            //Already being deinitialized by FishNet.
            if (Deinitializing)
                return;

            //Was destroyed without going through the proper methods.
            if (NetworkManager.IsServer)
                NetworkManager.ServerManager.Objects.NetworkObjectUnexpectedlyDestroyed(this);
            if (NetworkManager.IsClient)
                NetworkManager.ClientManager.Objects.NetworkObjectUnexpectedlyDestroyed(this);

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
            Deinitializing = true;
        }

        /// <summary>
        /// PreInitializes this script.
        /// </summary>
        /// <param name="networkManager"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PreInitialize(NetworkManager networkManager, int objectId, NetworkConnection owner, bool asServer)
        {
            Deinitializing = false;
            NetworkManager = networkManager;
            SetOwner(owner);
            ObjectId = objectId;

            /* This must be called at the beginning
             * so that all conditions are handled by the observer
             * manager prior to the preinitialize call on networkobserver. 
             * The method called is dependent on NetworkManager being set. */
            AddDefaultNetworkObserverConditions();

            if (NetworkBehaviours == null || NetworkBehaviours.Length == 0)
            {
                //If there are no child nobs then get NetworkBehaviours normally.
                if (ChildNetworkObjects.Count == 0)
                {
                    NetworkBehaviours = GetComponentsInChildren<NetworkBehaviour>();
                }
                //There are child nobs.
                else
                {
                    //Transforms which can be searched for networkbehaviours.
                    ListCache<Transform> transformCache = ListCaches.TransformCache;
                    transformCache.Reset();

                    transformCache.AddValue(transform);

                    for (int z = 0; z < transformCache.Written; z++)
                    {
                        Transform currentT = transformCache.Collection[z];
                        for (int i = 0; i < currentT.childCount; i++)
                        {
                            Transform t = currentT.GetChild(i);
                            bool hasNob = false;
                            for (int x = 0; x < ChildNetworkObjects.Count; x++)
                            {
                                if (ChildNetworkObjects[x].transform == t)
                                {
                                    hasNob = true;
                                    break;
                                }
                            }

                            /* If the transform being checked 
                             * does not have a network object then
                             * add it to the cache. */
                            if (!hasNob)
                                transformCache.AddValue(t);
                        }
                    }

                    int written;
                    //Iterate all cached transforms and get networkbehaviours.
                    ListCache<NetworkBehaviour> nbCache = ListCaches.NetworkBehaviourCache;
                    nbCache.Reset();
                    written = transformCache.Written;
                    List<Transform> ts = transformCache.Collection;
                    //
                    for (int i = 0; i < written; i++)
                        nbCache.AddValues(ts[i].GetNetworkBehaviours());

                    //Copy to array.
                    written = nbCache.Written;
                    List<NetworkBehaviour> nbs = nbCache.Collection;
                    NetworkBehaviours = new NetworkBehaviour[written];
                    //
                    for (int i = 0; i < written; i++)
                        NetworkBehaviours[i] = nbs[i];
                }
            }

            //Check and initialize found network behaviours.
            if (NetworkBehaviours.Length > byte.MaxValue)
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Currently only {byte.MaxValue} NetworkBehaviour scripts per object are allowed. Object {gameObject.name} will not initialized.");
            }
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].PreInitialize(this, (byte)i);
            }

            /* NetworkObserver uses some information from
             * NetworkBehaviour so it must be preinitialized
             * after NetworkBehaviours are. */
            if (asServer)
                PreInitializeObservers();

            //Add to connection objects if owner exist.
            if (owner != null)
                owner.AddObject(this);
        }

        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        internal void Initialize(bool asServer)
        {
            InitializeCallbacks(asServer);
        }

        /// <summary>
        /// Called to prepare this object to be destroyed or disabled.
        /// </summary>
        internal void Deinitialize(bool asServer)
        {
            InvokeStopCallbacks(asServer);
            if (asServer)
            {
                Deinitializing = true;
            }
            else
            {
                //Client only.
                if (!NetworkManager.IsServer)
                    Deinitializing = true;

                RemoveClientRpcLinkIndexes();
            }

            if (asServer)
                Observers.Clear();
        }

        ///// <summary>
        ///// Disables this object and resets network values.
        ///// </summary>
        //internal void DisableNetworkObject()
        //{
        //    SetOwner(null, false);
        //    ObjectId = -1;
        //    Observers.Clear();
        //    NetworkManager = null;
        //}

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
                    if (NetworkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Ownership cannot be given for object {gameObject.name}. Only server may give ownership.");
                    return;
                }

                //If the same owner don't bother sending a message, just ignore request.
                if (newOwner == Owner && asServer)
                    return;

                if (newOwner != null && newOwner.IsActive && !newOwner.LoadedStartScenes)
                {
                    if (NetworkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Ownership has been transfered to ConnectionId {newOwner.ClientId} but this is not recommended until after they have loaded start scenes. You can be notified when a connection loads start scenes by using connection.OnLoadedStartScenes on the connection, or SceneManager.OnClientLoadStartScenes.");
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
                if (prevOwner.IsValid)
                    prevOwner.RemoveObject(this);
            }
            //After changing owners invoke callbacks.
            InvokeOwnership(prevOwner, asServer);

            //If asServer send updates to clients as needed.
            if (asServer)
            {
                //Rebuild for new owner first so they get change messages.
                if (activeNewOwner)
                {
                    NetworkManager.SceneManager.AddConnectionToScene(newOwner, gameObject.scene);
                    RebuildObservers(newOwner);
                }

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
                    RebuildObservers(prevOwner);
            }
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
        internal ChangedTransformProperties GetChangedSceneTransformProperties()
        {
            ChangedTransformProperties ctp = ChangedTransformProperties.Unset;
            if (transform.position != SceneTransformProperties.Position)
                ctp |= ChangedTransformProperties.Position;
            if (transform.rotation != SceneTransformProperties.Rotation)
                ctp |= ChangedTransformProperties.Rotation;
            if (transform.localScale != SceneTransformProperties.LocalScale)
                ctp |= ChangedTransformProperties.LocalScale;

            return ctp;
        }

        /* //Notes this isn't used because it would require a significant amount of work
         * to track what to reset and what not to reset, and even then there may be missed
         * data. It's better to let the user manually reset what's needed. */
        ///// <summary>
        ///// Resets this object to how it was when placed in the scene.
        ///// </summary>
        //internal void ResetSceneObject()
        //{
        //    transform.position = SceneTransformProperties.Position;
        //    transform.rotation = SceneTransformProperties.Rotation;
        //    transform.localScale = SceneTransformProperties.LocalScale;
        //    //Reset syncvars in all networkbehaviours.
        //    for (int i = 0; i < NetworkBehaviours.Length; i++)
        //        NetworkBehaviours[i].ResetSyncTypes();
        //}

        #region Editor.
#if UNITY_EDITOR
        private void OnValidate()
        {
            ////Set if there are any nobs in children.
            //NetworkObject[] nobs  = GetComponentsInChildren<NetworkObject>(true);
            //ChildNetworkObjects.Clear();
            ////Start at index 1 as 0 would be this nob.
            //for (int i = 1; i < nobs.Length; i++)
            //    ChildNetworkObjects.Add(nobs[i]);

            PartialOnValidate();
        }
        partial void PartialOnValidate();
        private void Reset()
        {
            SerializeSceneTransformProperties();
            PartialReset();
        }
        partial void PartialReset();


        private void OnDrawGizmosSelected()
        {
            SerializeSceneTransformProperties();
        }

        /// <summary>
        /// Serializes SceneTransformProperties to current transform properties.
        /// </summary>
        private void SerializeSceneTransformProperties()
        {
            /* Use this method to set scene data since it doesn't need to exist outside 
            * the editor and because its updated regularly while selected. */
            //If a scene object.
            if (!EditorApplication.isPlaying && !string.IsNullOrEmpty(gameObject.scene.name))
            {
                SceneTransformProperties = new SceneTransformProperties(
                    transform.position, transform.rotation, transform.localScale);
            }
        }
#endif
        #endregion
    }

}

