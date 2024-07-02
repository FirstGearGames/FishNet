#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
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
        #region Types.
        /// <summary>
        /// Information about a predicted spawner when receiving a spawn from clients.
        /// </summary>
        private struct PredictedSpawnData
        {
            public PooledReader PayloadReader;
            public NetworkConnection PredictedSpawner;

            public PredictedSpawnData(PooledReader payloadReader, NetworkConnection predictedSpawner)
            {
                PayloadReader = payloadReader;
                PredictedSpawner = predictedSpawner;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Called right before client objects are destroyed when a client disconnects.
        /// </summary>
        public event Action<NetworkConnection> OnPreDestroyClientObjects;
        #endregion

        #region Internal.
        /// <summary>
        /// Collection of NetworkObjects recently despawned.
        /// Key: objectId.
        /// Value: despawn tick.
        /// This is used primarily to track if client is sending messages for recently despawned objects.
        /// Objects are automatically removed after RECENTLY_DESPAWNED_DURATION seconds.
        /// </summary>
        internal Dictionary<int, uint> RecentlyDespawnedIds = new Dictionary<int, uint>();
        #endregion

        #region Private.
        /// <summary>
        /// Cached ObjectIds which may be used when exceeding available ObjectIds.
        /// </summary>
        private Queue<int> _objectIdCache = new Queue<int>();
        /// <summary>
        /// Returns the ObjectId cache.
        /// </summary>
        /// <returns></returns>
        internal Queue<int> GetObjectIdCache() => _objectIdCache;
        /// <summary>
        /// NetworkBehaviours which have dirty SyncObjects.
        /// </summary>
        private List<NetworkBehaviour> _dirtySyncTypeBehaviours = new List<NetworkBehaviour>(20);
        /// <summary>
        /// Objects which need to be destroyed next tick.
        /// This is needed when running as host so host client will get any final messages for the object before they're destroyed.
        /// </summary>
        private Dictionary<int, NetworkObject> _pendingDestroy = new Dictionary<int, NetworkObject>();
        /// <summary>
        /// Scenes which were loaded that need to be setup.
        /// </summary>
        private List<(int, Scene)> _loadedScenes = new List<(int frame, Scene scene)>();
        /// <summary>
        /// Cache of spawning objects, used for recursively spawning nested NetworkObjects.
        /// </summary>
        private List<NetworkObject> _spawnCache = new List<NetworkObject>();
        /// <summary>
        /// True if one or more scenes are currently loading through the SceneManager.
        /// </summary>
        private bool _scenesLoading;
        /// <summary>
        /// Number of ticks which must pass to clear a recently despawned.
        /// </summary>
        private uint _cleanRecentlyDespawnedMaxTicks => base.NetworkManager.TimeManager.TimeToTicks(30d, TickRounding.RoundUp);
        #endregion

        internal ServerObjects(NetworkManager networkManager)
        {
            base.Initialize(networkManager);
            networkManager.SceneManager.OnLoadStart += SceneManager_OnLoadStart;
            networkManager.SceneManager.OnActiveSceneSetInternal += SceneManager_OnActiveSceneSet;
            networkManager.TimeManager.OnUpdate += TimeManager_OnUpdate;
        }

        /// <summary>
        /// Called when MonoBehaviours call Update.
        /// </summary>
        private void TimeManager_OnUpdate()
        {
            if (!base.NetworkManager.IsServerStarted)
            {
                _scenesLoading = false;
                _loadedScenes.Clear();
                return;
            }

            CleanRecentlyDespawned();

            if (!_scenesLoading)
                IterateLoadedScenes(false);
            Observers_OnUpdate();
        }

        #region Checking dirty SyncTypes.
        /// <summary>
        /// Iterates NetworkBehaviours with dirty SyncTypes.
        /// </summary>
        internal void WriteDirtySyncTypes()
        {
            List<NetworkBehaviour> collection = _dirtySyncTypeBehaviours;
            /* Tells networkbehaviours to check their
             * dirty synctypes. */
            for (int i = 0; i < collection.Count; i++)
            {
                bool dirtyCleared = collection[i].WriteDirtySyncTypes();
                if (dirtyCleared)
                {
                    collection.RemoveAt(i);
                    i--;
                }
            }
        }
        /// <summary>
        /// Sets that a NetworkBehaviour has a dirty syncVars.
        /// </summary>
        /// <param name="nb"></param>
        internal void SetDirtySyncType(NetworkBehaviour nb)
        {
            _dirtySyncTypeBehaviours.Add(nb);
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
            if (args.ConnectionState == LocalConnectionState.Started)
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
                    base.DespawnWithoutSynchronization(true);
                    base.SceneObjects_Internal.Clear();
                    _objectIdCache.Clear();
                    base.NetworkManager.ClearClientsCollection(base.NetworkManager.ServerManager.Clients);
                }
                //If at least one server is started then only clear for disconnecting server.
                else
                {
                    //Remove connections only for transportIndex.
                    base.NetworkManager.ClearClientsCollection(base.NetworkManager.ServerManager.Clients, args.TransportIndex);
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
            List<NetworkObject> nobs = CollectionCaches<NetworkObject>.RetrieveList();
            foreach (NetworkObject nob in connection.Objects)
                nobs.Add(nob);

            int nobsCount = nobs.Count;
            for (int i = 0; i < nobsCount; i++)
            {
                /* Objects may already be deinitializing when a client disconnects
                 * because the root object could have been despawned first, and in result
                 * all child objects would have been recursively despawned. 
                 * 
                 * EG: object is:
                 *      A (nob)
                 *          B (nob)
                 * 
                 * Both A and B are owned by the client so they will both be
                 * in collection. Should A despawn first B will recursively despawn
                 * from it. Then once that finishes and the next index of collection
                 * is run, which would B, the object B would have already been deinitialized. */
                if (!nobs[i].IsDeinitializing)
                    base.NetworkManager.ServerManager.Despawn(nobs[i]);
            }

            CollectionCaches<NetworkObject>.Store(nobs);
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
            //Ignore ushort.maxvalue as that indicates null.
            for (int i = 0; i < (ushort.MaxValue - 1); i++)
                shuffledCache.Add(i);
            /* Only shuffle when NOT in editor and not
             * development build.
             * Debugging could be easier when Ids are ordered. */
#if !DEVELOPMENT
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CacheObjectId(NetworkObject nob)
        {
            if (nob.ObjectId != NetworkObject.UNSET_OBJECTID_VALUE)
                CacheObjectId(nob.ObjectId);
        }
        /// <summary>
        /// Adds an ObjectId to objectId cache.
        /// </summary>
        /// <param name="id"></param>
        internal void CacheObjectId(int id)
        {
            _objectIdCache.Enqueue(id);
        }

        /// <summary>
        /// Gets the next ObjectId to use for NetworkObjects.
        /// </summary>
        /// <returns></returns>
        protected internal override int GetNextNetworkObjectId(bool errorCheck = true)
        {
            //Either something went wrong or user actually managed to spawn ~64K networked objects.
            if (_objectIdCache.Count == 0)
            {
                base.NetworkManager.LogError($"No more available ObjectIds. How the heck did you manage to have {ushort.MaxValue} objects spawned at once?");
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
        /// Called when a scene load starts.
        /// </summary>
        private void SceneManager_OnLoadStart(Scened.SceneLoadStartEventArgs obj)
        {
            _scenesLoading = true;
        }
        /// <summary>
        /// Called after the active scene has been scene, immediately after scene loads.
        /// </summary>
        private void SceneManager_OnActiveSceneSet()
        {
            _scenesLoading = false;
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

            Scene ddolScene = DDOL.GetDDOL().gameObject.scene;
            if (ddolScene.isLoaded)
                SetupSceneObjects(ddolScene);
        }

        /// <summary>
        /// Setup NetworkObjects in a scene. Should only be called when server is active.
        /// </summary>
        /// <param name="s"></param>
        private void SetupSceneObjects(Scene s)
        {
            if (!s.IsValid())
                return;

            List<NetworkObject> sceneNobs = CollectionCaches<NetworkObject>.RetrieveList();
            Scenes.GetSceneNetworkObjects(s, false, true, true, ref sceneNobs);

            //Sort the nobs based on initialization order.
            bool initializationOrderChanged = false;
            List<NetworkObject> cache = CollectionCaches<NetworkObject>.RetrieveList();
            foreach (NetworkObject item in sceneNobs)
                OrderRootByInitializationOrder(item, cache, ref initializationOrderChanged);
            OrderNestedByInitializationOrder(cache);
            //Store sceneNobs.
            CollectionCaches<NetworkObject>.Store(sceneNobs);

            bool isHost = base.NetworkManager.IsHostStarted;
            int nobsCount = cache.Count;
            for (int i = 0; i < nobsCount; i++)
            {
                NetworkObject nob = cache[i];
                //Only setup if a scene object and not initialzied.
                if (nob.IsNetworked && nob.IsSceneObject && nob.IsDeinitializing)
                {
                    base.UpdateNetworkBehavioursForSceneObject(nob, true);
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

            CollectionCaches<NetworkObject>.Store(cache);
        }

        /// <summary>
        /// Performs setup on a NetworkObject without synchronizing the actions to clients.
        /// </summary>
        /// <param name="objectId">Override ObjectId to use.</param>
        private void SetupWithoutSynchronization(NetworkObject nob, NetworkConnection ownerConnection = null, int? objectId = null, PredictedSpawnData? predictedSpawnData = null)
        {
            if (nob.IsNetworked)
            {
                if (objectId == null)
                    objectId = GetNextNetworkObjectId();
                nob.Preinitialize_Internal(NetworkManager, objectId.Value, ownerConnection, true);
                if (predictedSpawnData.HasValue)
                    base.ReadPayload(predictedSpawnData.Value.PredictedSpawner, nob, predictedSpawnData.Value.PayloadReader);

                base.AddToSpawned(nob, true);
                nob.gameObject.SetActive(true);
                nob.Initialize(true, true);
            }
        }
        #endregion

        #region Spawning.
        /// <summary>
        /// Spawns an object over the network.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Spawn(NetworkObject networkObject, NetworkConnection ownerConnection = null, UnityEngine.SceneManagement.Scene scene = default)
        {
            //Default as false, will change if needed.
            bool predictedSpawn = false;

            if (networkObject == null)
            {
                base.NetworkManager.LogError($"Specified networkObject is null.");
                return;
            }
            if (!NetworkManager.ServerManager.Started)
            {
                //Neither server nor client are started.
                if (!NetworkManager.ClientManager.Started)
                {
                    base.NetworkManager.LogWarning("Cannot spawn object because server nor client are active.");
                    return;
                }
                //Server has predicted spawning disabled.
                if (!NetworkManager.ServerManager.GetAllowPredictedSpawning())
                {
                    base.NetworkManager.LogWarning("Cannot spawn object because server is not active and predicted spawning is not enabled.");
                    return;
                }
                //Various predicted spawn checks.
                if (!base.CanPredictedSpawn(networkObject, NetworkManager.ClientManager.Connection, ownerConnection, false))
                    return;

                //Since server is not started then run TrySpawn for client, given this is a client trying to predicted spawn.
                if (!networkObject.PredictedSpawn.OnTrySpawnClient())
                    return;

                predictedSpawn = true;
            }
            if (!networkObject.gameObject.scene.IsValid())
            {
                base.NetworkManager.LogError($"{networkObject.name} is a prefab. You must instantiate the prefab first, then use Spawn on the instantiated copy.");
                return;
            }
            if (ownerConnection != null && ownerConnection.IsActive && !ownerConnection.LoadedStartScenes(!predictedSpawn))
            {
                base.NetworkManager.LogWarning($"{networkObject.name} was spawned but it's recommended to not spawn objects for connections until they have loaded start scenes. You can be notified when a connection loads start scenes by using connection.OnLoadedStartScenes on the connection, or SceneManager.OnClientLoadStartScenes.");
            }
            if (networkObject.IsSpawned)
            {
                base.NetworkManager.LogWarning($"{networkObject.name} is already spawned.");
                return;
            }
            NetworkBehaviour networkBehaviourParent = networkObject.CurrentParentNetworkBehaviour;
            if (networkBehaviourParent != null && !networkBehaviourParent.IsSpawned)
            {
                base.NetworkManager.LogError($"{networkObject.name} cannot be spawned because it has a parent NetworkObject {networkBehaviourParent} which is not spawned.");
                return;
            }
            /* If scene is specified make sure the object is root,
             * and if not move it before network spawning. */
            if (scene.IsValid())
            {
                if (networkObject.transform.parent != null)
                {
                    base.NetworkManager.LogError($"{networkObject.name} cannot be moved to scene name {scene.name}, handle {scene.handle} because {networkObject.name} is not root and only root objects may be moved.");
                    return;
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(networkObject.gameObject, scene);
                }
            }

            if (predictedSpawn)
                base.NetworkManager.ClientManager.Objects.PredictedSpawn(networkObject, ownerConnection);
            else
                SpawnWithoutChecks(networkObject, ownerConnection);
        }

        /// <summary>
        /// Spawns networkObject without any checks.
        /// </summary>
        private void SpawnWithoutChecks(NetworkObject networkObject, NetworkConnection ownerConnection = null, int? objectId = null, PredictedSpawnData? predictedSpawnData = null)
        {
            /* Setup locally without sending to clients.
            * When observers are built for the network object
            * during initialization spawn messages will
            * be sent. */
            networkObject.SetIsNetworked(true);
            _spawnCache.Add(networkObject);
            SetupWithoutSynchronization(networkObject, ownerConnection, objectId, predictedSpawnData);

            foreach (NetworkObject item in networkObject.NestedRootNetworkBehaviours)
            {
                /* Only spawn recursively if the nob state is unset.
                 * Unset indicates that the nob has not been */
                if (item.gameObject.activeInHierarchy || item.State == NetworkObjectState.Spawned)
                    SpawnWithoutChecks(item, ownerConnection, null, predictedSpawnData);
            }

            /* Copy to a new cache then reset _spawnCache
             * just incase rebuilding observers would lead to 
             * more additions into _spawnCache. EG: rebuilding
             * may result in additional objects being spawned
             * for clients and if _spawnCache were not reset
             * the same objects would be rebuilt again. This likely
             * would not affect anything other than perf but who
             * wants that. */
            List<NetworkObject> spawnCacheCopy = CollectionCaches<NetworkObject>.RetrieveList();
            spawnCacheCopy.AddRange(_spawnCache);
            _spawnCache.Clear();
            //Also rebuild observers for the object so it spawns for others.
            RebuildObservers(spawnCacheCopy);

            int spawnCacheCopyCount = spawnCacheCopy.Count;
            /* If also client then we need to make sure the object renderers have correct visibility.
             * Set visibility based on if the observers contains the clientHost connection. */
            if (NetworkManager.IsClientStarted)
            {
                int count = spawnCacheCopyCount;
                for (int i = 0; i < count; i++)
                    spawnCacheCopy[i].SetRenderersVisible(networkObject.Observers.Contains(NetworkManager.ClientManager.Connection));
            }

            CollectionCaches<NetworkObject>.Store(spawnCacheCopy);
        }

        /// <summary>
        /// Reads a predicted spawn.
        /// </summary>
        internal void ReadPredictedSpawn(PooledReader reader, NetworkConnection conn)
        {
            //TODO: parentChange. If has parent then read local space, otherwise world.
            /* 
             * //TODO this is crucial. Length must be sent with the predicted spawn
             * to skip past it if some reason we cannot continue.
             * For example, if the sceneId is not found there may be content in the reader
             * we cannot process, such as reading initial payload or syncvars.
             * When this does happen just skip the remaining amount
             * on the reader according to the length here. (not yet written/read) */

            sbyte initializeOrder;
            ushort collectionId;
            int prefabId;
            int objectId = reader.ReadNetworkObjectForSpawn(out initializeOrder, out collectionId, out _);
            //If objectId is not within predicted ids for conn.
            if (!conn.PredictedObjectIds.Contains(objectId))
            {
                reader.Clear();
                conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {conn.ClientId} used predicted spawning with a non-reserved objectId of {objectId}.");
                return;
            }

            NetworkConnection owner = reader.ReadNetworkConnection();
            SpawnType st = (SpawnType)reader.ReadUInt8Unpacked();
            //Not used at the moment.
            byte componentIndex = reader.ReadUInt8Unpacked();
            //Read transform values which differ from serialized values.
            base.ReadTransformProperties(reader, out Vector3? nullablePosition, out Quaternion? nullableRotation, out Vector3? nullableScale);

            NetworkObject nob;
            bool isGlobal = false;
            if (SpawnTypeEnum.FastContains(st, SpawnType.Scene))
            {
                ulong sceneId = reader.ReadUInt64Unpacked();
#if DEVELOPMENT
                string sceneName = string.Empty;
                string objectName = string.Empty;
                CheckReadSceneObjectDetails(reader, ref sceneName, ref objectName);
                nob = base.GetSceneNetworkObject(sceneId, sceneName, objectName);
#else
                nob = base.GetSceneNetworkObject(sceneId);
#endif
                if (nob != null)
                {
                    //TODO skip remaining here if cannot predicted spawn.
                    if (!base.CanPredictedSpawn(nob, conn, owner, true))
                        return;

                    nob.transform.SetLocalPositionRotationAndScale(nullablePosition, nullableRotation, nullableScale);
                }
                else
                {
                    //TODO skip remaining here if cannot predicted spawn.
                    return;
                }
            }
            else
            {
                /* Read past SpawnParentType and set to unset.
                 * PredictedSpawning does not support parenting in spawnsd
                 * at this time. */
                reader.ReadUInt8Unpacked();
                //SpawnParentType spt = reader.ReadUInt8();
                bool hasParent = false;
                //bool hasParent = (spt != SpawnParentType.Unset);

                prefabId = reader.ReadNetworkObjectId();
                //Invalid prefabId.
                if (prefabId == NetworkObject.UNSET_PREFABID_VALUE)
                {
                    reader.Clear();
                    conn.Kick(KickReason.UnusualActivity, LoggingType.Common, $"Spawned object has an invalid prefabId of {prefabId}. Make sure all objects which are being spawned over the network are within SpawnableObjects on the NetworkManager. Connection {conn.ClientId} will be kicked immediately.");
                    return;
                }

                PrefabObjects prefabObjects = NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);
                //PrefabObjects not found.
                if (prefabObjects == null)
                {
                    reader.Clear();
                    conn.Kick(KickReason.UnusualActivity, LoggingType.Common, $"PrefabObjects collection is not found for CollectionId {collectionId}. Be sure to add your addressables NetworkObject prefabs to the collection on server and client before attempting to spawn them over the network. Connection {conn.ClientId} will be kicked immediately.");
                    return;
                }
                //Check if prefab allows predicted spawning.
                NetworkObject nPrefab = prefabObjects.GetObject(true, prefabId);
                if (!base.CanPredictedSpawn(nPrefab, conn, owner, true))
                    return;

                ObjectPoolRetrieveOption retrieveOptions = ObjectPoolRetrieveOption.MakeActive;
                if (hasParent)
                    retrieveOptions |= ObjectPoolRetrieveOption.LocalSpace;

                nob = NetworkManager.GetPooledInstantiated(prefabId, collectionId, retrieveOptions, parent: null, nullablePosition, nullableRotation, nullableScale, asServer: true);
                isGlobal = SpawnTypeEnum.FastContains(st, SpawnType.InstantiatedGlobal);
            }

            nob.SetIsGlobal(isGlobal);
            //Initialize for prediction.
            nob.InitializePredictedObject_Server(base.NetworkManager, conn);
            //Read payload.

            PooledReader payloadReader = ReaderPool.Retrieve(reader.ReadArraySegmentAndSize(), base.NetworkManager, Reader.DataSource.Client);
            PredictedSpawnData predictedSpawnData = new PredictedSpawnData(payloadReader, conn);
            SpawnWithoutChecks(nob, owner, objectId, predictedSpawnData);
            payloadReader.Store();

            //Send the spawner a new reservedId.
            WriteResponse(true);
            //Writes a predicted spawn result to a client.
            void WriteResponse(bool success)
            {
                PooledWriter writer = WriterPool.Retrieve();
                writer.WritePacketIdUnpacked(PacketId.PredictedSpawnResult);
                writer.WriteNetworkObjectId(nob.ObjectId);
                writer.WriteBoolean(success);

                if (success)
                {
                    Queue<int> objectIdCache = NetworkManager.ServerManager.Objects.GetObjectIdCache();
                    //Write next objectId to use.
                    int invalidId = NetworkObject.UNSET_OBJECTID_VALUE;
                    int nextId = (objectIdCache.Count > 0) ? objectIdCache.Dequeue() : invalidId;
                    writer.WriteNetworkObjectId(nextId);
                    //If nextId is valid then also add it to spawners local cache.
                    if (nextId != invalidId)
                        conn.PredictedObjectIds.Enqueue(nextId);
                    ////Update RPC links.
                    //foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                    //    nb.WriteRpcLinks(writer);
                }

                conn.SendToClient((byte)Channel.Reliable, writer.GetArraySegment());
            }

        }
        #endregion

        #region Despawning.
        /// <summary>
        /// Cleans recently despawned objects.
        /// </summary>
        private void CleanRecentlyDespawned()
        {
            //Only iterate if frame ticked to save perf.
            if (!base.NetworkManager.TimeManager.FrameTicked)
                return;

            List<int> intCache = CollectionCaches<int>.RetrieveList();

            uint requiredTicks = _cleanRecentlyDespawnedMaxTicks;
            uint currentTick = base.NetworkManager.TimeManager.LocalTick;
            //Iterate 20, or 5% of the collection, whichever is higher.
            int iterations = Mathf.Max(20, (int)(RecentlyDespawnedIds.Count * 0.05f));
            /* Given this is a dictionary there is no gaurantee which order objects are
             * added. Because of this it's possible some objects may take much longer to
             * be removed. This is okay so long as a consistent chunk of objects are removed
             * at a time; eventually all objects will be iterated. */
            int count = 0;
            foreach (KeyValuePair<int, uint> kvp in RecentlyDespawnedIds)
            {
                long result = (currentTick - kvp.Value);
                //If enough ticks have passed to remove.
                if (result > requiredTicks)
                    intCache.Add(kvp.Key);

                count++;
                if (count == iterations)
                    break;
            }

            //Remove cached entries.
            int cCount = intCache.Count;
            for (int i = 0; i < cCount; i++)
                RecentlyDespawnedIds.Remove(intCache[i]);

            CollectionCaches<int>.Store(intCache);
        }

        /// <summary>
        /// Returns if an objectId was recently despawned.
        /// </summary>
        /// <param name="objectId">ObjectId to check.</param>
        /// <param name="ticks">Passed ticks to be within to be considered recently despawned.</param>
        /// <returns>True if an objectId was despawned with specified number of ticks.</returns>
        public bool RecentlyDespawned(int objectId, uint ticks)
        {
            uint despawnTick;
            if (!RecentlyDespawnedIds.TryGetValue(objectId, out despawnTick))
                return false;

            return ((NetworkManager.TimeManager.LocalTick - despawnTick) <= ticks);
        }
        /// <summary>
        /// Adds to objects pending destroy due to clientHost environment.
        /// </summary>
        /// <param name="nob"></param>
        internal void AddToPending(NetworkObject nob)
        {
            _pendingDestroy[nob.ObjectId] = nob;
        }
        /// <summary>
        /// Tries to removes objectId from PendingDestroy and returns if successful.
        /// </summary>
        internal bool RemoveFromPending(int objectId)
        {
            return _pendingDestroy.Remove(objectId);
        }
        /// <summary>
        /// Returns a NetworkObject in PendingDestroy.
        /// </summary>
        internal NetworkObject GetFromPending(int objectId)
        {
            NetworkObject nob;
            _pendingDestroy.TryGetValue(objectId, out nob);
            return nob;
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
        internal override void Despawn(NetworkObject networkObject, DespawnType despawnType, bool asServer)
        {
            //Default as false, will change if needed.
            bool predictedDespawn = false;

            if (networkObject == null)
            {
                base.NetworkManager.LogWarning($"NetworkObject cannot be despawned because it is null.");
                return;
            }
            if (networkObject.IsDeinitializing)
            {
                base.NetworkManager.LogWarning($"Object {networkObject.name} cannot be despawned because it is already deinitializing.");
                return;
            }

            if (!NetworkManager.ServerManager.Started)
            {
                //Neither server nor client are started.
                if (!NetworkManager.ClientManager.Started)
                {
                    base.NetworkManager.LogWarning("Cannot despawn object because server nor client are active.");
                    return;
                }
                //Server has predicted spawning disabled.
                if (!NetworkManager.ServerManager.GetAllowPredictedSpawning())
                {
                    base.NetworkManager.LogWarning("Cannot despawn object because server is not active and predicted spawning is not enabled.");
                    return;
                }
                //Various predicted despawn checks.
                if (!base.CanPredictedDespawn(networkObject, NetworkManager.ClientManager.Connection, false))
                    return;

                predictedDespawn = true;
            }
            if (!networkObject.gameObject.scene.IsValid())
            {
                base.NetworkManager.LogError($"{networkObject.name} is a prefab. You must instantiate the prefab first, then use Spawn on the instantiated copy.");
                return;
            }

            if (predictedDespawn)
            {
                base.NetworkManager.ClientManager.Objects.PredictedDespawn(networkObject);
            }
            else
            {
                FinalizeDespawn(networkObject, despawnType);
                RecentlyDespawnedIds[networkObject.ObjectId] = base.NetworkManager.TimeManager.LocalTick;
                base.Despawn(networkObject, despawnType, asServer);
            }
        }

        /// <summary>
        /// Called when a NetworkObject is destroyed without being deactivated first.
        /// </summary>
        /// <param name="nob"></param>
        internal override void NetworkObjectUnexpectedlyDestroyed(NetworkObject nob, bool asServer)
        {
            FinalizeDespawn(nob, DespawnType.Destroy);
            base.NetworkObjectUnexpectedlyDestroyed(nob, asServer);
        }

        /// <summary>
        /// Finalizes the despawn process. By the time this is called the object is considered unaccessible.
        /// </summary>
        /// <param name="nob"></param>
        private void FinalizeDespawn(NetworkObject nob, DespawnType despawnType)
        {
            List<NetworkBehaviour> dirtiedNbs = _dirtySyncTypeBehaviours;
            if (nob != null && nob.ObjectId != NetworkObject.UNSET_OBJECTID_VALUE)
            {
                // Write out any pending sync types and be sure to clear from the dirty list
                // to avoid trying to write out a despawned object later on.
                for (int i = 0, count = nob.NetworkBehaviours.Length; i < count; ++i)
                {
                    NetworkBehaviour nb = nob.NetworkBehaviours[i];
                    if (nb.SyncTypeDirty && nb.WriteDirtySyncTypes(true, true))
                        _dirtySyncTypeBehaviours.Remove(nb);
                }

                WriteDespawnAndSend(nob, despawnType);
                CacheObjectId(nob);
            }
        }

        /// <summary>
        /// Writes a despawn and sends it to clients.
        /// </summary>
        /// <param name="nob"></param>
        private void WriteDespawnAndSend(NetworkObject nob, DespawnType despawnType)
        {
            PooledWriter everyoneWriter = WriterPool.Retrieve();
            WriteDespawn(nob, despawnType, everyoneWriter);

            ArraySegment<byte> despawnSegment = everyoneWriter.GetArraySegment();

            //Add observers to a list cache.
            List<NetworkConnection> cache = CollectionCaches<NetworkConnection>.RetrieveList();
            cache.AddRange(nob.Observers);
            int cacheCount = cache.Count;
            for (int i = 0; i < cacheCount; i++)
            {
                //Invoke ondespawn and send despawn.
                NetworkConnection conn = cache[i];
                nob.InvokeOnServerDespawn(conn);
                NetworkManager.TransportManager.SendToClient((byte)Channel.Reliable, despawnSegment, conn);
                //Remove from observers.
                //nob.Observers.Remove(conn);
            }

            everyoneWriter.Store();
            CollectionCaches<NetworkConnection>.Store(cache);
        }
        /// <summary>
        /// Reads a predicted despawn.
        /// </summary>
        internal void ReadPredictedDespawn(Reader reader, NetworkConnection conn)
        {
            NetworkObject nob = reader.ReadNetworkObject();

            //Maybe server destroyed the object so don't kick if null.
            if (nob == null)
                return;

            //Various predicted despawn checks.
            if (!base.CanPredictedDespawn(nob, conn, true))
                return;

            //Despawn object.
            nob.Despawn();
        }
        #endregion
    }


}
