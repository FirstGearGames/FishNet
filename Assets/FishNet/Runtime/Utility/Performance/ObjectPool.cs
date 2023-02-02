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
        public abstract NetworkObject RetrieveObject(int prefabId, bool asServer);
        /// <summary>
        /// Returns an object that has been stored. A new object will be created if no stored objects are available.
        /// </summary>
        /// <param name="prefabId">PrefabId of the object to return.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public virtual NetworkObject RetrieveObject(int prefabId, ushort collectionId, bool asServer) => null;
        /// <summary>
        /// Stores an object into the pool.
        /// </summary>
        /// <param name="instantiated">Object to store.</param>
        /// <param name="asServer">True if being called on the server side.</param>
        /// <returns></returns>
        public abstract void StoreObject(NetworkObject instantiated, bool asServer);
    }

}