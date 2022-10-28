using FishNet.Managing.Object;
using FishNet.Object;
using System;
using UnityEngine;

namespace FishNet.Managing
{
    public partial class NetworkManager : MonoBehaviour
    {
        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Collection to use for spawnable objects.")]
        [SerializeField]
        private PrefabObjects _spawnablePrefabs;
        /// <summary>
        /// Collection to use for spawnable objects.
        /// </summary>
        public PrefabObjects SpawnablePrefabs { get => _spawnablePrefabs; set => _spawnablePrefabs = value; }
        #endregion


        /// <summary>
        /// Gets the index a prefab uses. Can be used in conjuction with GetPrefab.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="asServer">True if to get from the server collection.</param>
        /// <returns>Returns index if found, and -1 if not found.</returns>
        public int GetPrefabIndex(GameObject prefab, bool asServer)
        {
            int count = SpawnablePrefabs.GetObjectCount();
            for (int i = 0; i < count; i++)
            {
                GameObject go = SpawnablePrefabs.GetObject(asServer, i).gameObject;
                if (go == prefab)
                    return i;
            }

            //Fall through, not found.
            return -1;
        }

        /// <summary>
        /// Returns a prefab with prefabId.
        /// This method will bypass object pooling.
        /// </summary>
        /// <param name="prefabId">PrefabId to get.</param>
        /// <param name="asServer">True if getting the prefab asServer.</param>
        public NetworkObject GetPrefab(int prefabId, bool asServer)
        {
            return SpawnablePrefabs.GetObject(asServer, prefabId);
        }

    }

}