using FishNet.Object;
using FishNet.Serializing;
using FishNet.Utility.Performance;
using System;
using System.Collections.Generic;

namespace FishNet.Managing.Client
{

    /// <summary>
    /// Information about cached network objects.
    /// </summary>
    internal class NetworkObjectCache
    {
        #region Private.
        /// <summary>
        /// Cached objects buffer. Contains spawn and despawns.
        /// </summary>
        private ListCache<CachedNetworkObject> _cachedObjects = new ListCache<CachedNetworkObject>(0);
        /// <summary>
        /// NetworkManager this cache is for.
        /// </summary>
        private NetworkManager _networkManager;
        #endregion

        public NetworkObjectCache(NetworkManager manager)
        {
            _networkManager = manager;
        }

        /// <summary>
        /// Initializes for a spawned NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        /// <param name="syncValues"></param>
        /// <param name="manager"></param>
        public void AddSpawn(NetworkObject nob, ArraySegment<byte> syncValues, NetworkManager manager)
        {
            CachedNetworkObject cnob = _cachedObjects.AddReference();
            cnob.InitializeSpawn(nob, syncValues, manager);
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
                List<CachedNetworkObject> cnobs = _cachedObjects.Collection;
                for (int i = 0; i < written; i++)
                {
                    CachedNetworkObject cnob = cnobs[i];
                    //Shouldn't be possible, but networkobject went null before iteration could run.
                    if (cnob.NetworkObject == null)
                        continue;

                    if (cnob.Spawn)
                        IterateSpawn(cnob);
                    else
                        IterateDespawn(cnob);
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
            //Apply syncTypes.
            foreach (NetworkBehaviour nb in cnob.NetworkObject.NetworkBehaviours)
            {
                int length;
                PooledReader reader = cnob.SyncValuesReader;
                //SyncVars.
                length = reader.ReadInt32();
                nb.OnSyncType(reader, length, false);
                //SyncObjects
                length = reader.ReadInt32();
                nb.OnSyncType(reader, length, true);
            }

            //Activate.
            cnob.NetworkObject.gameObject.SetActive(true);
            cnob.NetworkObject.Initialize(false);
        }

        /// <summary>
        /// Deinitializes an object on clients and despawns the NetworkObject.
        /// </summary>
        /// <param name="cnob"></param>
        private void IterateDespawn(CachedNetworkObject cnob)
        {
            _networkManager.ClientManager.Objects.Despawn(cnob.NetworkObject, false);
        }

        /// <summary>
        /// Resets cache.
        /// </summary>
        public void Reset()
        {
            _cachedObjects.Reset();
        }
    }

    /// <summary>
    /// A cached network object which exist in world but has not been Initialized yet.
    /// </summary>
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
        public NetworkObject NetworkObject { get; private set; } = null;
        /// <summary>
        /// Reader containing sync values for the network object.
        /// </summary>
        public PooledReader SyncValuesReader { get; private set; } = null;
#pragma warning restore 0649
        /// <summary>
        /// Initializes for a spawned NetworkObject.
        /// </summary>
        /// <param name="nob"></param>
        /// <param name="syncValues"></param>
        /// <param name="manager"></param>
        public void InitializeSpawn(NetworkObject nob, ArraySegment<byte> syncValues, NetworkManager manager)
        {
            Spawn = true;

            NetworkObject = nob;
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
            if (SyncValuesReader != null)
                SyncValuesReader.Dispose();
        }
    }

}