using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Object.Helping;
using FishNet.Serializing;
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

        #region Private.
        /// <summary>
        /// Cached objects buffer. Contains spawns and despawns.
        /// </summary>
        private ListCache<CachedNetworkObject> _cachedObjects = new ListCache<CachedNetworkObject>(0);
        /// <summary>
        /// NetworkObjects which have been spawned already during the current iteration.
        /// </summary>
        private HashSet<NetworkObject> _iteratedSpawns = new HashSet<NetworkObject>();
        /// <summary>
        /// ClientObjects reference.
        /// </summary>
        private ClientObjects _clientObjects;
        /// <summary>
        /// NetworkManager for this cache.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Objets which are being spawned during iteration.
        /// </summary>
        private HashSet<NetworkObject> _spawningObjects = new HashSet<NetworkObject>();
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
                if (cnob.NetworkObject.ObjectId == objectId)
                {
                    //Any condition always returns.
                    if (searchType == CacheSearchType.Any)
                        return cnob.NetworkObject;

                    bool spawning = (searchType == CacheSearchType.Spawning);
                    if (cnob.Spawn == spawning)
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
        public void AddSpawn(NetworkObject nob, ArraySegment<byte> rpcLinks, ArraySegment<byte> syncValues, NetworkManager manager)
        {
            CachedNetworkObject cnob = _cachedObjects.AddReference();
            cnob.InitializeSpawn(nob, rpcLinks, syncValues, manager);
            _clientObjects.AddToSpawned(nob, false);
            _spawningObjects.Add(nob);
        }

        /// <summary>
        /// Initializes for a despawned NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        public void AddDespawn(NetworkObject nob)
        {
            CachedNetworkObject cnob = _cachedObjects.AddReference();
            cnob.InitializeDespawn(nob);
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
                List<CachedNetworkObject> collection = _cachedObjects.Collection;
                bool despawnConflict = false;
                /* The next iteration will set rpclinks,
                 * synctypes, and so on. */
                for (int i = 0; i < written; i++)
                {
                    CachedNetworkObject cnob = collection[i];
                    if (cnob.Spawn)
                    {
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
                            if (_networkManager.CanLog(LoggingType.Common))
                                Debug.Log($"NetworkObject {cnob.NetworkObject.name} is being despawned on the same tick it's spawned." +
                                    $" When this occurs SyncTypes will not be set on other objects during the time of this despawn." +
                                    $" In result, if NetworkObject {cnob.NetworkObject.name} is referencing a SyncType of another object being spawned this tick, the returned values will be default.");

                            despawnConflict = true;
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
                    if (cnob.Spawn)
                    {
                        /* Only continue with the initialization if it wasn't initialized
                         * early to prevent a despawn conflict. */
                        bool canInitialize = (!despawnConflict || !_iteratedSpawns.Contains(cnob.NetworkObject));
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

            //Apply syncTypes.
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
        }

        /// <summary>
        /// Deinitializes an object on clients and despawns the NetworkObject.
        /// </summary>
        /// <param name="cnob"></param>
        private void IterateDespawn(CachedNetworkObject cnob)
        {
            _clientObjects.Despawn(cnob.NetworkObject, false);
        }

        /// <summary>
        /// Resets cache.
        /// </summary>
        public void Reset()
        {
            _cachedObjects.Reset();
            _iteratedSpawns.Clear();
            _spawningObjects.Clear();
        }
    }

    /// <summary>
    /// A cached network object which exist in world but has not been Initialized yet.
    /// </summary>
    [Preserve]
    internal class CachedNetworkObject
    {
        /// <summary>
        /// True if spawning.
        /// </summary>
        public bool Spawn { get; private set; }
        /// <summary>
        /// Cached NetworkObject.
        /// </summary>
#pragma warning disable 0649
        public NetworkObject NetworkObject { get; private set; }
        /// <summary>
        /// Reader containing rpc links for the network object.
        /// </summary>
        public PooledReader RpcLinkReader { get; private set; }
        /// <summary>
        /// Reader containing sync values for the network object.
        /// </summary>
        public PooledReader SyncValuesReader { get; private set; }
#pragma warning restore 0649
        /// <summary>
        /// Initializes for a spawned NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        /// <param name="syncValues"></param>
        /// <param name="manager"></param>
        public void InitializeSpawn(NetworkObject nob, ArraySegment<byte> rpcLinks, ArraySegment<byte> syncValues, NetworkManager manager)
        {
            Spawn = true;

            NetworkObject = nob;
            RpcLinkReader = ReaderPool.GetReader(rpcLinks, manager);
            SyncValuesReader = ReaderPool.GetReader(syncValues, manager);
        }

        /// <summary>
        /// Initializes for a despawned NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        public void InitializeDespawn(NetworkObject nob)
        {
            Spawn = false;
            NetworkObject = nob;
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