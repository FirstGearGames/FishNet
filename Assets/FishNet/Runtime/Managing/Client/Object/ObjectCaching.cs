using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Object.Helping;
using FishNet.Serializing;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Managing.Client
{

    /// <summary>
    /// Information about cached network objects.
    /// </summary>
    internal class ClientObjectCache
    {
        #region Types.
        public enum CacheSearchType
        {
            Any = 0,
            Spawning = 1,
            Despawning = 2
        }
        #endregion

        #region Internal.
        /// <summary>
        /// Objets which are being spawned during iteration.
        /// </summary>
        internal Dictionary<int, NetworkObject> SpawningObjects = new Dictionary<int, NetworkObject>();
        #endregion

        #region Private.
        /// <summary>
        /// Cached objects buffer. Contains spawns and despawns.
        /// </summary>
        private ListCache<CachedNetworkObject> _cachedObjects = new ListCache<CachedNetworkObject>();
        /// <summary>
        /// NetworkObjects which have been spawned already during the current iteration.
        /// </summary>
        private HashSet<NetworkObject> _iteratedSpawns = new HashSet<NetworkObject>();
        /// <summary>
        /// Despawns which are occurring the same tick as their spawn.
        /// </summary>
        private HashSet<int> _conflictingDespawns = new HashSet<int>();
        /// <summary>
        /// ClientObjects reference.
        /// </summary>
        private ClientObjects _clientObjects;
        /// <summary>
        /// NetworkManager for this cache.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// True if logged the warning about despawning on the same tick as the spawn.
        /// This exist to prevent excessive spam of the warning.
        /// </summary>
        private bool _loggedSameTickWarning;
        #endregion

        public ClientObjectCache(ClientObjects cobs, NetworkManager networkManager)
        {
            _clientObjects = cobs;
            _networkManager = networkManager;
        }

        /// <summary>
        /// Returns a NetworkObject found in spawned cache using objectId.
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        public NetworkObject GetInCached(int objectId, CacheSearchType searchType)
        {
            int count = _cachedObjects.Written;
            List<CachedNetworkObject> collection = _cachedObjects.Collection;
            for (int i = 0; i < count; i++)
            {
                CachedNetworkObject cnob = collection[i];
                if (cnob.ObjectId == objectId)
                {
                    //Any condition always returns.
                    if (searchType == CacheSearchType.Any)
                        return cnob.NetworkObject;

                    bool spawning = (searchType == CacheSearchType.Spawning);
                    bool spawnAction = (cnob.Action == CachedNetworkObject.ActionType.Spawn);
                    if (spawning == spawnAction)
                        return cnob.NetworkObject;
                    else
                        return null;
                }
            }

            //Fall through.
            return null;
        }

        /// <summary>
        /// Initializes for a spawned NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        /// <param name="syncValues"></param>
        /// <param name="manager"></param>
        public void AddSpawn(NetworkManager manager, int objectId, int ownerId, SpawnType ost, byte componentIndex, int rootObjectId, int? parentObjectId, byte? parentComponentIndex
            , short? prefabId, Vector3? localPosition, Quaternion? localRotation, Vector3? localScale, ulong sceneId, ArraySegment<byte> rpcLinks, ArraySegment<byte> syncValues)
        {
            CachedNetworkObject cnob = _cachedObjects.AddReference();
            cnob.InitializeSpawn(manager, objectId, ownerId, ost, componentIndex, rootObjectId, parentObjectId, parentComponentIndex
                , prefabId, localPosition, localRotation, localScale, sceneId, rpcLinks, syncValues);
        }

        public void AddDespawn(int objectId, DespawnType despawnType)
        {
            CachedNetworkObject cnob = _cachedObjects.AddReference();
            cnob.InitializeDespawn(objectId, despawnType);
        }

        /// <summary>
        /// Iterates any written objects.
        /// </summary>
        public void Iterate()
        {
            int written = _cachedObjects.Written;
            if (written == 0)
                return;

            try
            {
                //Indexes which have already been processed.
                HashSet<int> processedIndexes = new HashSet<int>();
                List<CachedNetworkObject> collection = _cachedObjects.Collection;
                _conflictingDespawns.Clear();
                /* The next iteration will set rpclinks,
                 * synctypes, and so on. */
                for (int i = 0; i < written; i++)
                {
                    /* An index may already be processed if it was pushed ahead.
                     * This can occur if a nested object spawn exists but the root
                     * object has not spawned yet. In this situation the root spawn is
                     * found and performed first. */
                    if (processedIndexes.Contains(i))
                        continue;
                    CachedNetworkObject cnob = collection[i];
                    bool spawn = (cnob.Action == CachedNetworkObject.ActionType.Spawn);

                    /* See if nested, and if so check if root is already spawned.
                     * If parent is not spawned then find it and process the parent first. */
                    if (spawn)
                    {
                        /* When an object is nested or has a parent it is
                         * dependent upon either the root of nested, or the parent,
                         * being spawned to setup properly.
                         * 
                         * When either of these are true check spawned objects first
                         * to see if the objects exist. If not check if they are appearing
                         * later in the cache. Root or parent objects can appear later
                         * in the cache depending on the order of which observers are rebuilt.
                         * While it is possible to have the server ensure spawns always send
                         * root/parents first, that's a giant can of worms that's not worth getting into.
                         * Not only are there many scenarios to cover, but it also puts more work
                         * on the server. It's more effective to have the client handle the sorting. */

                        //Nested.
                        if (cnob.IsNested || cnob.HasParent)
                        {
                            bool nested = cnob.IsNested;
                            //It's not possible to be nested and have a parent. Set the Id to look for based on if nested or parented.
                            int targetObjectId = (nested) ? cnob.RootObjectId : cnob.ParentObjectId.Value;
                            NetworkObject nob = GetSpawnedObject(targetObjectId);
                            //If not spawned yet.
                            if (nob == null)
                            {
                                bool found = false;
                                for (int z = (i + 1); z < written; z++)
                                {
                                    CachedNetworkObject zCnob = collection[z];
                                    if (zCnob.ObjectId == targetObjectId)
                                    {
                                        found = true;
                                        if (cnob.Action != CachedNetworkObject.ActionType.Spawn)
                                        {
                                            if (_networkManager.CanLog(LoggingType.Error))
                                            {
                                                string errMsg = (nested)
                                                    ? $"ObjectId {targetObjectId} was found for a nested spawn, but ActionType is not spawn. ComponentIndex {cnob.ComponentIndex} will not be spawned."
                                                    : $"ObjectId {targetObjectId} was found for a parented spawn, but ActionType is not spawn. ObjectId {cnob.ObjectId} will not be spawned.";
                                                Debug.LogError(errMsg);
                                            }
                                            break;
                                        }
                                        else
                                        {
                                            ProcessObject(zCnob, true, z);
                                            break;
                                        }
                                    }
                                }
                                //Root nob could not be found.
                                if (!found && _networkManager.CanLog(LoggingType.Error))
                                {
                                    string errMsg = (nested)
                                        ? $"ObjectId {targetObjectId} could not be found for a nested spawn. ComponentIndex {cnob.ComponentIndex} will not be spawned."
                                        : $"ObjectId {targetObjectId} was found for a parented spawn. ObjectId {cnob.ObjectId} will not be spawned.";
                                    Debug.LogError(errMsg);
                                }
                            }
                        }
                    }

                    ProcessObject(cnob, spawn, i);
                }

                void ProcessObject(CachedNetworkObject cnob, bool spawn, int index)
                {
                    processedIndexes.Add(index);

                    if (spawn)
                    {
                        if (cnob.IsSceneObject)
                            cnob.NetworkObject = _clientObjects.GetSceneNetworkObject(cnob);
                        else if (cnob.IsNested)
                            cnob.NetworkObject = _clientObjects.GetNestedNetworkObject(cnob);
                        else
                            cnob.NetworkObject = _clientObjects.GetInstantiatedNetworkObject(cnob);
                    }
                    else
                    {
                        cnob.NetworkObject = _clientObjects.GetSpawnedNetworkObject(cnob);
                        /* Do not log unless not nested. Nested nobs sometimes
                         * could be destroyed if parent was first. */
                        if (!_networkManager.IsHost && cnob.NetworkObject == null && !cnob.IsNested)
                            _networkManager.Log($"NetworkObject for ObjectId of {cnob.ObjectId} was found null. Unable to despawn object. This may occur if a nested NetworkObject had it's parent object unexpectedly destroyed. This incident is often safe to ignore.");
                    }
                    NetworkObject nob = cnob.NetworkObject;
                    //No need to error here, the other Gets above would have.
                    if (nob == null)
                        return;

                    if (spawn)
                    {
                        //If not also server then object also has to be preinitialized.
                        if (!_networkManager.IsServer)
                        {
                            int ownerId = cnob.OwnerId;
                            //If local client is owner then use localconnection reference.
                            NetworkConnection localConnection = _networkManager.ClientManager.Connection;
                            NetworkConnection owner;
                            //If owner is self.
                            if (ownerId == localConnection.ClientId)
                            {
                                owner = localConnection;
                            }
                            else
                            {
                                /* If owner cannot be found then share owners
                                 * is disabled */
                                if (!_networkManager.ClientManager.Clients.TryGetValueIL2CPP(ownerId, out owner))
                                    owner = NetworkManager.EmptyConnection;
                            }
                            nob.PreinitializeInternal(_networkManager, cnob.ObjectId, owner, false);
                        }

                        _clientObjects.AddToSpawned(cnob.NetworkObject, false);
                        SpawningObjects.Add(cnob.ObjectId, cnob.NetworkObject);

                        IterateSpawn(cnob);
                        _iteratedSpawns.Add(cnob.NetworkObject);
                    }
                    else
                    {
                        /* If spawned already this iteration then the nob
                         * must be initialized so that the start/stop cycles
                         * complete normally. Otherwise, the despawn callbacks will
                         * fire immediately while the start callbacks will run after all
                         * spawns have been iterated.
                         * The downside to this is that synctypes
                         * for spawns later in this iteration will not be initialized
                         * yet, and if the nob being spawned/despawned references
                         * those synctypes the values will be default.
                         * 
                         * The alternative is to delay the despawning until after
                         * all spawns are iterated, but that will break the order
                         * reliability. This is unfortunately a lose/lose situation so
                         * the best we can do is let the user know the risk. */
                        if (_iteratedSpawns.Contains(cnob.NetworkObject))
                        {
                            if (!_loggedSameTickWarning)
                            {
                                _loggedSameTickWarning = true;
                                _networkManager.LogWarning($"NetworkObject {cnob.NetworkObject.name} is being despawned on the same tick it's spawned." +
                                               $" When this occurs SyncTypes will not be set on other objects during the time of this despawn." +
                                               $" In result, if NetworkObject {cnob.NetworkObject.name} is referencing a SyncType of another object being spawned this tick, the returned values will be default.");
                            }

                            _conflictingDespawns.Add(cnob.ObjectId);
                            cnob.NetworkObject.gameObject.SetActive(true);
                            cnob.NetworkObject.Initialize(false);
                        }
                        //Now being initialized, despawn the object.
                        IterateDespawn(cnob);
                    }
                }

                /* Lastly activate the objects after all data
                 * has been synchronized. This will execute callbacks,
                 * and any synctype hooks after the callbacks. */
                for (int i = 0; i < written; i++)
                {
                    CachedNetworkObject cnob = collection[i];
                    if (cnob.Action == CachedNetworkObject.ActionType.Spawn)
                    {
                        /* Apply syncTypes. It's very important to do this after all
                         * spawns have been processed and added to the manager.Objects collection.
                         * Otherwise, the synctype may reference an object spawning the same tick
                         * and the result would be null due to said object not being in spawned. */
                        foreach (NetworkBehaviour nb in cnob.NetworkObject.NetworkBehaviours)
                        {
                            PooledReader reader = cnob.SyncValuesReader;
                            //SyncVars.
                            int length = reader.ReadInt32();
                            nb.OnSyncType(reader, length, false);
                            //SyncObjects
                            length = reader.ReadInt32();
                            nb.OnSyncType(reader, length, true);
                        }

                        /* Only continue with the initialization if it wasn't initialized
                         * early to prevent a despawn conflict. */
                        bool canInitialize = (!_conflictingDespawns.Contains(cnob.ObjectId) || !_iteratedSpawns.Contains(cnob.NetworkObject));
                        if (canInitialize)
                        {
                            cnob.NetworkObject.gameObject.SetActive(true);
                            cnob.NetworkObject.Initialize(false);
                        }
                    }
                }
            }
            finally
            {
                //Once all have been iterated reset.
                Reset();
            }
        }

        /// <summary>
        /// Initializes an object on clients and spawns the NetworkObject.
        /// </summary>
        /// <param name="cnob"></param>
        private void IterateSpawn(CachedNetworkObject cnob)
        {
            /* All nob spawns have been added to spawned before
            * they are processed. This ensures they will be found if
            * anything is referencing them before/after initialization. */
            /* However, they have to be added again here should an ItereteDespawn
             * had removed them. This can occur if an object is set to be spawned,
             * thus added to spawned before iterations, then a despawn runs which
             * removes it from spawn. */
            _clientObjects.AddToSpawned(cnob.NetworkObject, false);

            List<ushort> rpcLinkIndexes = new List<ushort>();
            //Apply rpcLinks.
            foreach (NetworkBehaviour nb in cnob.NetworkObject.NetworkBehaviours)
            {
                PooledReader reader = cnob.RpcLinkReader;
                int length = reader.ReadInt32();

                int readerStart = reader.Position;
                while (reader.Position - readerStart < length)
                {
                    //Index of RpcLink.
                    ushort linkIndex = reader.ReadUInt16();
                    RpcLink link = new RpcLink(
                        cnob.NetworkObject.ObjectId, nb.ComponentIndex,
                        //RpcHash.
                        reader.ReadUInt16(),
                        //ObserverRpc.
                        (RpcType)reader.ReadByte());
                    //Add to links.
                    _clientObjects.SetRpcLink(linkIndex, link);

                    rpcLinkIndexes.Add(linkIndex);
                }
            }
            cnob.NetworkObject.SetRpcLinkIndexes(rpcLinkIndexes);
        }

        /// <summary>
        /// Deinitializes an object on clients and despawns the NetworkObject.
        /// </summary>
        /// <param name="cnob"></param>
        private void IterateDespawn(CachedNetworkObject cnob)
        {
            _clientObjects.Despawn(cnob.NetworkObject, cnob.DespawnType, false);
        }

        /// <summary>
        /// Returns a NetworkObject found in spawn cache, or Spawned.
        /// </summary>
        /// <param name="objectId"></param>
        internal NetworkObject GetSpawnedObject(int objectId)
        {
            NetworkObject result;
            //If not found in Spawning then check Spawned.
            if (!SpawningObjects.TryGetValue(objectId, out result))
            {
                Dictionary<int, NetworkObject> spawned = (_networkManager.IsHost) ?
                    _networkManager.ServerManager.Objects.Spawned
                    : _networkManager.ClientManager.Objects.Spawned;
                spawned.TryGetValue(objectId, out result);
            }

            return result;
        }


        /// <summary>
        /// Resets cache.
        /// </summary>
        public void Reset()
        {
            _cachedObjects.Reset();
            _iteratedSpawns.Clear();
            SpawningObjects.Clear();
        }
    }

    /// <summary>
    /// A cached network object which exist in world but has not been Initialized yet.
    /// </summary>
    [Preserve]
    internal class CachedNetworkObject
    {
        #region Types.
        public enum ActionType
        {
            Unset = 0,
            Spawn = 1,
            Despawn = 2,
        }
        #endregion

        /// <summary>
        /// True if cached object is nested.
        /// </summary>
        public bool IsNested => (ComponentIndex > 0);
        /// <summary>
        /// True if a scene object.
        /// </summary>
        public bool IsSceneObject => (SceneId > 0);
        /// <summary>
        /// True if this object has a parent.
        /// </summary>
        public bool HasParent => (ParentObjectId != null);
        /// <summary>
        /// True if the parent object is a NetworkBehaviour.
        /// </summary>
        public bool ParentIsNetworkBehaviour => (HasParent && (ParentComponentIndex != null));

        public int ObjectId;
        public int OwnerId;
        public SpawnType SpawnType;
        public DespawnType DespawnType;
        public byte ComponentIndex;
        public int RootObjectId;
        public int? ParentObjectId;
        public byte? ParentComponentIndex;
        public short? PrefabId;
        public Vector3? LocalPosition;
        public Quaternion? LocalRotation;
        public Vector3? LocalScale;
        public ulong SceneId;
        public ArraySegment<byte> RpcLinks;
        public ArraySegment<byte> SyncValues;



        /// <summary>
        /// True if spawning.
        /// </summary>
        public ActionType Action { get; private set; }
        /// <summary>
        /// Cached NetworkObject.
        /// </summary>
#pragma warning disable 0649
        public NetworkObject NetworkObject;
        /// <summary>
        /// Reader containing rpc links for the network object.
        /// </summary>
        public PooledReader RpcLinkReader { get; private set; }
        /// <summary>
        /// Reader containing sync values for the network object.
        /// </summary>
        public PooledReader SyncValuesReader { get; private set; }
#pragma warning restore 0649

        public void InitializeSpawn(NetworkManager manager, int objectId, int ownerId, SpawnType objectSpawnType, byte componentIndex, int rootObjectId, int? parentObjectId, byte? parentComponentIndex
    , short? prefabId, Vector3? localPosition, Quaternion? localRotation, Vector3? localScale, ulong sceneId, ArraySegment<byte> rpcLinks, ArraySegment<byte> syncValues)
        {
            ResetValues();
            Action = ActionType.Spawn;
            ObjectId = objectId;
            OwnerId = ownerId;
            SpawnType = objectSpawnType;
            ComponentIndex = componentIndex;
            RootObjectId = rootObjectId;
            ParentObjectId = parentObjectId;
            ParentComponentIndex = parentComponentIndex;
            PrefabId = prefabId;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            SceneId = sceneId;
            RpcLinks = rpcLinks;
            SyncValues = syncValues;

            RpcLinkReader = ReaderPool.GetReader(rpcLinks, manager);
            SyncValuesReader = ReaderPool.GetReader(syncValues, manager);
        }

        /// <summary>
        /// Initializes for a despawned NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        public void InitializeDespawn(int objectId, DespawnType despawnType)
        {
            ResetValues();
            Action = ActionType.Despawn;
            DespawnType = despawnType;
            ObjectId = objectId;
        }

        /// <summary>
        /// Resets values which could malform identify the cached object.
        /// </summary>
        private void ResetValues()
        {
            NetworkObject = null;
        }

        ~CachedNetworkObject()
        {
            if (RpcLinkReader != null)
                RpcLinkReader.Dispose();
            if (SyncValuesReader != null)
                SyncValuesReader.Dispose();
        }
    }

}