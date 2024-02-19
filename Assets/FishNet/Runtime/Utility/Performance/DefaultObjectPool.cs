using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using GameKit.Utilities;
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
        /// </summary>  //Remove on 2024/01/01 Convert to IReadOnlyList.
        public IReadOnlyCollection<Dictionary<int, Stack<NetworkObject>>> Cache => _cache;
        private List<Dictionary<int, Stack<NetworkObject>>> _cache = new List<Dictionary<int, Stack<NetworkObject>>>();
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
        /// When a NetworkObject is stored it's parent is set to these objects.
        /// Key: scene handle of the object to parent to.
        /// Value: object to parentt to.
        /// </summary>
        private Dictionary<int, Transform> _objectParents = new Dictionary<int, Transform>();
        #endregion

        #region Consts.
        /// <summary>
        /// Name to give the object which houses pooled NetworkObjects.
        /// </summary>
        private const string OBJECTS_PARENT_NAME = "DefaultObjectPool Parent";
        #endregion

        /// <summary>
        /// Returns an object that has been stored with a collectionId of 0. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] //Remove on 2024/01/01.
#pragma warning disable CS0672 // Member overrides obsolete member
        public override NetworkObject RetrieveObject(int prefabId, bool asServer)
#pragma warning restore CS0672 // Member overrides obsolete member
        {
            return RetrieveObject(prefabId, 0, asServer);
        }


        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="collectionId">CollectionId of the prefab.</param>
        /// <param name="position">Position for object before enabling it.</param>
        /// <param name="rotation">Rotation for object before enabling it.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public override NetworkObject RetrieveObject(int prefabId, ushort collectionId, Vector3 position, Quaternion rotation, bool asServer)
        {
            PrefabObjects po = base.NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);
            //Quick exit/normal retrieval when not using pooling.
            if (!_enabled)
            {
                NetworkObject prefab = po.GetObject(asServer, prefabId);
                return Instantiate(prefab, position, rotation);
            }

            Stack<NetworkObject> cache = GetOrCreateCache(collectionId, prefabId);
            NetworkObject nob;
            //Iterate until nob is populated just in case cache entries have been destroyed.
            do
            {
                if (cache.Count == 0)
                {
                    NetworkObject prefab = po.GetObject(asServer, prefabId);
                    /* A null nob should never be returned from spawnables. This means something
                     * else broke, likely unrelated to the object pool. */
                    nob = Instantiate(prefab, position, rotation);
                    //Can break instantly since we know nob is not null.
                    break;
                }
                else
                {
                    nob = cache.Pop();
                    if (nob != null)
                        nob.transform.SetPositionAndRotation(position, rotation);
                }

            } while (nob == null);

            nob.gameObject.SetActive(true);
            return nob;
        }
        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="collectionId">CollectionId of the prefab.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public override NetworkObject RetrieveObject(int prefabId, ushort collectionId, bool asServer)
        {
            PrefabObjects po = base.NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);
            //Quick exit/normal retrieval when not using pooling.
            if (!_enabled)
            {
                NetworkObject prefab = po.GetObject(asServer, prefabId);
                return Instantiate(prefab);
            }

            Stack<NetworkObject> cache = GetOrCreateCache(collectionId, prefabId);
            NetworkObject nob;
            //Iterate until nob is populated just in case cache entries have been destroyed.
            do
            {
                if (cache.Count == 0)
                {
                    NetworkObject prefab = po.GetObject(asServer, prefabId);
                    /* A null nob should never be returned from spawnables. This means something
                     * else broke, likely unrelated to the object pool. */
                    nob = Instantiate(prefab);
                    //Can break instantly since we know nob is not null.
                    break;
                }
                else
                {
                    nob = cache.Pop();
                }

            } while (nob == null);

            nob.gameObject.SetActive(true);
            return nob;
        }
        /// <summary>
        /// Stores an object into the pool.
        /// </summary>
        /// <param name="instantiated">Object to store.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void StoreObject(NetworkObject instantiated, bool asServer)
        {
            //Pooling is not enabled.
            if (!_enabled)
            {
                Destroy(instantiated.gameObject);
                return;
            }

            instantiated.gameObject.SetActive(false);
            instantiated.ResetState();
            Transform parent = GetObjectStoreParent(instantiated);
            instantiated.transform.SetParent(parent);
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
                InstanceFinder.NetworkManager.LogError($"Pefab {prefab.name} has an invalid prefabId and cannot be cached.");
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
        /// Clears pools destroying objects for all collectionIds
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            if (collectionId >= _cache.Count)
                return;

            Dictionary<int, Stack<NetworkObject>> dict = _cache[collectionId];
            foreach (Stack<NetworkObject> item in dict.Values)
            {
                while (item.Count > 0)
                {
                    NetworkObject nob = item.Pop();
                    if (nob != null)
                        Destroy(nob.gameObject);
                }
            }

            dict.Clear();
        }

        /// <summary>
        /// Returns which parent to use for an object when storing.
        /// </summary>
        /// <param name="nob"></param>
        /// <returns></returns>
        private Transform GetObjectStoreParent(NetworkObject nob)
        {
            Transform parent;
            int sceneHandle = nob.gameObject.scene.handle;
            //Try to output the transform.
            _objectParents.TryGetValue(sceneHandle, out parent);

            //If parent went null then make a new one and put it in the right scene.
            if (parent == null)
            {
                parent = new GameObject(OBJECTS_PARENT_NAME).transform;
                DefaultObjectPoolContainer container = parent.gameObject.AddComponent<DefaultObjectPoolContainer>();
                container.Initialize(this);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent.gameObject, nob.gameObject.scene);
                _objectParents[sceneHandle] = parent;
            }

            return parent;
        }

        /// <summary>
        /// Called when NetworkObjects are about to be destroyed.
        /// </summary>
        internal void ObjectsDestroyed(DefaultObjectPoolContainer container)
        {
            //Remove the container from objectParents.
            _objectParents.Remove(container.gameObject.scene.handle);

            List<NetworkObject> nobs = CollectionCaches<NetworkObject>.RetrieveList();
            int children = container.transform.childCount;

            //Only get the top layer of nobs.
            for (int i = 0; i < children; i++)
            {
                NetworkObject n = container.transform.GetChild(i).GetComponent<NetworkObject>();
                if (n != null)
                    nobs.Add(n);
            }
            //No nobs to process.
            if (nobs.Count == 0)
                return;

            /* This operation is intensive but occurs rarely
             * and prevents memory leaks. */

            /* A much simpler, though very much more expensive approach,
             * would be to iterate each object in nobs and pull the stack
             * for each object, then convert to a collection removing the nob,
             * and then converting back to the stack. This could be expensive
             * because it would be done for each object, meaning if there were
             * even just 10 objects in nobs the conversions would happen all 10 times.
             * 
             * 
             * 
             * Instead we make local collections where we first try to find a saved
             * hashset already made for the object data, and if not we convert the
             * stack to a hashset. This ensures hashsets are only made once per prefab/collectionId.
             * So if there were 10 objects in nobs but there were only 3 different types
             * the conversions would only happen 3 times.
             * Once the hashset is found(or made) the object is removed from it.
             * 
             * After all nobs are removed from the converted hashsets, the hashsets are
             * converted back into the stack. 
             * 
             * This process could probably be made better but we're going to save
             * that for FishNet V4. */

            /* Make a prefabId/NetworkObject hashset cache for each
             * collection Id. This is a very small amount of GC */
            List<Dictionary<int, HashSet<NetworkObject>>> localCached = new List<Dictionary<int, HashSet<NetworkObject>>>();
            for (int i = 0; i < _cache.Count; i++)
                localCached.Add(new Dictionary<int, HashSet<NetworkObject>>());

            int stackStart = 0;
            foreach (NetworkObject item in nobs)
            {
                int collectionId = item.SpawnableCollectionId;
                if (collectionId >= localCached.Count)
                    continue;

                Dictionary<int, HashSet<NetworkObject>> localDict = localCached[item.SpawnableCollectionId];
                HashSet<NetworkObject> localHashSet;
                /* If a local cache does not exist yet for the prefabId
                 * and collectionId then make one. */
                if (!localDict.TryGetValueIL2CPP(item.PrefabId, out localHashSet))
                {
                    localHashSet = CollectionCaches<NetworkObject>.RetrieveHashSet();
                    //Cache for the current ids.
                    Stack<NetworkObject> memberStack = GetOrCreateCache(item.SpawnableCollectionId, item.PrefabId);
                    stackStart = memberStack.Count;
                    while (memberStack.Count > 0)
                        localHashSet.Add(memberStack.Pop());

                    localDict[item.PrefabId] = localHashSet;
                }

                //Remove from the hashset.
               localHashSet.Remove(item);
            }

            //Nobs collection is no longer needed beyond this point.
            CollectionCaches<NetworkObject>.Store(nobs);
            /* Once all hashsets have had entries removed add back
             * remaining nobs to the correct stack. */
            for (int i = 0; i < localCached.Count; i++)
            {
                Dictionary<int, Stack<NetworkObject>> memberDict = _cache[i];
                Dictionary<int, HashSet<NetworkObject>> localDict = localCached[i];
                foreach (KeyValuePair<int, HashSet<NetworkObject>> localItem in localDict)
                {
                    /* The stack will always exist since we used GetOrCreate above
                     * to populate our localDict.
                     * But check if not to throw, just in case. */
                    if (memberDict.TryGetValueIL2CPP(localItem.Key, out Stack<NetworkObject> stk))
                    {
                        foreach (NetworkObject n in localItem.Value)
                            stk.Push(n);
                    }
                    else
                    {
                        Debug.LogError($"Stack could not be found for {localItem.Key}.");
                    }

                    //Once here the hashset has been added back and the hashset can be returned.
                    CollectionCaches<NetworkObject>.Store(localItem.Value);
                }
            }

        }

        /// <summary>
        /// Gets a cache for an id or creates one if does not exist.
        /// </summary>
        /// <param name="prefabId"></param>
        /// <returns></returns>
        private Stack<NetworkObject> GetOrCreateCache(int collectionId, int prefabId)
        {
            if (collectionId >= _cache.Count)
            {
                //Add more to the cache.
                while (_cache.Count <= collectionId)
                {
                    Dictionary<int, Stack<NetworkObject>> dict = new Dictionary<int, Stack<NetworkObject>>();
                    _cache.Add(dict);
                }
            }

            Dictionary<int, Stack<NetworkObject>> dictionary = _cache[collectionId];
            Stack<NetworkObject> cache;
            //No cache for prefabId yet, make one.
            if (!dictionary.TryGetValueIL2CPP(prefabId, out cache))
            {
                cache = new Stack<NetworkObject>();
                dictionary[prefabId] = cache;
            }
            return cache;
        }
    }


}