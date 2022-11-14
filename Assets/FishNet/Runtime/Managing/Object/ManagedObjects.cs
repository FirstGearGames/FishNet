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
        /// Removes a NetworkedObject from spawned.
        /// </summary>
        /// <param name="nob"></param>
        private void RemoveFromSpawned(int objectId, bool unexpectedlyDestroyed, ulong sceneId)
        {
            Spawned.Remove(objectId);
            //Do the same with SceneObjects.
            if (unexpectedlyDestroyed && (sceneId != 0))
                RemoveFromSceneObjects(sceneId);
        }

        /// <summary>
        /// Despawns a NetworkObject.
        /// </summary>
        internal virtual void Despawn(NetworkObject nob, DespawnType despawnType, bool asServer)
        {
            if (nob == null)
            {
                NetworkManager.LogWarning($"Cannot despawn a null NetworkObject.");
                return;
            }

            //True if should be destroyed, false if deactivated.
            bool destroy = false;
            /* Only modify object state if asServer,
             * or !asServer and not host. This is so clients, when acting as
             * host, don't destroy objects they lost observation of. */

            /* Nested prefabs can never be destroyed. Only check to 
             * destroy if not nested. By nested prefab, this means the object
             * despawning is part of another prefab that is also a spawned
             * network object. */
            if (!nob.IsNested)
            {
                //If as server.
                if (asServer)
                {
                    //Scene object.
                    if (!nob.IsSceneObject)
                    {
                        /* If client-host has visibility
                         * then disable and wait for client-host to get destroy
                         * message. Otherwise destroy immediately. */
                        if (nob.Observers.Contains(NetworkManager.ClientManager.Connection))
                            NetworkManager.ServerManager.Objects.AddToPending(nob);
                        else
                            destroy = true;
                    }
                }
                //Not as server.
                else
                {
                    bool isServer = NetworkManager.IsServer;
                    //Only check to destroy if not a scene object.
                    if (!nob.IsSceneObject)
                    {
                        /* If was removed from pending then also destroy.
                        * Pending objects are ones that exist on the server
                        * side only to await destruction from client side.
                        * Objects can also be destroyed if server is not
                        * active. */
                        destroy = (!isServer || NetworkManager.ServerManager.Objects.RemoveFromPending(nob.ObjectId));
                    }
                }
            }

            //Deinitialize to invoke callbacks.
            nob.Deinitialize(asServer);
            //Remove from match condition only if server.
            if (asServer)
                MatchCondition.RemoveFromMatchWithoutRebuild(nob, NetworkManager);
            RemoveFromSpawned(nob, false);

            //If to destroy.
            if (destroy)
            {
                if (despawnType == DespawnType.Destroy)
                    MonoBehaviour.Destroy(nob.gameObject);
                else
                    NetworkManager.StorePooledInstantiated(nob, nob.PrefabId, asServer);
            }
            /* If to potentially disable instead of destroy.
             * This is such as something is despawning server side
             * but a clientHost is present, or if a scene object. */
            else
            {
                //If as server.
                if (asServer)
                {
                    //If not clientHost then the object can be disabled.
                    if (!NetworkManager.IsClient)
                        nob.gameObject.SetActive(false);
                }
                //Not as server.
                else
                {
                    //If the server is not active then the object can be disabled.
                    if (!NetworkManager.IsServer)
                    {
                        nob.gameObject.SetActive(false);
                    }
                    //If also server then checks must be done.
                    else
                    {
                        /* Object is still spawned on the server side. This means
                         * the clientHost likely lost visibility. When this is the case
                         * update clientHost renderers. */
                        if (NetworkManager.ServerManager.Objects.Spawned.ContainsKey(nob.ObjectId))
                            nob.SetRenderersVisible(false);
                        /* No longer spawned on the server, can
                         * deactivate on the client. */
                        else
                            nob.gameObject.SetActive(false);
                    }
                }

                /* Also despawn child objects.
                 * This only must be done when not destroying
                 * as destroying would result in the despawn being
                 * forced. 
                 *
                 * Only run if asServer as well. The server will send
                 * individual despawns for each child. */
                if (asServer)
                {
                    foreach (NetworkObject childNob in nob.ChildNetworkObjects)
                    {
                        if (childNob != null && !childNob.IsDeinitializing)
                            Despawn(childNob, despawnType, asServer);
                    }
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
        /// <param name="prefab">Prefab to initialize.</param>
        /// <param name="index">Index within spawnable prefabs.</param>
        public static void InitializePrefab(NetworkObject prefab, int index)
        {
            if (prefab == null)
                return;
            /* Only set the Id if not -1. 
             * A value of -1 would indicate it's a scene
             * object. */
            if (index != -1)
                prefab.PrefabId = (short)index;

            byte componentIndex = 0;
            prefab.UpdateNetworkBehaviours(null, ref componentIndex);
        }

        /// <summary>
        /// Despawns Spawned NetworkObjects. Scene objects will be disabled, others will be destroyed.
        /// </summary>
        internal virtual void DespawnWithoutSynchronization(bool asServer)
        {
            foreach (NetworkObject nob in Spawned.Values)
                DespawnWithoutSynchronization(nob, asServer, nob.GetDefaultDespawnType(), false);

            Spawned.Clear();
        }

        /// <summary>
        /// Despawns a network object.
        /// </summary>
        /// <param name="nob"></param>
        internal virtual void DespawnWithoutSynchronization(NetworkObject nob, bool asServer, DespawnType despawnType, bool removeFromSpawned)
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
                if (removeFromSpawned)
                    RemoveFromSpawned(nob, false);
                if (nob.IsSceneObject)
                {
                    nob.gameObject.SetActive(false);
                }
                else
                {
                    if (despawnType == DespawnType.Destroy)
                        MonoBehaviour.Destroy(nob.gameObject);
                    else
                        NetworkManager.StorePooledInstantiated(nob, nob.PrefabId, asServer);
                }
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
                nob.SetRenderersVisible(true);
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
        /// Removes a NetworkObject from SceneObjects.
        /// </summary>
        /// <param name="nob"></param>
        protected internal void RemoveFromSceneObjects(ulong sceneId)
        {
            SceneObjects.Remove(sceneId);
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
