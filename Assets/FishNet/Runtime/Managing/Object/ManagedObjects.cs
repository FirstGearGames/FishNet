using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
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

        #region Private.
        /// <summary>
        /// Objects which need to be destroyed next tick.
        /// This is needed when running as host so host client will get any final messages for the object before they're destroyed.
        /// </summary>
        private Dictionary<int, NetworkObject> _pendingDestroy = new Dictionary<int, NetworkObject>();
        #endregion

        /// <summary>
        /// Destroys NetworkObjects pending for destruction.
        /// </summary>
        internal void DestroyPending()
        {
            foreach (NetworkObject item in _pendingDestroy.Values)
            {
                if (item != null)
                    MonoBehaviour.Destroy(item.gameObject);
            }

            _pendingDestroy.Clear();
        }


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
                if (NetworkManager.CanLog(Logging.LoggingType.Common))
                    Debug.Log($"Cannot despawn a null NetworkObject.");
                return;
            }

            nob.Deinitialize(asServer);

            /* Only modify object state if asServer,
             * or !asServer and not host. This is so clients, when acting as
             * host, don't destroy objects they lost observation of. */
            //If as server.
            if (asServer)
            {
                //Scene object.
                if (nob.SceneObject)
                {
                    nob.gameObject.SetActive(false);
                }
                //Not a scene object, destroy normally.
                else
                {
                    //If not host destroy object.
                    if (!NetworkManager.IsHost)
                        MonoBehaviour.Destroy(nob.gameObject);
                    else
                    {
                        nob.gameObject.SetActive(false);
                        _pendingDestroy[nob.ObjectId] = nob;
                    }
                }
            }
            //Not as server.
            else
            {
                //If not hosts.
                if (!NetworkManager.IsHost)
                {
                    //Scene object.
                    if (nob.SceneObject)
                        nob.gameObject.SetActive(false);
                    //Not a scene object, destroy normally.
                    else
                        MonoBehaviour.Destroy(nob.gameObject);
                }
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
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Error))
                    Debug.LogError($"Spawned NetworkObject not found for ObjectId {objectId}.");
            }

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
                if (NetworkManager.CanLog(Logging.LoggingType.Error))
                    Debug.LogError($"Spawned Component index of {componentIndex} is out of range for ObjectId {objectId}.");
                return null;
            }

            return nob.NetworkBehaviours[componentIndex];
        }

        /// <summary>
        /// Tries to skip data length for a packet.
        /// </summary>
        /// <param name="packetId"></param>
        /// <param name="reader"></param>
        /// <param name="dataLength"></param>
        protected void SkipDataLength(PacketId packetId, PooledReader reader, int startPosition, int dataLength)
        {
            /* -1 means length wasn't set, which would suggest a reliable packet.
            * Object should never be missing for reliable packets since spawns
            * and despawns are reliable in order. */
            if (dataLength == -1)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"NetworkBehaviour could not be found for {packetId}.");
            }
            /* If length is known then is unreliable packet. It's possible
             * this packetId arrived before or after the object was spawned/destroyed.
             * Skip past the data for this packet and use rest in reader. */
            else if (dataLength > 0)
            {
                reader.Position = startPosition;
                reader.Skip(Math.Min(dataLength, reader.Remaining));
            }
            /* -2 indicates the length is very long. Don't even try saving
             * the packet, user shouldn't be sending this much data over unreliable. */
            else if (dataLength == -2)
            {
                reader.Skip(reader.Remaining);
            }
        }

    }

}
