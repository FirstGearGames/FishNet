using FishNet.Managing;
using FishNet.Connection;
using UnityEngine;
using FishNet.Serializing;
using FishNet.Transporting;

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
        /// True if this NetworkObject was active during edit. Will be true if placed in scene during edit, and was in active state on run.
        /// </summary>
        internal bool ActiveDuringEdit = false;
        /// <summary>
        /// Returns if this object was placed in the scene during edit-time.
        /// </summary>
        /// <returns></returns>
        public bool SceneObject => (SceneId > 0);
        /// <summary>
        /// Owner of this object. Will be null if there is no owner. Owner is only visible to server.
        /// </summary>
        public NetworkConnection Owner { get; private set; } = null;
        /// <summary>
        /// Unique Id for this NetworkObject. This does not represent the object owner. See Connection for owner information.
        /// </summary>
        public int ObjectId { get; private set; }
        /// <summary>
        /// True if this NetworkObject is deinitializing. Will also be true until Initialize is called. May be false until the object is cleaned up if object is destroyed without using Despawn.
        /// </summary>
        public bool Deinitializing { get; private set; } = true;
        /// <summary>
        /// NetworkBehaviours within the root and children of this object.
        /// </summary>
        public NetworkBehaviour[] NetworkBehaviours { get; private set; } = null;
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

        private void Start()
        {
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
            if (Deinitializing && OwnerIsValid)
                Owner.RemoveObject(this);
        }

        private void OnDestroy()
        {
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
            if (OwnerIsValid)
                Owner.RemoveObject(this);

            Deinitializing = true;
        }

        /// <summary>
        /// PreInitializes this script.
        /// </summary>
        /// <param name="networkManager"></param>
        internal void PreInitialize(NetworkManager networkManager, int objectId, NetworkConnection owner, bool asServer)
        {
            Deinitializing = false;
            NetworkManager = networkManager;
            Owner = owner;
            ObjectId = objectId;

            //Add to connection objects if owner exist.
            if (owner != null)
            {
                owner.AddObject(this);
                //See if I can just call GiveOwnership instead. Will need to check ownership callback events to make sure they dont call twice and call in order. //todo
                if (asServer)
                    NetworkManager.SceneManager.AddConnectionToScene(owner, gameObject.scene);
            }

            if (asServer)
                PreInitializeObservers();

            if (NetworkBehaviours == null || NetworkBehaviours.Length == 0)
            {
                NetworkBehaviours = GetComponentsInChildren<NetworkBehaviour>();
                if (NetworkBehaviours.Length > byte.MaxValue)
                {
                    Debug.LogError($"Currently only 256 NetworkBehaviour scripts per object are allowed. Object {gameObject.name} will not initialized.");
                }
                else
                {
                    for (int i = 0; i < NetworkBehaviours.Length; i++)
                        NetworkBehaviours[i].PreInitialize(this, (byte)i);
                }
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

            if (asServer || (!asServer && !NetworkManager.IsServer))
                Deinitializing = true;
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
        internal void GiveOwnership(NetworkConnection newOwner, bool asServer)
        {
            if (asServer && !NetworkManager.IsServer)
            {
                Debug.LogWarning($"Ownership cannot be given for object {gameObject.name}. Only server may give ownership.");
                return;
            }

            //If the same owner don't bother sending a message, just ignore request.
            if (newOwner == Owner)
                return;

            NetworkConnection prevOwner = Owner;
            Owner = newOwner;
            //After changing owners invoke callbacks.
            InvokeOwnership(Owner, asServer);

            //If asServer send updates to clients as needed.
            if (asServer)
            {
                //Rebuild for new owner first so they get change messages.
                if (newOwner != null && newOwner.IsValid)
                {
                    newOwner.AddObject(this);
                    NetworkManager.SceneManager.AddConnectionToScene(newOwner, gameObject.scene);
                    RebuildObservers(newOwner);
                }

                using (PooledWriter writer = WriterPool.GetWriter())
                {
                    writer.WriteByte((byte)PacketId.OwnershipChange);
                    writer.WriteNetworkObject(this);
                    writer.WriteNetworkConnection(Owner);
                    //If sharing then send to all observers.
                    if (NetworkManager.ServerManager.ShareOwners)
                    {
                        NetworkManager.TransportManager.SendToClients((byte)Channel.Reliable, writer.GetArraySegment(), this);
                    }
                    //Only sending to old / new.
                    else
                    {
                        if (prevOwner != null)
                            NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, writer.GetArraySegment(), prevOwner);
                        if (newOwner != null)
                            NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, writer.GetArraySegment(), newOwner);
                    }
                }

                //Rebuild for old owner last so they also get change messages.
                if (prevOwner != null)
                {
                    prevOwner.RemoveObject(this);
                    RebuildObservers(prevOwner);
                }

            }

        }

        /// <summary>
        /// Returns if this NetworkObject is a scene object, and has changed.
        /// </summary>
        /// <returns></returns>
        public ChangedTransformProperties GetChangedSceneTransformProperties()
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
        protected virtual void Reset()
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

