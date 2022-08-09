using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Utility.Performance
{

    public static class GetNonAlloc
    {
        /// <summary>
        /// 
        /// </summary>
        private static List<Transform> _transformList = new List<Transform>();
        /// <summary>
        /// 
        /// </summary>
        private static List<NetworkBehaviour> _networkBehavioursList = new List<NetworkBehaviour>();

        /// <summary>
        /// Gets all NetworkBehaviours on a transform.
        /// </summary>
        public static List<NetworkBehaviour> GetNetworkBehaviours(this Transform t)
        {
            t.GetComponents(_networkBehavioursList);
            return _networkBehavioursList;
        }

        /// <summary>
        /// Gets all transforms on transform and it's children.
        /// </summary>
        public static List<Transform> GetTransformsInChildrenNonAlloc(this Transform t, bool includeInactive = false)
        {
            t.GetComponentsInChildren<Transform>(includeInactive, _transformList);
            return _transformList;
        }

    }

}