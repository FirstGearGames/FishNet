﻿using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using System;
using UnityEngine;
using UnityEngine.XR;

namespace FishNet.Utility.Performance
{
    public abstract class ObjectPool : MonoBehaviour
    {
        /// <summary>
        /// NetworkManager this ObjectPool belongs to.
        /// </summary>
        protected NetworkManager NetworkManager { get; private set; }

        /// <summary>
        /// Called at the end of every frame. This can be used to perform routine tasks.
        /// </summary>
        public virtual void LateUpdate() { }
        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        public virtual void InitializeOnce(NetworkManager nm)
        {
            NetworkManager = nm;
            nm.RuntimeSpawnablePrefabsWrapper.OnPrefabDiscarded += OnPrefabDiscardedForCollection;
        }

        /// <summary>
        /// Is called after a prefab is removed from a collection
        /// </summary>
        /// <param name="prefabId">PrefabId of the removed prefab</param>
        /// <param name="collectionId">CollectionId of the collection removed from</param>
        internal virtual void OnPrefabDiscardedForCollection(ushort collectionId, int prefabId, bool asServer)
        {
            
        }



        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="collectionId">CollectionId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        [Obsolete("Use RetrieveObject(int, ushort, RetrieveOption, parent, Vector3?, Quaternion? Vector3?, bool) instead.")] //Remove in V5
        public virtual NetworkObject RetrieveObject(int prefabId, ushort collectionId, Transform parent = null, Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null, bool makeActive = true, bool asServer = true) => null;
        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="collectionId">CollectionId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public virtual NetworkObject RetrieveObject(int prefabId, ushort collectionId, ObjectPoolRetrieveOption options, Transform parent = null, Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null, bool asServer = true) => null;
        /// <summary>
        /// Returns a prefab using specified values.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="collectionId">CollectionId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public virtual NetworkObject GetPrefab(int prefabId, ushort collectionId, bool asServer)
        {
            PrefabObjects po = NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);
            return po.GetObject(asServer, prefabId);
        }
        /// <summary>
        /// Checks if the <see cref="PrefabObjects"/> of collectionId has the specified prefab readily available.
        /// </summary>
        public virtual bool CanRetrieveObject(int prefabId, ushort collectionId, bool asServer)
        {
            PrefabObjects po = NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);       
            return po.HasObject(prefabId, asServer);
        }
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