using FishNet.Managing;
using FishNet.Connection;
using UnityEngine;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Managing.Logging;
using FishNet.Utility;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FishNet.Utility.Performance;
using System;
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
        /// True to synchronize the parent of this object during the spawn message.
        /// </summary>
        internal bool SynchronizeParent;
        /// <summary>
        /// Returns if this object was placed in the scene during edit-time.
        /// </summary>
        /// <returns></returns>
        public bool IsSceneObject => (SceneId > 0);
        [Obsolete("Use IsSceneObject instead.")] //Remove on 2023/01/01
        public bool SceneObject => IsSceneObject;

        /// <summary>
        /// Unique Id for this NetworkObject. This does not represent the object owner.
        /// </summary>
        public int ObjectId { get; private set; }
        /// <summary>
        /// True if this NetworkObject is deinitializing. Will also be true until Initialize is called. May be false until the object is cleaned up if object is destroyed without using Despawn.
        /// </summary>
        internal bool IsDeinitializing { get; private set; } = true;
        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
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
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        internal SceneTransformProperties SceneTransformProperties = new SceneTransformProperties();
        #endregion

        #region Serialized.
        /// <summary>
        /// True if the object will always initialize as a networked object. When false the object will not automatically initialize over the network. Using Spawn() on an object will always set that instance as networked.
        /// </summary>
        [Tooltip("True if the object will always initialize as a networked object. When false the object will not automatically initialize over the network. Using Spawn() on an object will always set that instance as networked.")]
        [SerializeField]
        private bool _isNetworked = true;
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
        /// <param name="isNetworked"></param>
        public void SetIsNetworked(bool isNetworked)
        {
            IsNetworked = isNetworked;
        }
        /// <summary>
        /// NetworkObjects which are children of this one.
        /// </summary>
        [SerializeField, HideInInspector]
        private bool _hasParentNetworkObjectAtEdit;

        /// <summary>
        /// Sets HasParentNetworkObjectAtEdit value.
        /// </summary>
        /// <param name="value"></param>
        internal void SetHasParentNetworkObjectAtEdit(bool value)
        {
            _hasParentNetworkObjectAtEdit = value;
        }
        #endregion

        private void Start()
        {
            if (!IsNetworked)
                return;
            /* Only the parent nob should try to deactivate.
             * If there is a parent nob then unset networked
             * and exit method. */
            if (_hasParentNetworkObjectAtEdit)
            {
                SetIsNetworked(false);
                return;
            }

            if (NetworkManager == null || (!NetworkManager.IsClient && !NetworkManager.IsServer))
            {
                //ActiveDuringEdit is only used for scene objects.
                if (IsSceneObject)
                    ActiveDuringEdit = true;
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            /* If deinitializing and an owner exist
             * then remove object from owner. */
            if (IsDeinitializing && Owner.IsValid)
                Owner.RemoveObject(this);
        }

        private void OnDestroy()
        {
            //Does this need to be here? I'm thinking no, remove it and examine later. //todo
            if (Owner.IsValid)
                Owner.RemoveObject(this);
            //Already being deinitialized by FishNet.
            if (IsDeinitializing)
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
            IsDeinitializing = true;

            SetActiveStatus(false, true);
            SetActiveStatus(false, false);
            //Don't need to reset sync types if object is being destroyed.
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
        /// Initializes this script. This is only called once even when as host.
        /// </summary>
        /// <param name="networkManager"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PreinitializeInternal(NetworkManager networkManager, int objectId, NetworkConnection owner, bool synchronizeParent, bool asServer)
        {
            IsDeinitializing = false;
            //QOL references.
            NetworkManager = networkManager;
            ServerManager = networkManager.ServerManager;
            ClientManager = networkManager.ClientManager;
            TransportManager = networkManager.TransportManager;
            TimeManager = networkManager.TimeManager;
            SceneManager = networkManager.SceneManager;
            RollbackManager = networkManager.RollbackManager;

            SynchronizeParent = synchronizeParent;
            SetOwner(owner);
            ObjectId = objectId;

            /* This must be called at the beginning
             * so that all conditions are handled by the observer
             * manager prior to the preinitialize call on networkobserver. 
             * The method called is dependent on NetworkManager being set. */
            AddDefaultNetworkObserverConditions();

            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InitializeOnceInternal();

            /* NetworkObserver uses some information from
             * NetworkBehaviour so it must be preinitialized
             * after NetworkBehaviours are. */
            if (asServer)
                InitializeOnceObservers();

            //Add to connection objects if owner exist.
            if (owner != null)
                owner.AddObject(this);
        }

        /// <summary>
        /// Updates NetworkBehaviours and initializes them with serialized values.
        /// </summary>
        internal void UpdateNetworkBehaviours()
        {
            //Go through each nob and set if it has a parent nob.
            NetworkObject[] nobs = GetComponentsInChildren<NetworkObject>(true);
            foreach (NetworkObject n in nobs)
                n.SetHasParentNetworkObjectAtEdit(n != this);

            NetworkBehaviours = GetComponentsInChildren<NetworkBehaviour>(true);
            //Check and initialize found network behaviours.
            if (NetworkBehaviours.Length > byte.MaxValue)
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Currently only {byte.MaxValue} NetworkBehaviour scripts per object are allowed. Object {gameObject.name} will not initialized.");
            }
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].SerializeComponents(this, (byte)i);
            }
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
                IsDeinitializing = true;
            }
            else
            {
                //Client only.
                if (!NetworkManager.IsServer)
                    IsDeinitializing = true;

                RemoveClientRpcLinkIndexes();
            }

            //if (SceneObject)
            //This needs to be done even if not scene object to support pooling.
            ResetSyncTypes(asServer);

            if (asServer)
                Observers.Clear();

            SetActiveStatus(false, asServer);
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
                if (activeNewOwner)
                    NetworkManager.ServerManager.Objects.RebuildObservers(this, newOwner);

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
                    RebuildObservers(prevOwner, false);
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
            SceneUpdateNetworkBehaviours();
            PartialOnValidate();
        }
        partial void PartialOnValidate();
        private void Reset()
        {
            SerializeSceneTransformProperties();
            SceneUpdateNetworkBehaviours();
            PartialReset();
        }
        partial void PartialReset();

        private void SceneUpdateNetworkBehaviours()
        {
            if (!string.IsNullOrEmpty(gameObject.scene.name))
                UpdateNetworkBehaviours();
        }

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

