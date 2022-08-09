using UnityEngine;

namespace FishNet.Managing.Statistic
{

    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/StatisticsManager")]
    public class StatisticsManager : MonoBehaviour
    {
        /// <summary>
        /// Statistics for NetworkTraffic.
        /// </summary>
        public NetworkTraficStatistics NetworkTraffic = new NetworkTraficStatistics();

        internal void InitializeOnceInternal(NetworkManager manager)
        {
            NetworkTraffic.InitializeOnceInternal(manager);
        }
  
    }

}