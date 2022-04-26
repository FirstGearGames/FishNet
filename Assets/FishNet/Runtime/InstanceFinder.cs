using FishNet.Component.ColliderRollback;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Logging;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Utility;
using System.Linq;
using UnityEngine;

namespace FishNet
{

    /// <summary>
    /// Used to globally get information from the first found instance of NetworkManager.
    /// </summary>
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
                    int managersCount = NetworkManager.Instances.Count;
                    //At least one manager.
                    if (managersCount > 0)
                    {
                        _networkManager = NetworkManager.Instances.First();
                        if (managersCount > 1)
                        {
                            if (_networkManager.CanLog(LoggingType.Warning))
                                Debug.LogWarning($"Multiple NetworkManagers found, the first result will be returned. If you only wish to have one NetworkManager then uncheck 'Allow Multiple' within your NetworkManagers.");
                        }
                    }
                    //No managers.
                    else
                    {
                        //If application is quitting return null without logging.
                        if (ApplicationState.IsQuitting())
                            return null;

                        Debug.Log($"NetworkManager not found in any open scenes.");
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
        /// Returns the first instance of TimeManager.
        /// </summary>
        public static TimeManager TimeManager
        {
            get
            {
                NetworkManager nm = NetworkManager;
                return (nm == null) ? null : nm.TimeManager;
            }
        }

        /// <summary>
        /// Returns the first instance of SceneManager.
        /// </summary>
        public static SceneManager SceneManager
        {
            get
            {
                NetworkManager nm = NetworkManager;
                return (nm == null) ? null : nm.SceneManager;
            }
        }
        /// <summary>
        /// Returns the first instance of RollbackManager.
        /// </summary>
        public static RollbackManager RollbackManager
        {
            get
            {
                NetworkManager nm = NetworkManager;
                return (nm == null) ? null : nm.RollbackManager;
            }
        }

        /// <summary>
        /// True if the server is active.
        /// </summary>
        public static bool IsServer => (NetworkManager == null) ? false : NetworkManager.IsServer;
        /// <summary>
        /// True if only the server is active.
        /// </summary>
        public static bool IsServerOnly => (NetworkManager == null) ? false : NetworkManager.IsServerOnly;
        /// <summary>
        /// True if the client is active and authenticated.
        /// </summary>
        public static bool IsClient => (NetworkManager == null) ? false : NetworkManager.IsClient;
        /// <summary>
        /// True if only the client is active and authenticated.
        /// </summary>
        public static bool IsClientOnly => (NetworkManager == null) ? false : NetworkManager.IsClientOnly;
        /// <summary>
        /// True if client and server are active.
        /// </summary>
        public static bool IsHost => (NetworkManager == null) ? false : NetworkManager.IsHost;
        /// <summary>
        /// True if client nor server are active.
        /// </summary>
        public static bool IsOffline => (NetworkManager == null) ? true : (!NetworkManager.IsServer && !NetworkManager.IsClient);
        #endregion

        #region Private.
        /// <summary>
        /// NetworkManager instance.
        /// </summary>
        private static NetworkManager _networkManager;
        #endregion


    }


}