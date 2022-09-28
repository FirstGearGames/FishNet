using FishNet.Object;
using FishNet.Utility.Extension;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Utility.Performance
{


    public class DefaultObjectPool : ObjectPool
    {
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
        /// Pooled network objects.
        /// </summary>
        private Dictionary<int, Stack<NetworkObject>> _cached = new Dictionary<int, Stack<NetworkObject>>();
        #endregion

        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public override NetworkObject RetrieveObject(int prefabId, bool asServer)
        {
            //Quick exit/normal retrieval when not using pooling.
            if (!_enabled)
            {
                NetworkObject prefab = base.NetworkManager.SpawnablePrefabs.GetObject(asServer, prefabId);
                return Instantiate(prefab);
            }

            Stack<NetworkObject> cache;
            //No cache for prefabId yet, make one.
            if (!_cached.TryGetValueIL2CPP(prefabId, out cache))
            {
                cache = new Stack<NetworkObject>();
                _cached[prefabId] = cache;
            }

            NetworkObject nob;
            //Iterate until nob is populated just in case cache entries have been destroyed.
            do
            {
                if (cache.Count == 0)
                {
                    NetworkObject prefab = base.NetworkManager.SpawnablePrefabs.GetObject(asServer, prefabId);
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
        /// <param name="prefabId">PrefabId of the object.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public override void StoreObject(NetworkObject instantiated, int prefabId, bool asServer)
        {
            //Pooling is not enabled.
            if (!_enabled)
            {
                Destroy(instantiated.gameObject);
                return;
            }

            Stack<NetworkObject> cache;
            if (!_cached.TryGetValue(prefabId, out cache))
            {
                cache = new Stack<NetworkObject>();
                _cached[prefabId] = cache;
            }

            instantiated.gameObject.SetActive(false);
            instantiated.ResetForObjectPool();
            cache.Push(instantiated);
        }
    }


}