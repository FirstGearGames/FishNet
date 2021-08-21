using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Object
{
    public abstract class ManagedObjects
    {
        #region Public.
        /// <summary>
        /// NetworkObjects which are currently active.
        /// </summary>
        public Dictionary<int, NetworkObject> Spawned = new Dictionary<int, NetworkObject>();
        #endregion

        #region Protected.
        /// <summary>
        /// Returns the next ObjectId to use.
        /// </summary>
        protected virtual int GetNextNetworkObjectId() { return -1; }
        /// <summary>
        /// NetworkManager handling this.
        /// </summary>
        protected NetworkManager NetworkManager = null;
        /// <summary>
        /// Objects in currently running scenes. These objects can be active or inactive.
        /// </summary>
        protected Dictionary<ulong, NetworkObject> SceneObjects = new Dictionary<ulong, NetworkObject>();
        #endregion

        public ManagedObjects()
        {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        ~ManagedObjects()
        {
            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
        }



        /// <summary>
        /// Called when a scene is loaded.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="arg1"></param>
        protected virtual void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1) { }

        /// <summary>
        /// Called when a NetworkObject runs Deactivate.
        /// </summary>
        /// <param name="nob"></param>
        internal virtual void NetworkObjectUnexpectedlyDestroyed(NetworkObject nob)
        {
            if (nob == null)
                return;
            /* Try to remove from Spawned even
             * if the NetworkObject may not be present.
             * Although this is very cheap, down the road
             * FishNet may check for initialization before
             * performing this action. */
            Spawned.Remove(nob.ObjectId);
            //Do the same with SceneObjects.
            if (nob.SceneObject)
                RemoveFromSceneObjects(nob);
        }

        /// <summary>
        /// Despawns a NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        internal virtual void Despawn(NetworkObject nob, bool asServer)
        {
            if (nob == null)
            {
                Debug.Log($"Cannot despawn a null NetworkObject.");
                return;
            }

            nob.Deinitialize(asServer);

            /* Only modify object state if asServer,
             * or !asServer and not host. This is so clients, when acting as
             * host, don't destroy objects they lost observation of. */
            if (asServer || (!asServer && !NetworkManager.IsHost))
            {
                //Scene object.
                if (nob.SceneObject)
                    nob.gameObject.SetActive(false);
                //Not a scene object, destroy normally.
                else
                    MonoBehaviour.Destroy(nob.gameObject);
            }

            Spawned.Remove(nob.ObjectId);
            //Do the same with SceneObjects.
            if (nob.SceneObject)
                RemoveFromSceneObjects(nob);
        }
        /// <summary>
        /// Despawns Spawned NetworkObjects. Scene objects will be disabled, others will be destroyed.
        /// </summary>
        protected virtual void DespawnSpawnedWithoutSynchronization(bool asServer)
        {
            foreach (NetworkObject nob in Spawned.Values)
                DespawnWithoutSynchronization(nob, asServer);

            Spawned.Clear();
        }

        /// <summary>
        /// Despawns a network object.
        /// </summary>
        /// <param name="nob"></param>
        protected virtual void DespawnWithoutSynchronization(NetworkObject nob, bool asServer)
        {
            //Null can occur when running as host and server already despawns such as wehen stopping.
            if (nob == null)
                return;

            nob.Deinitialize(asServer);

            /* Only run if asServer, or not 
             * asServer and server isn't running. This
             * prevents objects from affecting the server
             * as host* when being modified client side. */
            if (asServer || (!asServer && !NetworkManager.IsServer))
            {
                if (nob.SceneObject)
                    nob.gameObject.SetActive(false);
                else
                    MonoBehaviour.Destroy(nob.gameObject);
            }
        }

        /// <summary>
        /// Adds a NetworkObject to Spawned.
        /// </summary>
        /// <param name="nob"></param>
        protected void AddToSpawned(NetworkObject nob)
        {
            Spawned[nob.ObjectId] = nob;
        }

        /// <summary>
        /// Adds a NetworkObject to SceneObjects.
        /// </summary>
        /// <param name="nob"></param>
        protected void AddToSceneObjects(NetworkObject nob)
        {
            SceneObjects[nob.SceneId] = nob;
        }

        /// <summary>
        /// Removes a NetworkObject from SceneObjects.
        /// </summary>
        /// <param name="nob"></param>
        protected void RemoveFromSceneObjects(NetworkObject nob)
        {
            SceneObjects.Remove(nob.SceneId);
        }

        /// <summary>
        /// Finds a NetworkObject within Spawned.
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        protected NetworkObject GetSpawnedNetworkObject(int objectId)
        {
            NetworkObject r;
            if (!Spawned.TryGetValue(objectId, out r))
                Debug.LogError($"Spawned NetworkObject not found for ObjectId {objectId}.");

            return r;
        }

        /// <summary>
        /// Finds a NetworkBehaviour within Spawned.
        /// </summary>
        /// <param name="objectId"></param>
        /// <param name="componentIndex"></param>
        /// <returns></returns>
        protected NetworkBehaviour GetSpawnedNetworkBehaviour(int objectId, byte componentIndex)
        {
            NetworkObject nob = GetSpawnedNetworkObject(objectId);
            if (nob == null)
                return null;

            //Component index is out of range.
            if (nob.NetworkBehaviours.Length <= componentIndex)
            {
                Debug.LogError($"Spawned Component index of {componentIndex} is out of range for ObjectId {objectId}.");
                return null;
            }

            return nob.NetworkBehaviours[componentIndex];
        }

    }

}
