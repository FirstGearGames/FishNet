using FishNet.Managing;
using FishNet.Object;
using System;
using UnityEngine;

namespace FishNet.Utility.Performance
{

    public abstract class ObjectPool : MonoBehaviour
    {
        /// <summary>
        /// NetworkManager this ObjectPool belongs to.
        /// </summary>
        protected NetworkManager NetworkManager {get; private set;}

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        public virtual void InitializeOnce(NetworkManager nm)
        {
            NetworkManager = nm;
        }

        /// <summary>
        /// Returns an object that has been stored using collectioNid of 0. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        [Obsolete("Use RetrieveObject(int, ushort, bool)")] //Remove on 2024/01/01.
        public abstract NetworkObject RetrieveObject(int prefabId, bool asServer);
        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public virtual NetworkObject RetrieveObject(int prefabId, ushort collectionId, bool asServer) => null;
        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public virtual NetworkObject RetrieveObject(int prefabId, ushort collectionId, Vector3 position, Quaternion rotation, bool asServer) => null;
        /// <summary>
        /// Stores an object into the pool.
        /// </summary>
        /// <param name="instantiated">Object to store.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public abstract void StoreObject(NetworkObject instantiated, bool asServer);
        /// <summary>
        /// Instantiates a number of objects and adds them to the pool.
        /// </summary>
        /// <param name="prefab">Prefab to cache.</param>
        /// <param name="count">Quantity to spawn.</param>
        /// <param name="asServer">True if storing prefabs for the server collection. This is only applicable when using DualPrefabObjects.</param>
        public virtual void CacheObjects(NetworkObject prefab, int count, bool asServer) { }
    }

}