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
        private static List<NetworkObject> _networkObjectListA = new List<NetworkObject>();
        /// <summary>
        /// List for NetworkObjects.
        /// </summary>
        private static List<NetworkObject> _networkObjectListB = new List<NetworkObject>();
        #endregion

        /// <summary>
        /// Gets all NetworkObjects in a scene.
        /// </summary>
        /// <param name="s">Scene to get objects in.</param>
        /// <param name="firstOnly">True to only return the first NetworkObject within an object chain. False will return nested NetworkObjects.</param>
        /// <param name="cache">ListCache of found NetworkObjects.</param>
        /// <returns></returns>
        public static void GetSceneNetworkObjects(Scene s, bool firstOnly, out ListCache<NetworkObject> nobCache)
        {
            nobCache = ListCaches.GetNetworkObjectCache();
            //Iterate all root objects for the scene.
            s.GetRootGameObjects(_gameObjectList);
            foreach (GameObject go in _gameObjectList)
            {

                //Get NetworkObjects within children of each root.
                go.GetComponentsInChildren<NetworkObject>(true, _networkObjectListA);
                //If network objects are found.
                if (_networkObjectListA.Count > 0)
                {
                    //Add only the first networkobject 
                    if (firstOnly)
                    {
                        /* The easiest way to see if a nob is nested is to
                         * get nobs in parent and if the count is greater than 1, then
                         * it is nested. The technique used here isn't exactly fast but
                         * it will only occur during scene loads, so I'm trading off speed
                         * for effort and readability. */
                        foreach (NetworkObject nob in _networkObjectListA)
                        {
                            nob.GetComponentsInParent<NetworkObject>(true, _networkObjectListB);
                            //No extra nobs, only this one.
                            if (_networkObjectListB.Count == 1)
                                nobCache.AddValue(nob);
                        }
                    }
                    //Not first only, add them all.
                    else
                    {
                        nobCache.AddValues(_networkObjectListA);
                    }

                }
            }
        }

    }

}