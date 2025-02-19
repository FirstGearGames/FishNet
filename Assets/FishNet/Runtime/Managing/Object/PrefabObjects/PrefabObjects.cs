using FishNet.Documenting;
using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Object
{
    //document
    [APIExclude]
    public abstract class PrefabObjects : ScriptableObject
    {
        /// <summary>
        /// CollectionId for this <see cref="PrefabObjects"/>
        /// </summary>
        public ushort CollectionId { get; private set; }
        /// <summary>
        /// Invoked when an object is removed
        /// </summary>
        public event Action<int, bool> OnObjectDiscarded;
       
        /// <summary>
        /// Invoked when an object is added
        /// </summary>
        public event Action<int, NetworkObject, bool> OnObjectAdded;
        /// <summary>
        /// Sets CollectionIdValue.
        /// </summary>
        internal void SetCollectionId(ushort id) => CollectionId = id;
        /// <summary>
        /// True if this <see cref="PrefabObjects"/> has prefabs which are to be loaded per request.
        /// </summary>
        public abstract bool UsingOnDemandPrefabs();
        public abstract void Clear();
        public abstract int GetObjectCount();
        public abstract NetworkObject GetObject(int id, bool asServer);
        public abstract bool HasObject(int id, bool asServer);
        public abstract void RemoveNull();
        public abstract void AddObject(NetworkObject networkObject, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void AddObjects(List<NetworkObject> networkObjects, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void AddObjects(NetworkObject[] networkObjects, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void AddObject(DualPrefab dualPrefab, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void AddObjects(List<DualPrefab> dualPrefab, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void AddObjects(DualPrefab[] dualPrefab, bool checkForDuplicates = false, bool initializeAdded = true);
        public abstract void InitializePrefabRange(int startIndex);
        /// <summary>
        /// Begin async retrieval of the object with id and then add them when done.
        /// </summary>
        public virtual void RequestObjectAsync(int id, bool asServer) { }
        /// <summary>
        /// Begin async retrieval of multiple objects by id and then add them.
        /// </summary>
        public virtual void RequestObjectAsync(int[] ids, bool asServer) { }

    }
}