using FishNet.Managing.Logging;
using UnityEngine;

namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {

        /// <summary>
        /// True if can log for loggingType.
        /// </summary>
        /// <param name="loggingType"></param>
        /// <returns></returns>
        public bool CanLog(LoggingType loggingType)
        {
            return (NetworkManager == null) ? false : NetworkManager.CanLog(loggingType);
        }
    }


}