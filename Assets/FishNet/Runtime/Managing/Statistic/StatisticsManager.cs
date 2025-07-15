using UnityEngine;

namespace FishNet.Managing.Statistic
{
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/StatisticsManager")]
    public class StatisticsManager : MonoBehaviour
    {
        /// <summary>
        /// True to operate while in release. This may cause allocations and impact performance.
        /// </summary>
        [Tooltip("True to operate while in release. This may cause allocations and impact performance.")]
        [SerializeField]
        private bool _runInRelease;
        /// <summary>
        /// Statistics for NetworkTraffic.
        /// </summary>
        [Tooltip("Statistics for NetworkTraffic.")]
        [SerializeField]
        private NetworkTrafficStatistics _networkTraffic;

        internal void InitializeOnce_Internal(NetworkManager manager)
        {
#if (!UNITY_EDITOR && !DEVELOPMENT_BUILD) || UNITY_SERVER
            if (!_runInRelease)
            {
                _networkTraffic = null;
                return;
            }
#endif
            InstantiateNetworkTrafficIfNeeded();

            _networkTraffic.InitializeOnce_Internal(manager);
        }

        public bool TryGetNetworkTrafficStatistics(out NetworkTrafficStatistics statistics)
        {
            InstantiateNetworkTrafficIfNeeded();

            if (_networkTraffic.IsEnabled())
                statistics = _networkTraffic;
            else
                statistics = null;

            return statistics != null;
        }

        /// <summary>
        /// Instantiates NetworkTraffic if currently null.
        /// </summary>
        private void InstantiateNetworkTrafficIfNeeded()
        {
            if (_networkTraffic == null)
                _networkTraffic = new();
        }
    }
}