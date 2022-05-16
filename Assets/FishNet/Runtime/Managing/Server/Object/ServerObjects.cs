using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Server
{
    /// <summary>
    /// Handles objects and information about objects for the server. See ManagedObjects for inherited options.
    /// </summary>
    public partial class ServerObjects : ManagedObjects
    {
        #region Public.
        /// <summary>
        /// Called right before client objects are destroyed when a client disconnects.
        /// </summary>
        public event Action<NetworkConnection> OnPreDestroyClientObjects;
        #endregion

        #region Private.
        /// <summary>
        /// Cached ObjectIds which may be used when exceeding available ObjectIds.
        /// </summary>
        private Queue<int> _objectIdCache = new Queue<int>();
        /// <summary>
        /// NetworkBehaviours which have dirty SyncVars.
        /// </summary>
        private List<NetworkBehaviour> _dirtySyncVarBehaviours = new List<NetworkBehaviour>(20);
        /// <summary>
        /// NetworkBehaviours which have dirty SyncObjects.
        /// </summary>
        private List<NetworkBehaviour> _dirtySyncObjectBehaviours = new List<NetworkBehaviour>(20);
        /// <summary>
        /// Objects which need to be destroyed next tick.
        /// This is needed when running as host so host client will get any final messages for the object before they're destroyed.
        /// </summary>
        private Dictionary<int, NetworkObject> _pendingDestroy = new Dictionary<int, NetworkObject>();
        /// <summary>
        /// Scenes which were loaded that need to be setup.
        /// </summary>
        private List<(int, Scene)> _loadedScenes = new List<(int frame, Scene scene)>();
        #endregion

        internal ServerObjects(NetworkManager networkManager)
        {
            base.NetworkManager = networkManager;
            networkManager.SceneManager.OnActiveSceneSetInternal += SceneManager_OnActiveSceneSet;
            networkManager.TimeManager.OnUpdate += TimeManager_OnUpdate;
        }

        /// <summary>
        /// Called when MonoBehaviours call Update.
        /// </summary>
        private void TimeManager_OnUpdate()
        {
            IterateLoadedScenes(false);
            PartialOnUpdate();
        }
        partial void PartialOnUpdate();

        #region Checking dirty SyncTypes.
        /// <summary>
        /// Iterates NetworkBehaviours with dirty SyncTypes.
        /// </summary>
        internal void WriteDirtySyncTypes()
        {
            /* Tells networkbehaviours to check their
             * dirty synctypes. */
            IterateCollection(_dirtySyncVarBehaviours, false);
            IterateCollection(_dirtySyncObjectBehaviours, true);

            void IterateCollection(List<NetworkBehaviour> collection, bool isSyncObject)
            {
                for (int i = 0; i < collection.Count; i++)
                {
                    bool dirtyCleared = collection[i].WriteDirtySyncTypes(isSyncObject);
                    if (dirtyCleared)
                    {
                        collection.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
        /// <summary>
        /// Sets that a NetworkBehaviour has a dirty syncVars.
        /// </summary>
        /// <param name="nb"></param>
        internal void SetDirtySyncType(NetworkBehaviour nb, bool isSyncObject)
        {
            if (isSyncObject)
                _dirtySyncObjectBehaviours.Add(nb);
            else
                _dirtySyncVarBehaviours.Add(nb);
        }
        #endregion

        #region Connection Handling.
        /// <summary>
        /// Called when the connection state changes for the local server.
        /// </summary>
        /// <param name="args"></param>
        internal void OnServerConnectionState(ServerConnectionStateArgs args)
        {

            //If server just connected.
            if (args.ConnectionState == LocalConnectionStates.Started)
            {
                /* If there's no servers started besides the one
                 * that just started then build Ids and setup scene objects. */
                if (base.NetworkManager.ServerManager.OneServerStarted())
                {
                    BuildObjectIdCache();
                    SetupSceneObjects();
                }
            }
            //Server in anything but started state.
            else
            {
                //If no servers are started then reset.
                if (!base.NetworkManager.ServerManager.AnyServerStarted())
                {
                    base.DespawnSpawnedWithoutSynchronization(true);
                    base.SceneObjects.Clear();
                    _objectIdCache.Clear();
                    base.NetworkManager.ServerManager.Clients.Clear();
                }
            }
        }

        /// <summary>
        /// Called when a client disconnects.
        /// </summary>
        /// <param name="connection"></param>
        internal void ClientDisconnected(NetworkConnection connection)
        {
            RemoveFromObserversWithoutSynchronization(connection);

            OnPreDestroyClientObjects?.Invoke(connection);

            /* A cache is made because the Objects
             * collection would end up modified during
             * iteration from removing ownership and despawning. */
            ListCache<NetworkObject> cache = ListCaches.GetNetworkObjectCache();
            foreach (NetworkObject nob in connection.Objects)
                cache.AddValue(nob);

            int written = cache.Written;
            List<NetworkObject> collection = cache.Collection;
            for (int i = 0; i < written; i++)
                collection[i].Despawn();

            ListCaches.StoreCache(cache);
        }
        #endregion

        #region ObjectIds.
        /// <summary>
        /// Builds the ObjectId cache with all possible Ids.
        /// </summary>
        private void BuildObjectIdCache()
        {
            _objectIdCache.Clear();

            /* Shuffle Ids to make it more difficult
             * for clients to track spawned object
             * count. */
            List<int> shuffledCache = new List<int>();
            for (int i = 0; i < short.MaxValue; i++)
                shuffledCache.Add(i);
            /* Only shuffle when NOT in editor and not
             * development build.
             * Debugging could be easier when Ids are ordered. */
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            shuffledCache.Shuffle();
#endif
            //Add shuffled to objectIdCache.
            //Build Id cache.
            int cacheCount = shuffledCache.Count;
            for (int i = 0; i < cacheCount; i++)
                _objectIdCache.Enqueue(shuffledCache[i]);
        }
        /// <summary>
        /// Caches a NetworkObject ObjectId.
        /// </summary>
        /// <param name="nob"></param>
        private void CacheObjectId(NetworkObject nob)
        {
            if (nob.ObjectId >= 0)
                _objectIdCache.Enqueue(nob.ObjectId);
        }

        /// <summary>
        /// Gets the next ObjectId to use for NetworkObjects.
        /// </summary>
        /// <returns></returns>
        protected internal override int GetNextNetworkObjectId()
        {
            //Either something went wrong or user actually managed to spawn ~32K networked objects.
            if (_objectIdCache.Count == 0)
            {
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"No more available ObjectIds. How the heck did you manage to have {short.MaxValue} objects spawned at once?");
                return -1;
            }
            else
            {
                return _objectIdCache.Dequeue();
            }
        }
        #endregion

        #region Initializing Objects In Scenes.
        /// <summary>
        /// Called after the active scene has been scene, immediately after scene loads.
        /// </summary>
        private void SceneManager_OnActiveSceneSet()
        {
            IterateLoadedScenes(true);
        }
        /// <summary>
        /// Iterates loaded scenes and sets them up.
        /// </summary>
        /// <param name="ignoreFrameRestriction">True to ignore the frame restriction when iterating.</param>
        internal void IterateLoadedScenes(bool ignoreFrameRestriction)
        {
            //Not started, clear loaded scenes.
            if (!NetworkManager.ServerManager.Started)
                _loadedScenes.Clear();

            for (int i = 0; i < _loadedScenes.Count; i++)
            {
                (int frame, Scene scene) value = _loadedScenes[i];
                if (ignoreFrameRestriction || (Time.frameCount > value.frame))
                {
                    SetupSceneObjects(value.scene);
                    _loadedScenes.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// Called when a scene loads on the server.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="arg1"></param>
        protected internal override void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1)
        {
            base.SceneManager_sceneLoaded(s, arg1);

            if (!NetworkManager.ServerManager.Started)
                return;
            //Add to loaded scenes so that they are setup next frame.
            _loadedScenes.Add((Time.frameCount, s));
        }

        /// <summary>
        /// Setup all NetworkObjects in scenes. Should only be called when server is active.
        /// </summary>
        protected internal void SetupSceneObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                SetupSceneObjects(SceneManager.GetSceneAt(i));

            Scene ddolScene = DDOLFinder.GetDDOL().gameObject.scene;
            if (ddolScene.isLoaded)
                SetupSceneObjects(ddolScene);
        }

        /// <summary>
        /// Setup NetworkObjects in a scene. Should only be called when server is active.
        /// </summary>
        /// <param name="s"></param>
        private void SetupSceneObjects(Scene s)
        {
            ListCache<NetworkObject> nobs;
            SceneFN.GetSceneNetworkObjects(s, true, out nobs);
            bool isHost = base.NetworkManager.IsHost;

            for (int i = 0; i < nobs.Written; i++)
            {
                NetworkObject nob = nobs.Collection[i];
                //Only setup if a scene object and not initialzied.
                if (nob.IsNetworked && nob.IsSceneObject && nob.IsDeinitializing)
                {
                    base.UpdateNetworkBehaviours(nob, true);
                    base.AddToSceneObjects(nob);
                    /* If was active in the editor (before hitting play), or currently active
                     * then PreInitialize without synchronizing to clients. There is no reason
                     * to synchronize to clients because the scene just loaded on server,
                     * which means clients are not yet in the scene. */
                    if (nob.ActiveDuringEdit || nob.gameObject.activeInHierarchy)
                    {
                        //If not host then object doesn't need to be spawned until a client joins.
                        if (!isHost)
                            SetupWithoutSynchronization(nob);
                        //Otherwise spawn object so observers update for clientHost.
                        else
                            SpawnWithoutChecks(nob);
                    }
                }
            }

            ListCaches.StoreCache(nobs);
        }

        /// <summary>
        /// Performs setup on a NetworkObject without synchronizing the actions to clients.
        /// </summary>
        private void SetupWithoutSynchronization(NetworkObject nob, NetworkConnection ownerConnection = null, bool synchronizeParent = true)
        {
            if (nob.IsNetworked)
            {
                int objectId = GetNextNetworkObjectId();
                nob.PreinitializeInternal(NetworkManager, objectId, ownerConnection, synchronizeParent, true);
                base.AddToSpawned(nob, true);
                nob.gameObject.SetActive(true);
                nob.Initialize(true);
            }
        }
        #endregion

        #region Spawning.
        /// <summary>
        /// Spawns an object over the network.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Spawn(NetworkObject networkObject, NetworkConnection ownerConnection = null, bool synchronizeParent = true)
        {
            if (!NetworkManager.ServerManager.Started)
            {
                if (base.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning("Cannot spawn object because the server is not active.");
                return;
            }
            if (networkObject == null)
            {
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Specified networkObject is null.");
                return;
            }
            if (!networkObject.gameObject.scene.IsValid())
            {
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"{networkObject.name} is a prefab. You must instantiate the prefab first, then use Spawn on the instantiated copy.");
                return;
            }
            if (ownerConnection != null && ownerConnection.IsActive && !ownerConnection.LoadedStartScenes)
            {
                if (base.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"{networkObject.name} was spawned but it's recommended to not spawn objects for connections until they have loaded start scenes. You can be notified when a connection loads start scenes by using connection.OnLoadedStartScenes on the connection, or SceneManager.OnClientLoadStartScenes.");
            }
            if (networkObject.IsSpawned)
            {
                if (base.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"{networkObject.name} is already spawned.");
                return;
            }

            SpawnWithoutChecks(networkObject, ownerConnection, synchronizeParent);
        }

        /// <summary>
        /// Spawns networkObject without any checks.
        /// </summary>
        private void SpawnWithoutChecks(NetworkObject networkObject, NetworkConnection ownerConnection = null, bool synchronizeParent = true)
        {
            /* Setup locally without sending to clients.
            * When observers are built for the network object
            * during initialization spawn messages will
            * be sent. */
            networkObject.SetIsNetworked(true);
            SetupWithoutSynchronization(networkObject, ownerConnection, synchronizeParent);
            //Also rebuild observers for the object so it spawns for others.
            RebuildObservers(networkObject);

            /* If also client then we need to make sure the object renderers have correct visibility.
             * Set visibility based on if the observers contains the clientHost connection. */
            if (NetworkManager.IsClient)
                networkObject.SetHostVisibility(networkObject.Observers.Contains(NetworkManager.ClientManager.Connection));
        }

        /// <summary>
        /// Writes a spawn into writers.
        /// </summary>
        /// <param name="nob"></param>
        /// <param name="connection">Connection spawn is being written for.</param>
        /// <param name="everyoneWriter"></param>
        /// <param name="ownerWriter"></param>
        private void WriteSpawn(NetworkObject nob, NetworkConnection connection, ref PooledWriter everyoneWriter, ref PooledWriter ownerWriter)
        {
            /* Using a number of writers to prevent rebuilding the
             * packets excessively for values that are owner only
             * vs values that are everyone. To save performance the
             * owner writer is only written to if owner is valid.
             * This makes the code a little uglier but will scale
             * significantly better with more connections.
             * 
             * EG:
             * with this technique networkBehaviours are iterated
             * twice if there is an owner; once for data to send to everyone
             * and again for data only going to owner. 
             *
             * The alternative would be to iterate the networkbehaviours
             * for every connection it's going to and filling a single
             * writer with values based on if owner or not. This would
             * result in significantly more iterations. */
            PooledWriter headerWriter = WriterPool.GetWriter();
            headerWriter.WritePacketId(PacketId.ObjectSpawn);
            headerWriter.WriteNetworkObject(nob);
            if (base.NetworkManager.ServerManager.ShareIds || connection == nob.Owner)
                headerWriter.WriteNetworkConnection(nob.Owner);
            else
                headerWriter.WriteInt16(-1);

            /* Write if a scene object or not, and also
             * store sceneObjectId if is a scene object. */
            bool sceneObject = nob.IsSceneObject;
            headerWriter.WriteBoolean(sceneObject);
            /* Writing a scene object. */
            if (sceneObject)
            {
                //Write Guid.
                headerWriter.WriteUInt64(nob.SceneId, AutoPackType.Unpacked);
                //Write changed properties.
                ChangedTransformProperties ctp = nob.GetChangedSceneTransformProperties();
                headerWriter.WriteByte((byte)ctp);
                //If properties have changed.
                if (ctp != ChangedTransformProperties.Unset)
                {
                    //Write any changed properties.
                    if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.Position))
                        headerWriter.WriteVector3(nob.transform.position);
                    if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.Rotation))
                        headerWriter.WriteQuaternion(nob.transform.rotation, base.NetworkManager.ServerManager.SpawnPacking.Rotation);
                    if (Enums.TransformPropertiesContains(ctp, ChangedTransformProperties.LocalScale))
                        headerWriter.WriteVector3(nob.transform.localScale);
                }
            }
            /* Writing a spawned object. */
            else
            {
                //Check to write parent behaviour or nob.
                NetworkBehaviour parentNb = null;
                Transform t = nob.transform.parent;
                if (t != null)
                {
                    parentNb = t.GetComponent<NetworkBehaviour>();
                    /* Check for a NetworkObject if there is no NetworkBehaviour.
                     * There is a small chance the parent object will only contain
                     * a NetworkObject. */
                    if (parentNb == null)
                    {
                        //If null check if there is a nob.
                        NetworkObject parentNob = t.GetComponent<NetworkObject>();
                        //ParentNob is null or not spawned.
                        if (!ParentIsSpawned(parentNob))
                        { 
                            headerWriter.WriteByte((byte)SpawnParentType.Unset);
                        }
                        else
                        { 
                            headerWriter.WriteByte((byte)SpawnParentType.NetworkObject);
                            headerWriter.WriteNetworkObject(parentNob);
                        }
                    }
                    //NetworkBehaviour found on parent.
                    else
                    {
                        //ParentNb is null or not spawned.
                        if (!ParentIsSpawned(parentNb.NetworkObject))
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.Unset);
                        }
                        else
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.NetworkBehaviour);
                            headerWriter.WriteNetworkBehaviour(parentNb);
                        }
                    }

                    //True if pNob is not null, and is spawned.
                    bool ParentIsSpawned(NetworkObject pNob)
                    {
                        bool isNull = (pNob == null);
                        if (isNull || !pNob.IsSpawned)
                        {
                            /* Only log if pNob exist. Otherwise this would print if the user 
                             * was parenting any object, which may not be desirable as they could be
                             * simply doing it for organization reasons. */
                            if (!isNull && base.NetworkManager.CanLog(LoggingType.Warning))
                                Debug.LogWarning($"Parent {t.name} is not spawned. {nob.name} will not have it's parent sent in the spawn message.");
                            return false;
                        }

                        return true;
                    }

                }
                //No parent.
                else
                {
                    headerWriter.WriteByte((byte)SpawnParentType.Unset);
                }


                headerWriter.WriteInt16(nob.PrefabId);
                /* //muchlater Write only properties that are different
                 * from the prefab. Odds are position will be changed,
                 * and possibly rotation, but not too likely scale. */
                headerWriter.WriteVector3(nob.transform.position);
                headerWriter.WriteQuaternion(nob.transform.rotation, base.NetworkManager.ServerManager.SpawnPacking.Rotation);
                headerWriter.WriteVector3(nob.transform.localScale);
            }

            //Write headers first.
            everyoneWriter.WriteBytes(headerWriter.GetBuffer(), 0, headerWriter.Length);
            if (nob.Owner.IsValid)
                ownerWriter.WriteBytes(headerWriter.GetBuffer(), 0, headerWriter.Length);

            /* Used to write latest data which must be sent to
             * clients, such as SyncTypes and RpcLinks. */
            PooledWriter tempWriter = WriterPool.GetWriter();
            //Send RpcLinks first.
            foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                nb.WriteRpcLinks(tempWriter);
            //Add to everyone/owner.
            everyoneWriter.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);
            if (nob.Owner.IsValid)
                ownerWriter.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);

            //Add most recent sync type values.
            /* SyncTypes have to be populated for owner and everyone.
            * The data may be unique for owner if synctypes are set
            * to only go to owner. */
            tempWriter.Reset();
            foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                nb.WriteSyncTypesForSpawn(tempWriter, false);
            everyoneWriter.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);
            //If owner is valid then populate owner writer as well.
            if (nob.Owner.IsValid)
            {
                tempWriter.Reset();
                foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                    nb.WriteSyncTypesForSpawn(tempWriter, true);
                ownerWriter.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);
            }

            //Dispose of writers created in this method.
            headerWriter.Dispose();
            tempWriter.Dispose();
        }
        #endregion

        #region Despawning.
        internal void AddToPending(NetworkObject nob)
        {
            _pendingDestroy[nob.ObjectId] = nob;
        }
        /// <summary>
        /// Tries to removes objectId from PendingDestroy and returns if successful.
        /// </summary>
        /// <param name="objectId"></param>
        internal bool RemoveFromPending(int objectId)
        {
            return _pendingDestroy.Remove(objectId);
        }
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

        /// <summary>
        /// Despawns an object over the network.
        /// </summary>
        internal override void Despawn(NetworkObject nob, bool asServer)
        {
            if (nob.CanSpawnOrDespawn(true))
            {
                FinalizeDespawn(nob);
                base.Despawn(nob, true);
            }
        }

        /// <summary>
        /// Called when a NetworkObject is destroyed without being deactivated first.
        /// </summary>
        /// <param name="nob"></param>
        internal override void NetworkObjectUnexpectedlyDestroyed(NetworkObject nob)
        {
            FinalizeDespawn(nob);
            base.NetworkObjectUnexpectedlyDestroyed(nob);
        }

        /// <summary>
        /// Finalizes the despawn process. By the time this is called the object is considered unaccessible.
        /// </summary>
        /// <param name="nob"></param>
        private void FinalizeDespawn(NetworkObject nob)
        {
            if (nob != null && nob.ObjectId != -1)
            {
                nob.WriteDirtySyncTypes();
                WriteDespawnAndSend(nob);
                CacheObjectId(nob);
            }
        }

        /// <summary>
        /// Writes a despawn and sends it to clients.
        /// </summary>
        /// <param name="nob"></param>
        private void WriteDespawnAndSend(NetworkObject nob)
        {
            PooledWriter everyoneWriter = WriterPool.GetWriter();
            WriteDespawn(nob, ref everyoneWriter);

            ArraySegment<byte> despawnSegment = everyoneWriter.GetArraySegment();

            //Add observers to a list cache.
            ListCache<NetworkConnection> cache = ListCaches.GetNetworkConnectionCache();
            cache.Reset();
            cache.AddValues(nob.Observers);
            int written = cache.Written;
            for (int i = 0; i < written; i++)
            {
                //Invoke ondespawn and send despawn.
                NetworkConnection conn = cache.Collection[i];
                nob.InvokeOnServerDespawn(conn);
                NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, despawnSegment, conn);
                //Remove from observers.
                //nob.Observers.Remove(conn);
            }

            everyoneWriter.Dispose();
            ListCaches.StoreCache(cache);
        }
        /// <summary>
        /// Writes a despawn.
        /// </summary>
        /// <param name="nob"></param>
        private void WriteDespawn(NetworkObject nob, ref PooledWriter everyoneWriter)
        {
            everyoneWriter.WritePacketId(PacketId.ObjectDespawn);
            everyoneWriter.WriteNetworkObject(nob);
        }



    }
    #endregion



}
