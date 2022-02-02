#if UNITY_EDITOR
using FishNet.Editing;
#endif
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using UnityEngine;
using FishNet.Managing.Scened;
using FishNet.Authenticating;
using FishNet.Object;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using System.Collections.Generic;
using System;
using FishNet.Managing.Observing;
using System.Linq;
using FishNet.Managing.Debugging;
using FishNet.Managing.Object;
using FishNet.Transporting;
using FishNet.Utility.Extension;

namespace FishNet.Managing
{
    /// <summary>
    /// Acts as a container for all things related to your networking session.
    /// </summary>
    [DefaultExecutionOrder(short.MinValue)]
    [DisallowMultipleComponent]
    public sealed partial class NetworkManager : MonoBehaviour
    {
        #region Types.
        /// <summary>
        /// How to persist with multiple NetworkManagers.
        /// </summary>
        public enum PersistenceType
        {
            /// <summary>
            /// Destroy any new NetworkManagers.
            /// </summary>
            DestroyNewest,
            /// <summary>
            /// Destroy previous NetworkManager when a new NetworkManager occurs.
            /// </summary>
            DestroyOldest,
            /// <summary>
            /// Allow multiple NetworkManagers, do not destroy any automatically.
            /// </summary>
            AllowMultiple
        }

        #endregion

        #region Public.
        /// <summary>
        /// 
        /// </summary>
        private static List<NetworkManager> _instances = new List<NetworkManager>();
        /// <summary>
        /// Currently initialized NetworkManagers.
        /// </summary>
        public static IReadOnlyCollection<NetworkManager> Instances
        {
            get
            {
                /* Remove null instances of NetworkManager.
                * This shouldn't happen because instances are removed
                * OnDestroy but none the less something is causing
                * it. */
                for (int i = 0; i < _instances.Count; i++)
                {
                    if (_instances[i] == null)
                    {
                        _instances.RemoveAt(i);
                        i--;
                    }
                }
                return _instances;
            }
        }

        /// <summary>
        /// True if server is active.
        /// </summary>
        public bool IsServer => ServerManager.Started;
        /// <summary>
        /// True if only the server is active.
        /// </summary>
        public bool IsServerOnly => (IsServer && !IsClient);
        /// <summary>
        /// True if the client is active and authenticated.
        /// </summary>
        public bool IsClient => (ClientManager.Started && ClientManager.Connection.Authenticated);
        /// <summary>
        /// True if only the client is active and authenticated.
        /// </summary>
        public bool IsClientOnly => (!IsServer && IsClient);
        /// <summary>
        /// True if client and server are active.
        /// </summary>
        public bool IsHost => (IsServer && IsClient);
        /// <summary>
        /// True if client nor server are active.
        /// </summary>
        public bool IsOffline => (!IsServer && !IsClient);
        /// <summary>
        /// ServerManager for this NetworkManager.
        /// </summary>
        public ServerManager ServerManager { get; private set; }
        /// <summary>
        /// ClientManager for this NetworkManager.
        /// </summary>
        public ClientManager ClientManager { get; private set; }
        /// <summary>
        /// TransportManager for this NetworkManager.
        /// </summary>
        public TransportManager TransportManager { get; private set; }
        /// <summary>
        /// TimeManager for this NetworkManager.
        /// </summary>
        public TimeManager TimeManager { get; private set; }
        /// <summary>
        /// SceneManager for this NetworkManager.
        /// </summary>
        public SceneManager SceneManager { get; private set; }
        /// <summary>
        /// ObserverManager for this NetworkManager.
        /// </summary>
        public ObserverManager ObserverManager { get; private set; }
        /// <summary>
        /// Authenticator for this NetworkManager. May be null if no Authenticator is used.
        /// </summary>
        public Authenticator Authenticator { get; private set; }
        /// <summary>
        /// DebugManager for this NetworkManager.
        /// </summary>
        public DebugManager DebugManager { get; private set; }
        /// <summary>
        /// An empty connection reference. Used when a connection cannot be found to prevent object creation.
        /// </summary>
        [APIExclude]
        public static NetworkConnection EmptyConnection { get; private set; } = new NetworkConnection();
        #endregion

        #region Internal.
        /// <summary>
        /// Starting index for RpcLinks.
        /// </summary>
        internal static ushort StartingRpcLinkIndex;
        #endregion

        #region Serialized.
        /// <summary>
        /// True to refresh the DefaultPrefabObjects collection whenever the editor enters play mode. This is an attempt to alleviate the DefaultPrefabObjects scriptable object not refreshing when using multiple editor applications such as ParrelSync.
        /// </summary>
        [Tooltip("True to refresh the DefaultPrefabObjects collection whenever the editor enters play mode. This is an attempt to alleviate the DefaultPrefabObjects scriptable object not refreshing when using multiple editor applications such as ParrelSync.")]
        [SerializeField]
        private bool _refreshDefaultPrefabs = true;
        /// <summary>
        /// True to have your application run while in the background.
        /// </summary>
        [Tooltip("True to have your application run while in the background.")]
        [SerializeField]
        private bool _runInBackground = true;
        /// <summary>
        /// True to make this instance DontDestroyOnLoad. This is typical if you only want one NetworkManager.
        /// </summary>
        [Tooltip("True to make this instance DontDestroyOnLoad. This is typical if you only want one NetworkManager.")]
        [SerializeField]
        private bool _dontDestroyOnLoad = true;
        /// <summary>
        /// How to persist when other NetworkManagers are introduced.
        /// </summary>
        [Tooltip("How to persist when other NetworkManagers are introduced.")]
        [SerializeField]
        private PersistenceType _persistence = PersistenceType.DestroyNewest;
        #endregion

        #region Private.
        /// <summary>
        /// True if this NetworkManager can persist after Awake checks.
        /// </summary>
        private bool _canPersist;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum framerate allowed.
        /// </summary>
        internal const ushort MAXIMUM_FRAMERATE = 9999;
        #endregion


        private void Awake()
        {
            InitializeLogging();
            if (StartingRpcLinkIndex == 0)
                StartingRpcLinkIndex = (ushort)(EnumFN.GetHighestValue<PacketId>() + 1);

#if UNITY_EDITOR
            /* If first instance then force
             * default prefabs to repopulate.
             * This is only done in editor because
             * cloning tools sometimes don't synchronize
             * scriptable object changes, which is what
             * the default prefabs is. */
            if (_refreshDefaultPrefabs && SpawnablePrefabs != null && SpawnablePrefabs is DefaultPrefabObjects dpo)
                dpo.PopulateDefaultPrefabs(false);
#endif

            _canPersist = CanInitialize();
            if (!_canPersist)
                return;

            if (TryGetComponent<NetworkObject>(out _))
            {
                if (CanLog(LoggingType.Error))
                    Debug.LogError($"NetworkObject component found on the NetworkManager object {gameObject.name}. This is not allowed and will cause problems. Remove the NetworkObject component from this object.");
            }

            SpawnablePrefabs.InitializePrefabRange(0);
            SetDontDestroyOnLoad();
            SetRunInBackground();
            AddDebugManager();
            AddTransportManager();
            AddServerAndClientManagers();
            AddTimeManager();
            AddSceneManager();
            AddObserverManager();
            AddRollbackManager();
            InitializeComponents();

            _instances.Add(this);

            ServerManager.StartForHeadless();
        }

        private void OnDestroy()
        {
            _instances.Remove(this);
        }

        /// <summary>
        /// Initializes components. To be called after all components are added.
        /// </summary>
        private void InitializeComponents()
        {
            TimeManager.InitializeOnce(this);
            TimeManager.OnLateUpdate += TimeManager_OnLateUpdate;
            SceneManager.InitializeOnce(this);
            ServerManager.InitializeOnce(this);
            ClientManager.InitializeOnce(this);
            RollbackManager.InitializeOnce(this);
        }

        /// <summary>
        /// Updates the frame rate based on server and client status.
        /// </summary>
        internal void UpdateFramerate()
        {
            bool clientStarted = ClientManager.Started;
            bool serverStarted = ServerManager.Started;

            int frameRate;
            if (clientStarted && serverStarted)
                frameRate = Math.Max(ServerManager.FrameRate, ClientManager.FrameRate);
            else if (clientStarted)
                frameRate = ClientManager.FrameRate;
            else if (serverStarted)
                frameRate = ServerManager.FrameRate;
            else
                frameRate = MAXIMUM_FRAMERATE;

            /* Make sure framerate isn't set to 9999 on server.
             * If it is then default to tick rate. */
#if UNITY_SERVER
            if (frameRate == MAXIMUM_FRAMERATE)
                frameRate = TimeManager.TickRate;
#endif

            Application.targetFrameRate = frameRate;
        }

        /// <summary>
        /// Called when MonoBehaviours call LateUpdate.
        /// </summary>
        private void TimeManager_OnLateUpdate()
        {
            /* Some reason runinbackground becomes unset
            * or the setting goes ignored some times when it's set
            * in awake. Rather than try to fix or care why Unity
            * does this just set it in LateUpdate(or Update). */
            SetRunInBackground();
        }


        /// <summary>
        /// Returns if this NetworkManager can initialize.
        /// </summary>
        /// <returns></returns>
        private bool CanInitialize()
        {
            /* If allow multiple then any number of
             * NetworkManagers are allowed. Don't
             * automatically destroy any. */
            if (_persistence == PersistenceType.AllowMultiple)
                return true;

            List<NetworkManager> instances = Instances.ToList();
            //If at least one manager is already instantiated/initialized.
            if (instances.Count > 0)
            {
                GameObject target = null;
                //If destroy newest.
                if (_persistence == PersistenceType.DestroyNewest)
                {
                    target = gameObject;
                }
                //If destroy oldest.
                else
                {
                    NetworkManager previous = instances[0];
                    if (previous != null)
                        target = previous.gameObject;
                }

                bool result = (target != gameObject);
                if (target != null)
                {
                    if (CanLog(LoggingType.Common))
                        Debug.Log($"NetworkManager on object {gameObject.name} is is being destroyed due to persistence type {_persistence}. Another NetworkManager already exists on object {instances[0].gameObject.name}.");
                    Destroy(gameObject);
                }

                return result;
            }
            //First manager, will not be destroyed.
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Sets DontDestroyOnLoad if configured to.
        /// </summary>
        private void SetDontDestroyOnLoad()
        {
            if (_dontDestroyOnLoad)
                DontDestroyOnLoad(this);
        }

        /// <summary>
        /// Sets Application.runInBackground to runInBackground.
        /// </summary>
        private void SetRunInBackground()
        {
            Application.runInBackground = _runInBackground;
        }


        /// <summary>
        /// Adds DebugManager.
        /// </summary>
        private void AddDebugManager()
        {
            if (gameObject.TryGetComponent<DebugManager>(out DebugManager result))
                DebugManager = result;
            else
                DebugManager = gameObject.AddComponent<DebugManager>();
        }

        /// <summary>
        /// Adds TransportManager.
        /// </summary>
        private void AddTransportManager()
        {
            if (gameObject.TryGetComponent<TransportManager>(out TransportManager result))
                TransportManager = result;
            else
                TransportManager = gameObject.AddComponent<TransportManager>();
        }

        /// <summary>
        /// Adds TimeManager.
        /// </summary>
        private void AddTimeManager()
        {
            if (gameObject.TryGetComponent<TimeManager>(out TimeManager result))
                TimeManager = result;
            else
                TimeManager = gameObject.AddComponent<TimeManager>();
        }


        /// <summary>
        /// Adds SceneManager.
        /// </summary>
        private void AddSceneManager()
        {
            if (gameObject.TryGetComponent<SceneManager>(out SceneManager result))
                SceneManager = result;
            else
                SceneManager = gameObject.AddComponent<SceneManager>();
        }

        /// <summary>
        /// Adds ObserverManager.
        /// </summary>
        private void AddObserverManager()
        {
            if (gameObject.TryGetComponent<ObserverManager>(out ObserverManager result))
                ObserverManager = result;
            else
                ObserverManager = gameObject.AddComponent<ObserverManager>();
        }


        /// <summary>
        /// Adds and assigns NetworkServer and NetworkClient if they are not already setup.
        /// </summary>
        private void AddServerAndClientManagers()
        {
            //Add servermanager.
            if (gameObject.TryGetComponent<ServerManager>(out ServerManager sm))
                ServerManager = sm;
            else
                ServerManager = gameObject.AddComponent<ServerManager>();

            //Add clientmanager.
            if (gameObject.TryGetComponent<ClientManager>(out ClientManager cm))
                ClientManager = cm;
            else
                ClientManager = gameObject.AddComponent<ClientManager>();
        }

        #region Editor.
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (SpawnablePrefabs == null)
                Reset();
        }
        private void Reset()
        {
            if (SpawnablePrefabs == null)
            {
                SpawnablePrefabs = DefaultPrefabsFinder.GetDefaultPrefabsFile(out _);
                //If found.
                if (SpawnablePrefabs != null)
                {
                    if (CanLog(LoggingType.Common))
                        Debug.Log($"NetworkManager on {gameObject.name} is using the default prefabs collection.");
                }
            }
        }
#endif

        #endregion

    }


}