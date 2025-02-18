using Codice.Client.BaseCommands.BranchExplorer;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Utility.Performance
{

    public class DefaultObjectPool : ObjectPool
    {
        #region Public.
        /// <summary>
        /// Cache for pooled NetworkObjects.
        /// </summary>
        public IReadOnlyList<Dictionary<int, Stack<NetworkObject>>> Cache => _cache;
        private List<Dictionary<int, Stack<NetworkObject>>> _cache = new();
        #endregion

        #region Serialized.
        /// <summary>
        /// True if to use object pooling.
        /// </summary>
        [Tooltip("True if to use object pooling.")]
        [SerializeField]
        private bool _enabled = true;
        #endregion

        #region Private.
        /// <summary>
        /// Current count of the cache collection.
        /// </summary>
        private int _cacheCount = 0;
        #endregion

        
#pragma warning disable CS0672 // Member overrides obsolete member
        public override NetworkObject RetrieveObject(int prefabId, ushort collectionId, Transform parent = null, Vector3? nullablePosition = null, Quaternion? nullableRotation = null, Vector3? nullableScale = null, bool makeActive = true, bool asServer = true)
#pragma warning restore CS0672 // Member overrides obsolete member
        {
            ObjectPoolRetrieveOption options = ObjectPoolRetrieveOption.Unset;
            if (makeActive)
                options |= ObjectPoolRetrieveOption.MakeActive;

            return RetrieveObject(prefabId, collectionId, options, parent, nullablePosition, nullableRotation, nullableScale, asServer);
        }

        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="collectionId">CollectionId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public override NetworkObject RetrieveObject(int prefabId, ushort collectionId, ObjectPoolRetrieveOption options, Transform parent = null, Vector3? nullablePosition = null, Quaternion? nullableRotation = null, Vector3? nullableScale = null, bool asServer = true)
        {
            bool makeActive = options.FastContains(ObjectPoolRetrieveOption.MakeActive);
            bool localSpace = options.FastContains(ObjectPoolRetrieveOption.LocalSpace);

            if (!_enabled)
                return GetFromInstantiate();

            Stack<NetworkObject> cache = GetOrCreateCache(collectionId, prefabId);
            NetworkObject nob = null;

            //Iterate until nob is populated just in case cache entries have been destroyed.
            while (nob == null)
            {
                if (cache.TryPop(out nob))
                {
                    if (nob != null)
                    {
                        nob.transform.SetParent(parent);
                        if (localSpace)
                            nob.transform.SetLocalPositionRotationAndScale(nullablePosition, nullableRotation, nullableScale);
                        else
                            nob.transform.SetWorldPositionRotationAndScale(nullablePosition, nullableRotation, nullableScale);

                        if (makeActive)
                            nob.gameObject.SetActive(true);

                        return nob;
                    }
                }
                //Nothing left in cache.
                else
                {
                    break;
                }
            }

            //Fall through, nothing in cache.
            return GetFromInstantiate();

            //Returns a network object via instantation.
            NetworkObject GetFromInstantiate()
            {
                NetworkObject prefab = GetPrefab(prefabId, collectionId, asServer);
                if (prefab == null)
                {
                    return null;
                }
                else
                {
                    NetworkObject result;
                    Vector3 scale;

                    if (localSpace)
                    {
                        prefab.transform.OutLocalPropertyValues(nullablePosition, nullableRotation, nullableScale, out Vector3 pos, out Quaternion rot, out scale);
                        if (parent != null)
                        {
                            //Convert pos and rot to world values for the instantiate.
                            pos = parent.TransformPoint(pos);
                            rot = (parent.rotation * rot);
                        }
                        result = Instantiate(prefab, pos, rot, parent);
                    }
                    else
                    {
                        prefab.transform.OutWorldPropertyValues(nullablePosition, nullableRotation, nullableScale, out Vector3 pos, out Quaternion rot, out scale);
                        result = Instantiate(prefab, pos, rot, parent);
                    }

                    result.transform.localScale = scale;

                    if (makeActive)
                        result.gameObject.SetActive(true);
                    return result;
                }
            }
        }
        /// <summary>
        /// Returns a prefab for prefab and collectionId.
        /// </summary>
        public override NetworkObject GetPrefab(int prefabId, ushort collectionId, bool asServer)
        {
            PrefabObjects po = base.NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);
            return po.GetObject(asServer, prefabId);
        }

        /// <summary>
        /// Checks if the <see cref="PrefabObjects"/> of collectionId has the specified prefab readily available.
        /// </summary>
        public override bool CanRetrieveObject(int prefabId, ushort collectionId, bool asServer)
        {
            PrefabObjects po = NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);
            return po.HasObject(asServer, prefabId);
        }

        /// <summary>
        /// Stores an object into the pool.
        /// </summary>
        /// <param name="instantiated">Object to store.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>

        public override void StoreObject(NetworkObject instantiated, bool asServer)
        {
            //Pooling is not enabled.
            if (!_enabled)
            {
                Destroy(instantiated.gameObject);
                return;
            }

            instantiated.gameObject.SetActive(false);
            instantiated.ResetState(asServer);
            Stack<NetworkObject> cache = GetOrCreateCache(instantiated.SpawnableCollectionId, instantiated.PrefabId);
            cache.Push(instantiated);
        }

        /// <summary>
        /// Instantiates a number of objects and adds them to the pool.
        /// </summary>
        /// <param name="prefab">Prefab to cache.</param>
        /// <param name="count">Quantity to spawn.</param>
        /// <param name="asServer">True if storing prefabs for the server collection. This is only applicable when using DualPrefabObjects.</param>
        public override void CacheObjects(NetworkObject prefab, int count, bool asServer)
        {
            if (!_enabled)
                return;
            if (count <= 0)
                return;
            if (prefab == null)
                return;
            if (prefab.PrefabId == NetworkObject.UNSET_PREFABID_VALUE)
            {
                NetworkManagerExtensions.LogError($"Pefab {prefab.name} has an invalid prefabId and cannot be cached.");
                return;
            }

            Stack<NetworkObject> cache = GetOrCreateCache(prefab.SpawnableCollectionId, prefab.PrefabId);
            for (int i = 0; i < count; i++)
            {
                NetworkObject nob = Instantiate(prefab);
                nob.gameObject.SetActive(false);
                cache.Push(nob);
            }
        }

        /// <summary>
        /// Is called after a prefab is removed from a collection
        /// </summary>
        /// <param name="prefabId">PrefabId of the removed prefab</param>
        /// <param name="collectionId">CollectionId of the collection removed from</param>
        internal override void CollectionObjectDiscarded(ushort collectionId, int prefabId, bool asServer)
        {
            if (NetworkManager.IsOffline)
                return;
            /* We will assume user's implementing async spawns make the OnObjectDiscarded callback
             * after they ensure all instances of the prefab are despawned but before attempting to unload
             * them from memory
             */
            ManagedObjects objects = NetworkManager.IsServerStarted ? NetworkManager.ServerManager.Objects : NetworkManager.ClientManager.Objects;

            if (objects.PrefabSpawnCounts[prefabId] > 0)
            {
                NetworkManager.LogWarning(
                    $"PrefabId: [{prefabId}] was removed from it's collection {collectionId} before all instances of it were despawned. " +
                    $"Ensure all instances are despawned before removal.");
                return;
            }

            ClearIndividualCache(collectionId, prefabId);
        }

        /// <summary>
        /// Is called once an object is added to a collection
        /// </summary>
        /// <param name="collectionId">CollectionId of the collection removed from</param>
        /// <param name="prefabId">PrefabId of the removed prefab</param>
        /// <param name="nob">Network object added</param>
        internal override void CollectionObjectAdded(ushort collectionId, int prefabId, NetworkObject nob, bool asServer)
        {
            CacheObjects(nob, prefabId, asServer);
        }



        /// <summary>
        /// Clears pools destroying objects for all collectionIds
        /// </summary>
        public void ClearPool()
        {
            int count = _cache.Count;
            for (int i = 0; i < count; i++)
                ClearPool(i);
        }

        /// <summary>
        /// Clears a pool destroying objects for collectionId.
        /// </summary>
        /// <param name="collectionId">CollectionId to clear for.</param>
        public void ClearPool(int collectionId)
        {
            if (collectionId >= _cacheCount)
                return;

            Dictionary<int, Stack<NetworkObject>> dict = _cache[collectionId];
            foreach (Stack<NetworkObject> item in dict.Values)
            {
                while (item.TryPop(out NetworkObject nob))
                {
                    if (nob != null)
                        Destroy(nob.gameObject);
                }
            }

            dict.Clear();
        }

        public void ClearIndividualCache(int collectionId, int prefabId)
        {
            if (collectionId < 0 || _cache.Count -1 < collectionId)
            {
                return;
            }
            Dictionary<int, Stack<NetworkObject>> dict = _cache[collectionId];

            Stack<NetworkObject> cache;

            if (dict.TryGetValueIL2CPP(prefabId, out cache))
            {
                while (cache.TryPop(out NetworkObject nob))
                {
                    if (nob != null)
                        Destroy(nob.gameObject);
                }
            }
        }

        /// <summary>
        /// Gets a cache for an id or creates one if does not exist.
        /// </summary>
        /// <param name="prefabId"></param>
        /// <returns></returns>
        public Stack<NetworkObject> GetOrCreateCache(int collectionId, int prefabId)
        {
            if (collectionId >= _cacheCount)
            {
                //Add more to the cache.
                while (_cache.Count <= collectionId)
                {
                    Dictionary<int, Stack<NetworkObject>> dict = new();
                    _cache.Add(dict);
                }
                _cacheCount = collectionId;
            }

            Dictionary<int, Stack<NetworkObject>> dictionary = _cache[collectionId];
            Stack<NetworkObject> cache;
            //No cache for prefabId yet, make one.
            if (!dictionary.TryGetValueIL2CPP(prefabId, out cache))
            {
                cache = new();
                dictionary[prefabId] = cache;
            }
            return cache;
        }
    }


}