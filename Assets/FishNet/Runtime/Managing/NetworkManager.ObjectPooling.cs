using FishNet.Object;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishNet.Managing
{
    public sealed partial class NetworkManager : MonoBehaviour
    {
        /// <summary>
        /// Returns an instantiated or pooled object using supplied values. When a value is not specified it uses default values to the prefab or NetworkManager.       
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkObject GetPooledInstantiated(NetworkObject prefab, bool asServer)
        {
            return GetPooledInstantiated(prefab.PrefabId, prefab.SpawnableCollectionId, parent: null, position: null, rotation: null, scale: null, makeActive: true, asServer);
        }
        /// <summary>
        /// Returns an instantiated or pooled object using supplied values. When a value is not specified it uses default values to the prefab or NetworkManager.       
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkObject GetPooledInstantiated(NetworkObject prefab, Vector3 position, Quaternion rotation, bool asServer)
            => GetPooledInstantiated(prefab.PrefabId, prefab.SpawnableCollectionId, parent: null, position, rotation, scale: null, makeActive: true, asServer);
        /// <summary>
        /// Returns an instantiated or pooled object using supplied values. When a value is not specified it uses default values to the prefab or NetworkManager.       
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NetworkObject GetPooledInstantiated(GameObject prefab, bool asServer)
        {
            if (SetPrefabInformation(prefab, out _, out int prefabId, out ushort collectionId))
                return GetPooledInstantiated(prefabId, collectionId, parent: null, position: null, rotation: null, scale: null, makeActive: true, asServer);
            //Fallthrough, failure.
            return null;
        }
        /// <summary>
        /// Returns an instantiated or pooled object using supplied values. When a value is not specified it uses default values to the prefab or NetworkManager.       
        /// </summary>
        public NetworkObject GetPooledInstantiated(GameObject prefab, Vector3 position, Quaternion rotation, bool asServer)
        {
            if (SetPrefabInformation(prefab, out _, out int prefabId, out ushort collectionId))
                return GetPooledInstantiated(prefabId, collectionId, parent: null, position, rotation, scale: null, makeActive: true, asServer);
            //Fallthrough, failure.
            return null;
        }
        /// <summary>
        /// Returns an instantiated or pooled object using supplied values. When a value is not specified it uses default values to the prefab or NetworkManager.
        /// </summary>
        public NetworkObject GetPooledInstantiated(int prefabId, ushort collectionId, bool asServer)
            => GetPooledInstantiated(prefabId, collectionId, parent: null, position: null, rotation: null, scale: null, makeActive: true, asServer: asServer);
        /// <summary>
        /// Returns an instantiated or pooled object using supplied values. When a value is not specified it uses default values to the prefab or NetworkManager.
        /// </summary>
        public NetworkObject GetPooledInstantiated(int prefabId, ushort collectionId, Vector3 position, Quaternion rotation, bool asServer)
            => GetPooledInstantiated(prefabId, collectionId, parent: null, position, rotation, scale: null, makeActive: true, asServer);
        /// <summary>
        /// Returns an instantiated or pooled object using supplied values. When a value is not specified it uses default values to the prefab or NetworkManager.
        /// </summary>
        /// <param name="makeActive">True to make the NetworkObject active if not already. Using false will not prevent an object from activating via instantation, but rather indicates to not set active manually prior to returning a NetworkObject.</param>
        public NetworkObject GetPooledInstantiated(int prefabId, ushort collectionId, Transform parent, Vector3? position, Quaternion? rotation, Vector3? scale, bool makeActive, bool asServer)
            => _objectPool.RetrieveObject(prefabId, collectionId, parent, position, rotation, scale, makeActive, asServer);
        /// <summary>
        /// Stores an instantied object.
        /// </summary>
        /// <param name="instantiated">Object which was instantiated.</param>
        /// <param name="asServer">True to store for the server.</param>
        public void StorePooledInstantiated(NetworkObject instantiated, bool asServer)
        {
            _objectPool.StoreObject(instantiated, asServer);
        }
        /// <summary>
        /// Instantiates a number of objects and adds them to the pool.
        /// </summary>
        /// <param name="prefab">Prefab to cache.</param>
        /// <param name="count">Quantity to spawn.</param>
        /// <param name="asServer">True if storing prefabs for the server collection. This is only applicable when using DualPrefabObjects.</param>
        public void CacheObjects(NetworkObject prefab, int count, bool asServer)
        {
            _objectPool.CacheObjects(prefab, count, asServer);
        }

        /// <summary>
        /// Outputs a prefab, along with it's Id and collectionId. Returns if the information could be found.
        /// </summary>
        private bool SetPrefabInformation(GameObject prefab, out NetworkObject nob, out int prefabId, out ushort collectionId)
        {
            if (!prefab.TryGetComponent<NetworkObject>(out nob))
            {
                prefabId = 0;
                collectionId = 0;
                InternalLogError($"NetworkObject was not found on {prefab}. An instantiated NetworkObject cannot be returned.");
                return false;
            }
            else
            {
                prefabId = nob.PrefabId;
                collectionId = nob.SpawnableCollectionId;
                return true;
            }
        }

    }


}