using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Performance;
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
        protected internal virtual int GetNextNetworkObjectId() { return -1; }
        /// <summary>
        /// NetworkManager handling this.
        /// </summary>
        protected NetworkManager NetworkManager = null;
        /// <summary>
        /// Objects in currently loaded scenes. These objects can be active or inactive.
        /// Key is the objectId while value is the object. Key is not the same as NetworkObject.ObjectId.
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
        protected internal virtual void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1) { }

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
        internal virtual void Despawn(NetworkObject nob, bool asServer)
        {
            if (nob == null)
            {
                if (NetworkManager.CanLog(LoggingType.Common))
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
                    /* If client-host has visibility
                     * then disable and wait for client-host to get destroy
                     * message. Otherwise destroy immediately. */
                    if (nob.Observers.Contains(NetworkManager.ClientManager.Connection))
                    {
                        nob.gameObject.SetActive(false);
                        NetworkManager.ServerManager.Objects.AddToPending(nob);
                    }
                    else
                    {
                        MonoBehaviour.Destroy(nob.gameObject);
                    }
                }
            }
            //Not as server.
            else
            {
                //Scene object.
                if (nob.SceneObject)
                {
                    //If also server don't set inactive again, server would have already done so.
                    if (!NetworkManager.IsServer)
                        nob.gameObject.SetActive(false);
                }
                //Not a scene object, destroy normally.
                else
                {
                    /* If was removed from pending then also destroy.
                    * Pending objects are ones that exist on the server
                     * side only to await destruction from client side.
                     * Objects can also be destroyed if server is not
                     * active. */
                    bool canDestroy = (!NetworkManager.IsServer || NetworkManager.ServerManager.Objects.RemoveFromPending(nob.ObjectId));
                    if (canDestroy)
                        MonoBehaviour.Destroy(nob.gameObject);
                }
            }

            Spawned.Remove(nob.ObjectId);
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
        protected internal void AddToSceneObjects(NetworkObject nob)
        {
            SceneObjects[nob.SceneId] = nob;
        }

        /// <summary>
        /// Removes a NetworkObject from SceneObjects.
        /// </summary>
        /// <param name="nob"></param>
        protected internal void RemoveFromSceneObjects(NetworkObject nob)
        {
            SceneObjects.Remove(nob.SceneId);
        }

        /// <summary>
        /// Finds a NetworkObject within Spawned.
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        protected internal NetworkObject GetSpawnedNetworkObject(int objectId)
        {
            NetworkObject r;
            if (!Spawned.TryGetValue(objectId, out r))
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Spawned NetworkObject not found for ObjectId {objectId}.");
            }

            return r;
        }

        /// <summary>
        /// Tries to skip data length for a packet.
        /// </summary>
        /// <param name="packetId"></param>
        /// <param name="reader"></param>
        /// <param name="dataLength"></param>
        protected internal void SkipDataLength(PacketId packetId, PooledReader reader, int dataLength)
        {
            /* -1 means length wasn't set, which would suggest a reliable packet.
            * Object should never be missing for reliable packets since spawns
            * and despawns are reliable in order. */
            if (dataLength == (int)UnreliablePacketLength.ReliableOrBroadcast)
            {
                /* Default logging for server is errors only. Use error on client and warning
                 * on servers to reduce chances of allocation attacks. */
#if DEVELOPMENT_BUILD || UNITY_EDITOR || !UNITY_SERVER
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"NetworkBehaviour could not be found for {packetId}.");
#else
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"NetworkBehaviour could not be found for {packetId}.");
#endif
            }
            /* If length is known then is unreliable packet. It's possible
             * this packetId arrived before or after the object was spawned/destroyed.
             * Skip past the data for this packet and use rest in reader. With non-linked
             * RPCs length is sent before object information. */
            else if (dataLength >= 0)
            {
                reader.Skip(Math.Min(dataLength, reader.Remaining));
            }
            /* -2 indicates the length is very long. Don't even try saving
             * the packet, user shouldn't be sending this much data over unreliable. */
            else if (dataLength == (int)UnreliablePacketLength.PurgeRemaiming)
            {
                reader.Skip(reader.Remaining);
            }
        }

    }

}
