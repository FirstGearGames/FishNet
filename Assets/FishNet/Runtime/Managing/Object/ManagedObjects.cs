using FishNet.Component.Observing;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        /// <summary>
        /// Subscribes to SceneManager.SceneLoaded event.
        /// </summary>
        /// <param name="subscribe"></param>
        internal void SubscribeToSceneLoaded(bool subscribe)
        {
            if (subscribe)
                SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            else
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

            RemoveFromSpawned(nob, true);
        }

        /// <summary>
        /// Removes a NetworkedObject from spawned.
        /// </summary>
        /// <param name="nob"></param>
        private void RemoveFromSpawned(NetworkObject nob, bool unexpectedlyDestroyed)
        {
            Spawned.Remove(nob.ObjectId);
            //Do the same with SceneObjects.
            if (unexpectedlyDestroyed && nob.IsSceneObject)
                RemoveFromSceneObjects(nob);
        }

        /// <summary>
        /// Despawns a NetworkObject.
        /// </summary>
        internal virtual void Despawn(NetworkObject nob, bool asServer)
        {
            if (nob == null)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Cannot despawn a null NetworkObject.");
                return;
            }

            //True if should be destroyed, false if deactivated.
            bool destroy;
            /* Only modify object state if asServer,
             * or !asServer and not host. This is so clients, when acting as
             * host, don't destroy objects they lost observation of. */
            //If as server.
            if (asServer)
            {
                //Scene object.
                if (nob.IsSceneObject)
                {
                    destroy = false;
                }
                //Not a scene object, destroy normally.
                else
                {
                    /* If client-host has visibility
                     * then disable and wait for client-host to get destroy
                     * message. Otherwise destroy immediately. */
                    if (nob.Observers.Contains(NetworkManager.ClientManager.Connection))
                    {
                        destroy = false;
                        NetworkManager.ServerManager.Objects.AddToPending(nob);
                    }
                    else
                    {
                        destroy = true;
                    }
                }
            }
            //Not as server.
            else
            {
                //Scene object.
                if (nob.IsSceneObject)
                {
                    destroy = false;
                }
                //Not a scene object, destroy normally.
                else
                {
                    /* If was removed from pending then also destroy.
                    * Pending objects are ones that exist on the server
                     * side only to await destruction from client side.
                     * Objects can also be destroyed if server is not
                     * active. */
                    destroy = (!NetworkManager.IsServer || NetworkManager.ServerManager.Objects.RemoveFromPending(nob.ObjectId));
                }
            }

            //Deinitialize to invoke callbacks.
            nob.Deinitialize(asServer);
            //Remove from match condition only if server.
            if (asServer)
                MatchCondition.RemoveFromMatchWithoutRebuild(nob, NetworkManager);
            //Remove from spawned collection.
            RemoveFromSpawned(nob, false);

            if (destroy)
            {
                MonoBehaviour.Destroy(nob.gameObject);
            }
            else
            {
                /* If running as client and is also server
                 * then see if server still has object spawned.
                 * If not, the object can be disabled, otherwise
                 * hide the renderers. */
                if (!asServer && NetworkManager.IsServer)
                {
                    //Still spawned.
                    if (NetworkManager.ServerManager.Objects.Spawned.ContainsKey(nob.ObjectId))
                        nob.SetHostVisibility(false);
                    //Not spawned.
                    else
                        nob.gameObject.SetActive(false);
                }
                //AsServer or not IsServer, can deactivate
                else
                {
                    nob.gameObject.SetActive(false);
                }
            }

        }


        /// <summary>
        /// Updates NetworkBehaviours on nob.
        /// </summary>
        /// <param name="asServer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdateNetworkBehaviours(NetworkObject nob, bool asServer)
        {
            //Would have already been done on server side.
            if (!asServer && NetworkManager.IsServer)
                return;

            InitializePrefab(nob, -1);
        }

        /// <summary>
        /// Initializes a prefab, not to be mistaken for initializing a spawned object.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="index"></param>
        public static void InitializePrefab(NetworkObject prefab, int index)
        {
            if (prefab == null)
                return;
            /* Only set the Id if not -1. 
             * A value of -1 would indicate it's a scene
             * object. */
            if (index != -1)
                prefab.SetPrefabId((short)index);
            prefab.UpdateNetworkBehaviours();
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
                if (nob.IsSceneObject)
                    nob.gameObject.SetActive(false);
                else
                    MonoBehaviour.Destroy(nob.gameObject);
            }
        }

        /// <summary>
        /// Adds a NetworkObject to Spawned.
        /// </summary>
        /// <param name="nob"></param>
        internal void AddToSpawned(NetworkObject nob, bool asServer)
        {
            Spawned[nob.ObjectId] = nob;

            //If being added as client and is also server.
            if (!asServer && NetworkManager.IsServer)
                nob.SetHostVisibility(true);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal NetworkObject GetSpawnedNetworkObject(int objectId)
        {
            NetworkObject r;
            if (!Spawned.TryGetValueIL2CPP(objectId, out r))
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
        protected internal void SkipDataLength(ushort packetId, PooledReader reader, int dataLength, int rpcLinkObjectId = -1)
        {
            /* -1 means length wasn't set, which would suggest a reliable packet.
            * Object should never be missing for reliable packets since spawns
            * and despawns are reliable in order. */
            if (dataLength == (int)MissingObjectPacketLength.Reliable)
            {
                string msg;
                bool isRpcLink = (packetId >= NetworkManager.StartingRpcLinkIndex);
                if (isRpcLink)
                {
                    msg = (rpcLinkObjectId == -1) ?
                        $"RPCLink of Id {(PacketId)packetId} could not be found. Remaining data will be purged." :
                        $"ObjectId {rpcLinkObjectId} for RPCLink {(PacketId)packetId} could not be found.";
                }
                else
                {
                    msg = $"NetworkBehaviour could not be found for packetId {(PacketId)packetId}. Remaining data will be purged.";
                }

                /* Default logging for server is errors only. Use error on client and warning
                 * on servers to reduce chances of allocation attacks. */
#if DEVELOPMENT_BUILD || UNITY_EDITOR || !UNITY_SERVER
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError(msg);
#else
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning(msg);
#endif
                reader.Skip(reader.Remaining);
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
            else if (dataLength == (int)MissingObjectPacketLength.PurgeRemaiming)
            {
                reader.Skip(reader.Remaining);
            }
        }

    }

}
