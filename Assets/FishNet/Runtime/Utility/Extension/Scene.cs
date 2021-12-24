using FishNet.Object;
using FishNet.Utility.Performance;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Utility.Extension
{

    public static class SceneFN
    {
        #region Private.

        /// <summary>
        /// Used for performance gains when getting objects.
        /// </summary>
        private static List<GameObject> _gameObjectList = new List<GameObject>();
        /// <summary>
        /// List for NetworkObjects.
        /// </summary>
        private static List<NetworkObject> _networkObjectList = new List<NetworkObject>();
        #endregion

        /// <summary>
        /// Gets all NetworkObjects in a scene.
        /// </summary>
        /// <param name="s">Scene to get objects in.</param>
        /// <param name="count">Number of entries written to the collection.</param>
        /// <returns></returns>
        public static List<NetworkObject> GetSceneNetworkObjects(Scene s, out int count)
        {
            ListCache<NetworkObject> cache = ListCaches.NetworkObjectCache;
            cache.Reset();

            //Iterate all root objects for the scene.
            s.GetRootGameObjects(_gameObjectList);
            for (int i = 0; i < _gameObjectList.Count; i++)
            {
                /* Get NetworkObjects within children of each
                 * root object then add them to the cache. */
                _gameObjectList[i].GetComponentsInChildren<NetworkObject>(true, _networkObjectList);
                if (_networkObjectList != null)
                    cache.AddValues(_networkObjectList);
            }

            count = cache.Written;
            return cache.Collection;
        }

    }

}