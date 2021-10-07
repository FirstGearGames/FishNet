using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using UnityEngine;
using FishNet.Managing.Logging;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishNet
{

    public static class InstanceFinder
    {

        #region Public.
        /// <summary>
        /// Returns the first found NetworkManager instance.
        /// </summary>
        public static NetworkManager NetworkManager
        {
            get
            {
                if (_networkManager == null)
                {
                    NetworkManager[] managers = GameObject.FindObjectsOfType<NetworkManager>();
                    if (managers.Length > 0)
                    {
                        if (managers.Length > 1)
                        {
                            if (managers[0].CanLog(LoggingType.Warning))
                                Debug.LogWarning($"Multiple NetworkManagers found, the first result will be returned. If you only wish to have one NetworkManager then uncheck 'Allow Multiple' within your NetworkManagers.");
                        }

                        _networkManager = managers[0];
                    }
                    else
                    {

                        /* If in editor check if exiting play mode, and if
                         * so then exit method. This is to ensure errors aren't
                         * thrown when users try to use NetworkManager within OnDestroy
                         * while exiting playmode. */
#if UNITY_EDITOR
                        if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.isPlaying)
                            return null;
#endif                        
                        Debug.LogError($"NetworkManager not found in any open scenes.");
                    }
                }

                return _networkManager;
            }
        }

        /// <summary>
        /// Returns the first instance of ServerManager.
        /// </summary>
        public static ServerManager ServerManager
        {
            get
            {
                NetworkManager nm = NetworkManager;
                return (nm == null) ? null : nm.ServerManager;
            }
        }

        /// <summary>
        /// Returns the first instance of ClientManager.
        /// </summary>
        public static ClientManager ClientManager
        {
            get
            {
                NetworkManager nm = NetworkManager;
                return (nm == null) ? null : nm.ClientManager;
            }
        }

        /// <summary>
        /// Returns the first instance of TransportManager.
        /// </summary>
        public static TransportManager TransportManager
        {
            get
            {
                NetworkManager nm = NetworkManager;
                return (nm == null) ? null : nm.TransportManager;
            }
        }

        /// <summary>
        /// Returns the first instance of TransportManager.
        /// </summary>
        public static TimeManager TimeManager
        {
            get
            {
                NetworkManager nm = NetworkManager;
                return (nm == null) ? null : nm.TimeManager;
            }
        }

        public static SceneManager SceneManager
        {
            get
            {
                NetworkManager nm = NetworkManager;
                return (nm == null) ? null : nm.SceneManager;
            }
        }

        /// <summary>
        /// True if the server is active.
        /// </summary>
        public static bool IsServer => (NetworkManager == null) ? false : NetworkManager.IsServer;
        /// <summary>
        /// True if the client is active and authenticated.
        /// </summary>
        public static bool IsClient => (NetworkManager == null) ? false : NetworkManager.IsClient;
        /// <summary>
        /// True if client and server are active.
        /// </summary>
        public static bool IsHost => (NetworkManager == null) ? false : (ServerManager.Started && ClientManager.Started);
        #endregion

        #region Private.
        /// <summary>
        /// NetworkManager instance.
        /// </summary>
        private static NetworkManager _networkManager;
        #endregion


    }


}