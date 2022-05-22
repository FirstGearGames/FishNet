using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace FishNet.Managing.Scened
{
    /// <summary>
    /// Handles loading, unloading, and scene visibility for clients.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneManager : MonoBehaviour
    {

        #region Public.
        /// <summary>
        /// Called after the active scene has been set, immediately after scene loads. This will occur before NetworkBehaviour callbacks run for the scene's objects.
        /// </summary>
        public event Action OnActiveSceneSet;
        /// <summary>
        /// Called when a client loads initial scenes after connecting. Boolean will be true if asServer.
        /// </summary>
        public event Action<NetworkConnection, bool> OnClientLoadedStartScenes;
        /// <summary>
        /// Called when a scene change queue has begun. This will only call if a scene has succesfully begun to load or unload. The queue may process any number of scene events. For example: if a scene is told to unload while a load is still in progress, then the unload will be placed in the queue.
        /// </summary>
        public event Action OnQueueStart;
        /// <summary>
        /// Called when the scene queue is emptied.
        /// </summary>
        public event Action OnQueueEnd;
        /// <summary>
        /// Called when a scene load starts.
        /// </summary>
        public event Action<SceneLoadStartEventArgs> OnLoadStart;
        /// <summary>
        /// Called when completion percentage changes while loading a scene. Value is between 0f and 1f, while 1f is 100% done. Can be used for custom progress bars when loading scenes.
        /// </summary>
        public event Action<SceneLoadPercentEventArgs> OnLoadPercentChange;
        /// <summary>
        /// Called when a scene load ends.
        /// </summary>
        public event Action<SceneLoadEndEventArgs> OnLoadEnd;
        /// <summary>
        /// Called when a scene unload starts.
        /// </summary>
        public event Action<SceneUnloadStartEventArgs> OnUnloadStart;
        /// <summary>
        /// Called when a scene unload ends.
        /// </summary>
        public event Action<SceneUnloadEndEventArgs> OnUnloadEnd;
        /// <summary>
        /// Called when a client presence changes within a scene, before the server rebuilds observers.
        /// </summary>
        public event Action<ClientPresenceChangeEventArgs> OnClientPresenceChangeStart;
        /// <summary>
        /// Called when a client presence changes within a scene, after the server rebuilds observers.
        /// </summary>
        public event Action<ClientPresenceChangeEventArgs> OnClientPresenceChangeEnd;
        /// <summary>
        /// Connections within each scene.
        /// </summary>
        public Dictionary<Scene, HashSet<NetworkConnection>> SceneConnections { get; private set; } = new Dictionary<Scene, HashSet<NetworkConnection>>();
        #endregion

        #region Internal.
        /// <summary>
        /// Called after the active scene has been set, immediately after scene loads.
        /// </summary>
        internal event Action OnActiveSceneSetInternal;
        #endregion

        #region Private.
        /// <summary>
        /// NetworkManager for this script.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// ServerManager for this script.
        /// </summary>
        private ServerManager _serverManager => _networkManager.ServerManager;
        /// <summary>
        /// ClientManager for this script.
        /// </summary>
        private ClientManager _clientManager => _networkManager.ClientManager;
        /// <summary>
        /// Scenes which are currently loaded as networked scenes. All players should have networked scenes loaded.
        /// </summary>
        private string[] _globalScenes = new string[0];
        /// <summary>
        /// Lastest SceneLoadData for a global load.
        /// </summary>
        private SceneLoadData _globalSceneLoadData = new SceneLoadData();
        /// <summary>
        /// Scenes to load or unload, in order.
        /// </summary>
        private List<object> _queuedOperations = new List<object>();
        /// <summary>
        /// Scenes which must be manually unloaded, even when emptied.
        /// </summary>
        private HashSet<Scene> _manualUnloadScenes = new HashSet<Scene>();
        /// <summary>
        /// Scene containing moved objects when changing single scene. On client this will contain all objects moved until the server destroys them.
        /// The network only sends spawn messages once per-client, per server side scene load. If a scene load is performed only for specific connections
        /// then the server is not resetting their single scene, but rather the single scene for those connections only. Because of this, any objects
        /// which are to be moved will not receive a second respawn message, as they are never destroyed on server, only on client.
        /// While on server only this scene contains objects being moved temporarily, before being moved to the new scene.
        /// </summary>
        private Scene _movedObjectsScene;
        /// <summary>
        /// Scene containing objects awaiting to be destroyed by the client-host.
        /// This is required when unloading scenes where the client-host has visibility.
        /// Otherwise the objects would become destroyed when the scene unloads on the server
        /// which would cause missing networkobjects on clients when receiving despawn messages.
        /// </summary>
        private Scene _delayedDestroyScene;
        /// <summary>
        /// A scene to be set as the active scene where there are no global scenes.
        /// This is used to prevent connection scenes and MovedObjectsScene from becoming the active scene.
        /// </summary>
        private Scene _fallbackActiveScene;
        /// <summary>
        /// Becomes true when when a scene first successfully begins to load or unload. Value is reset to false when the scene queue is emptied.
        /// </summary>
        private bool _sceneQueueStartInvoked;
        /// <summary>
        /// Objects being moved from MovedObjects scene to another. 
        /// </summary>
        private List<GameObject> _movingObjects = new List<GameObject>();
        /// <summary>
        /// How many scene load confirmations the server is expecting from a client.
        /// Unloads do not need to be checked because server does not require confirmation for those.
        /// This is used to prevent attacks.
        /// </summary>
        private Dictionary<NetworkConnection, int> _pendingClientSceneChanges = new Dictionary<NetworkConnection, int>();
        #endregion

        #region Consts.
        /// <summary>
        /// String to use when scene data used to load is invalid.
        /// </summary>
        private const string INVALID_SCENELOADDATA = "One or more datas in SceneLoadData are invalid.This generally occurs when calling this method without specifying any scenes or when data fields are null.";
        /// <summary>
        /// String to use when scene data used to unload is invalid.
        /// </summary>
        private const string INVALID_SCENEUNLOADDATA = "One or more datas in SceneLoadData are invalid.This generally occurs when calling this method without specifying any scenes or when data fields are null.";
        #endregion

        #region Unity callbacks and initialization.
        private void Awake()
        {
            UnitySceneManager.sceneUnloaded += SceneManager_SceneUnloaded;
        }

        private void Start()
        {
            //No need to unregister since managers are on the same object.
            _networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            _clientManager.RegisterBroadcast<LoadScenesBroadcast>(OnLoadScenes);
            _clientManager.RegisterBroadcast<UnloadScenesBroadcast>(OnUnloadScenes);
            _serverManager.RegisterBroadcast<ClientScenesLoadedBroadcast>(OnClientLoadedScenes);

            _clientManager.RegisterBroadcast<EmptyStartScenesBroadcast>(OnClientEmptyStartScenes);
            _serverManager.RegisterBroadcast<EmptyStartScenesBroadcast>(OnServerEmptyStartScenes);
        }

        private void OnDestroy()
        {
            UnitySceneManager.sceneUnloaded -= SceneManager_SceneUnloaded;
        }

        /// <summary>
        /// Called when the server connection state changes.
        /// </summary>
        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            //If no servers are started.
            if (!_networkManager.ServerManager.AnyServerStarted())
                ResetValues();

        }
        /// <summary>
        /// Resets as if first use.
        /// </summary>
        private void ResetValues()
        {
            SceneConnections.Clear();
            _globalScenes = new string[0];
            _globalSceneLoadData = new SceneLoadData();
            _queuedOperations.Clear();
            _manualUnloadScenes.Clear();
            _sceneQueueStartInvoked = false;
            _movingObjects.Clear();
        }

        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        private void ServerManager_OnRemoteConnectionState(NetworkConnection arg1, RemoteConnectionStateArgs arg2)
        {
            if (arg2.ConnectionState == RemoteConnectionStates.Stopped)
                ClientDisconnected(arg1);
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void InitializeOnceInternal(NetworkManager manager)
        {
            _networkManager = manager;
        }

        /// <summary>
        /// Received when a scene is unloaded.
        /// </summary>
        /// <param name="arg0"></param>
        private void SceneManager_SceneUnloaded(Scene scene)
        {
            if (!_networkManager.IsServer)
                return;

            /* Remove any unloaded scenes from local variables. This shouldn't
             * be needed if the user properly utilizes this scene manager,
             * but just incase, we don't want a memory leak. */
            SceneConnections.Remove(scene);
            _manualUnloadScenes.Remove(scene);
            RemoveFromGlobalScenes(scene);
        }
        #endregion

        #region Initial synchronizing.
        /// <summary>
        /// Invokes OnClientLoadedStartScenes if connection just loaded start scenes.
        /// </summary>
        /// <param name="connection"></param>
        private void TryInvokeLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            if (connection.SetLoadedStartScenes(asServer))
                OnClientLoadedStartScenes?.Invoke(connection, asServer);
        }

        /// <summary>
        /// Called when authenitcator has concluded a result for a connection. Boolean is true if authentication passed, false if failed. This invokes before OnClientAuthenticated so FishNet may run operations on authenticated clients before user code does.
        /// </summary>
        /// <param name="obj"></param>
        internal void OnClientAuthenticated(NetworkConnection connection)
        {
            AddPendingLoad(connection);

            //No global scenes to load.
            if (_globalScenes.Length == 0)
            {
                EmptyStartScenesBroadcast msg = new EmptyStartScenesBroadcast();
                connection.Broadcast(msg);
            }
            else
            {
                SceneLoadData sld = new SceneLoadData(_globalScenes);
                sld.Params = _globalSceneLoadData.Params;
                sld.Options = _globalSceneLoadData.Options;
                sld.ReplaceScenes = _globalSceneLoadData.ReplaceScenes;

                LoadQueueData qd = new LoadQueueData(SceneScopeTypes.Global, new NetworkConnection[0], sld, _globalScenes, false);
                //Send message to load the networked scenes.
                LoadScenesBroadcast msg = new LoadScenesBroadcast()
                {
                    QueueData = qd
                };

                connection.Broadcast(msg, true);
            }
        }

        /// <summary>
        /// Received on client when the server has no start scenes.
        /// </summary>
        private void OnClientEmptyStartScenes(EmptyStartScenesBroadcast msg)
        {
            _clientManager.Broadcast(msg);
        }
        /// <summary>
        /// Received on server when client confirms there are no start scenes.
        /// </summary>
        private void OnServerEmptyStartScenes(NetworkConnection conn, EmptyStartScenesBroadcast msg)
        {
            //Already received, shouldn't be happening again.
            if (conn.LoadedStartScenes)
            {
                if (_networkManager.CanLog(LoggingType.Common))
                    Debug.LogError($"Received multiple EmptyStartSceneBroadcast from connectionId {conn.ClientId}. Connection will be kicked immediately.");
                _networkManager.TransportManager.Transport.StopConnection(conn.ClientId, true);
            }
            else
            {
                OnClientLoadedScenes(conn, new ClientScenesLoadedBroadcast());
            }
        }
        #endregion

        #region Player disconnect.
        /// <summary>
        /// Received when a player disconnects from the server.
        /// </summary>
        /// <param name="conn"></param> //finish.
        private void ClientDisconnected(NetworkConnection conn)
        {
            _pendingClientSceneChanges.Remove(conn);
            /* Remove connection from all scenes. While doing so check
             * if scene should be unloaded provided there are no more clients
             * in the scene, and it's set to automatically unload. This situation is a bit
             * unique since a client disconnect happens outside the manager, so there
             * isn't much code we can re-use to perform this operation. */
            List<Scene> scenesToUnload = new List<Scene>();
            //Current active scene.
            Scene activeScene = UnitySceneManager.GetActiveScene();
            foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> item in SceneConnections)
            {
                Scene scene = item.Key;
                HashSet<NetworkConnection> hs = item.Value;

                bool removed = hs.Remove(conn);
                /* If no more observers for scene, not a global scene, and not to be manually unloaded
                 * then remove scene from SceneConnections and unload it. */
                if (removed && hs.Count == 0 &&
                    !IsGlobalScene(scene) && !_manualUnloadScenes.Contains(scene) &&
                    (scene != activeScene))
                    scenesToUnload.Add(scene);
            }

            //If scenes should be unloaded.
            if (scenesToUnload.Count > 0)
            {
                foreach (Scene s in scenesToUnload)
                    SceneConnections.Remove(s);
                SceneUnloadData sud = new SceneUnloadData(SceneLookupData.CreateData(scenesToUnload));
                UnloadConnectionScenes(new NetworkConnection[0], sud);
            }
        }
        #endregion

        #region Server received messages.
        /// <summary>
        /// Received on server when a client loads scenes.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        private void OnClientLoadedScenes(NetworkConnection conn, ClientScenesLoadedBroadcast msg)
        {
            int pendingLoads;
            _pendingClientSceneChanges.TryGetValueIL2CPP(conn, out pendingLoads);

            //There's no loads or unloads pending, kick client.
            if (pendingLoads == 0)
            {
                if (_networkManager.CanLog(LoggingType.Common))
                    Debug.LogError($"Received excessive ClientScenesLoadedBroadcast from connectionId {conn.ClientId}. Connection will be kicked immediately.");
                _networkManager.TransportManager.Transport.StopConnection(conn.ClientId, true);

                return;
            }
            //If there is a load pending then update pending count.
            else
            {
                pendingLoads--;
                if (pendingLoads == 0)
                    _pendingClientSceneChanges.Remove(conn);
                else
                    _pendingClientSceneChanges[conn] = pendingLoads;
            }

            if (!Comparers.IsDefault(msg))
            {
                foreach (SceneLookupData item in msg.SceneLookupDatas)
                {
                    Scene s = item.GetScene(out _);
                    if (s.IsValid())
                        AddConnectionToScene(conn, s);
                }
            }

            TryInvokeLoadedStartScenes(conn, true);
        }
        #endregion

        #region Events.
        /// <summary>
        /// Checks if OnQueueStart should invoke, and if so invokes.
        /// </summary>
        private void TryInvokeOnQueueStart()
        {
            if (_sceneQueueStartInvoked)
                return;

            _sceneQueueStartInvoked = true;
            OnQueueStart?.Invoke();
        }
        /// <summary>
        /// Checks if OnQueueEnd should invoke, and if so invokes.
        /// </summary>
        private void TryInvokeOnQueueEnd()
        {
            if (!_sceneQueueStartInvoked)
                return;

            _sceneQueueStartInvoked = false;
            OnQueueEnd?.Invoke();
        }
        /// <summary>
        /// Invokes that a scene load has started. Only called when valid scenes will be loaded.
        /// </summary>
        /// <param name="qd"></param>
        private void InvokeOnSceneLoadStart(LoadQueueData qd)
        {
            TryInvokeOnQueueStart();
            OnLoadStart?.Invoke(new SceneLoadStartEventArgs(qd));
        }
        /// <summary>
        /// Invokes that a scene load has ended. Only called after a valid scene has loaded.
        /// </summary>
        /// <param name="qd"></param>
        private void InvokeOnSceneLoadEnd(LoadQueueData qd, List<string> requestedLoadScenes, List<Scene> loadedScenes)
        {
            //Make new list to not destroy original data.
            List<string> skippedScenes = requestedLoadScenes.ToList();
            //Remove loaded scenes from requested scenes.
            for (int i = 0; i < loadedScenes.Count; i++)
                skippedScenes.Remove(loadedScenes[i].name);

            SceneLoadEndEventArgs args = new SceneLoadEndEventArgs(qd, loadedScenes.ToArray(), skippedScenes.ToArray());
            OnLoadEnd?.Invoke(args);
        }
        /// <summary>
        /// Invokes that a scene unload has started. Only called when valid scenes will be unloaded.
        /// </summary>
        /// <param name="sqd"></param>
        private void InvokeOnSceneUnloadStart(UnloadQueueData sqd)
        {
            TryInvokeOnQueueStart();
            OnUnloadStart?.Invoke(new SceneUnloadStartEventArgs(sqd));
        }
        /// <summary>
        /// Invokes that a scene unload has ended. Only called after a valid scene has unloaded.
        /// </summary>
        /// <param name="sqd"></param>
        private void InvokeOnSceneUnloadEnd(UnloadQueueData sqd, List<Scene> unloadedScenes)
        {
            int[] handles = new int[unloadedScenes.Count];
            OnUnloadEnd?.Invoke(new SceneUnloadEndEventArgs(sqd, handles));
        }
        /// <summary>
        /// Invokes when completion percentage changes while unloading or unloading a scene. Value is between 0f and 1f, while 1f is 100% done.
        /// </summary>
        /// <param name="value"></param>
        private void InvokeOnScenePercentChange(LoadQueueData qd, float value)
        {
            value = Mathf.Clamp(value, 0f, 1f);
            SceneLoadPercentEventArgs slp = new SceneLoadPercentEventArgs(qd, value);
            OnLoadPercentChange?.Invoke(slp);
        }
        #endregion

        #region Scene queue processing.
        /// <summary>
        /// Queues a load or unload operation and starts queue if needed.
        /// </summary>
        /// <param name="data"></param>
        private void QueueOperation(object data)
        {
            //Add to scene queue data.        
            _queuedOperations.Add(data);
            /* If only one entry then scene operations are not currently in progress.
             * Should there be more than one entry then scene operations are already 
             * occuring. The coroutine will automatically load in order. */

            if (_queuedOperations.Count == 1)
                StartCoroutine(__ProcessSceneQueue());
        }
        /// <summary>
        /// Processes queued scene operations.
        /// </summary>
        /// <param name="asServer"></param>
        /// <returns></returns>
        private IEnumerator __ProcessSceneQueue()
        {
            /* Queue start won't invoke unless a scene load or unload actually occurs.
             * For example: if a scene is already loaded, and nothing needs to be loaded,
             * queue start will not invoke. */

            while (_queuedOperations.Count > 0)
            {
                //If a load scene.
                if (_queuedOperations[0] is LoadQueueData)
                    yield return StartCoroutine(__LoadScenes());
                //If an unload scene.
                else if (_queuedOperations[0] is UnloadQueueData)
                    yield return StartCoroutine(__UnloadScenes());

                if (_queuedOperations.Count > 0)
                    _queuedOperations.RemoveAt(0);
            }

            TryInvokeOnQueueEnd();
        }
        #endregion

        #region LoadScenes
        /// <summary>
        /// Loads scenes on the server and for all clients. Future clients will automatically load these scenes.
        /// </summary>
        /// <param name="sceneLoadData">Data about which scenes to load.</param>
        public void LoadGlobalScenes(SceneLoadData sceneLoadData)
        {
            LoadGlobalScenesInternal(sceneLoadData, _globalScenes, true);
        }
        /// <summary>
        /// Adds to load scene queue.
        /// </summary>
        /// <param name="sceneLoadData"></param>
        /// <param name="asServer"></param>
        private void LoadGlobalScenesInternal(SceneLoadData sceneLoadData, string[] globalScenes, bool asServer)
        {
            if (!CanExecute(asServer, true))
                return;
            if (SceneDataInvalid(sceneLoadData, true))
                return;

            LoadQueueData lqd = new LoadQueueData(SceneScopeTypes.Global, new NetworkConnection[0], sceneLoadData, globalScenes, asServer);
            QueueOperation(lqd);
        }

        /// <summary>
        /// Loads scenes on server and tells connections to load them as well. Other connections will not load this scene.
        /// </summary>
        /// <param name="conn">Connections to load scenes for.</param>
        /// <param name="sceneLoadData">Data about which scenes to load.</param>
        public void LoadConnectionScenes(NetworkConnection conn, SceneLoadData sceneLoadData)
        {
            LoadConnectionScenes(new NetworkConnection[] { conn }, sceneLoadData);
        }
        /// <summary>
        /// Loads scenes on server and tells connections to load them as well. Other connections will not load this scene.
        /// </summary>
        /// <param name="conns">Connections to load scenes for.</param>
        /// <param name="sceneLoadData">Data about which scenes to load.</param>
        public void LoadConnectionScenes(NetworkConnection[] conns, SceneLoadData sceneLoadData)
        {
            LoadConnectionScenesInternal(conns, sceneLoadData, _globalScenes, true);
        }
        /// <summary>
        /// Loads scenes on server without telling clients to load the scenes.
        /// </summary>
        /// <param name="sceneLoadData">Data about which scenes to load.</param>
        public void LoadConnectionScenes(SceneLoadData sceneLoadData)
        {
            LoadConnectionScenesInternal(new NetworkConnection[0], sceneLoadData, _globalScenes, true);
        }

        /// <summary>
        /// Adds to load scene queue.
        /// </summary>
        /// <param name="sceneLoadData"></param>
        /// <param name="asServer"></param>
        private void LoadConnectionScenesInternal(NetworkConnection[] conns, SceneLoadData sceneLoadData, string[] globalScenes, bool asServer)
        {
            if (!CanExecute(asServer, true))
                return;
            if (SceneDataInvalid(sceneLoadData, true))
                return;

            LoadQueueData lqd = new LoadQueueData(SceneScopeTypes.Connections, conns, sceneLoadData, globalScenes, asServer);
            QueueOperation(lqd);
        }

        /// <summary>
        /// Returns if a NetworkObject can be moved.
        /// </summary>
        /// <param name="warn"></param>
        /// <returns></returns>
        private bool CanMoveNetworkObject(NetworkObject nob)
        {
            bool canLog = _networkManager.CanLog(LoggingType.Warning);

            //Null.
            if (nob == null)
            {
                if (canLog)
                    Debug.LogWarning($"NetworkObject is null.");
                return false;
            }
            //Not networked.
            if (!nob.IsNetworked)
            {
                if (canLog)
                    Debug.LogWarning($"NetworkObject {nob.name} cannot be moved as it is not networked.");
                return false;
            }

            //Not spawned.
            if (!nob.IsSpawned)
            {
                if (canLog)
                    Debug.LogWarning($"NetworkObject {nob.name} canot be moved as it is not spawned.");
                return false;
            }
            //SceneObject.
            if (nob.IsSceneObject)
            {
                if (canLog)
                    Debug.LogWarning($"NetworkObject {nob.name} cannot be moved as it is a scene object.");
                return false;
            }
            //Not root.
            if (nob.transform.parent != null)
            {
                if (canLog)
                    Debug.LogWarning($"NetworkObject {nob.name} cannot be moved because it is not the root object. Unity can only move root objects between scenes.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Loads a connection scene queue data. This behaves just like a networked scene load except it sends only to the specified connections, and it always loads as an additive scene on server.
        /// </summary>
        /// <returns></returns>
        private IEnumerator __LoadScenes()
        {
            LoadQueueData data = _queuedOperations[0] as LoadQueueData;

            //True if running as server.
            bool asServer = data.AsServer;
            //True if running as client, while network server is active.
            bool asHost = (!asServer && _networkManager.IsServer);

            //If connection went inactive.
            if (!ConnectionActive(asServer))
                yield break;

            if (data.SceneLoadData.SceneLookupDatas.Length == 0)
            {
                if (_networkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"No scenes specified to load.");
                yield break;
            }

            //True if replacing scenes with specified ones.
            ReplaceOption replaceScenes = data.SceneLoadData.ReplaceScenes;

            /* Immediately set new global scenes. If on client this is whatever
             * server passes in. This should be set even if scope type
             * is not global because clients might get a connection scene first.
             */
            if (!asServer)
            {
                if (!asHost)
                    _globalScenes = data.GlobalScenes;
            }
            /* However, if server, then only update global scenes if scope
             * is global. */
            else if (asServer && data.ScopeType == SceneScopeTypes.Global)
            {
                _globalSceneLoadData = data.SceneLoadData;
                string[] names = data.SceneLoadData.SceneLookupDatas.GetNames();
                //If replacing.
                if (replaceScenes != ReplaceOption.None)
                {
                    _globalScenes = names;
                }
                //Add onto.
                else
                {
                    int index = _globalScenes.Length;
                    Array.Resize(ref _globalScenes, _globalScenes.Length + names.Length);
                    Array.Copy(names, 0, _globalScenes, index, names.Length);
                }

                data.GlobalScenes = _globalScenes;
            }


            /* Scene queue data scenes.
            * All scenes in the scene queue data whether they will be loaded or not. */
            List<string> requestedLoadSceneNames = new List<string>();
            List<int> requestedLoadSceneHandles = new List<int>();

            /* Make a null filled array. This will be populated
             * using loaded scenes, or already loaded (eg cannot be loaded) scenes. */
            SceneLookupData[] broadcastLookupDatas = new SceneLookupData[data.SceneLoadData.SceneLookupDatas.Length];

            /* LoadableScenes and SceneReferenceDatas.
            /* Will contain scenes which may be loaded.
             * Scenes might not be added to loadableScenes
             * if for example loadOnlyUnloaded is true and
             * the scene is already loaded. */
            List<SceneLookupData> loadableScenes = new List<SceneLookupData>();
            for (int i = 0; i < data.SceneLoadData.SceneLookupDatas.Length; i++)
            {
                SceneLookupData sld = data.SceneLoadData.SceneLookupDatas[i];
                //Scene to load.
                bool byHandle;
                Scene s = sld.GetScene(out byHandle);
                //If found then add it to requestedLoadScenes.
                if (s.IsValid())
                {
                    requestedLoadSceneNames.Add(s.name);
                    if (byHandle)
                        requestedLoadSceneHandles.Add(s.handle);
                }

                if (CanLoadScene(data, sld))
                {
                    //Don't load if as host, server side would have loaded already.
                    if (!asHost)
                        loadableScenes.Add(sld);
                }
                //Only the server needs to find scene handles to send to client. Client will send these back to the server.
                else if (asServer)
                {
                    /* If here then scene cannot be loaded, which
                     * can only happen if the scene already exists.
                     * Find the scene using sld and set to datas. */
                    /* Set at the index of i. This way should the current
                     * SLD not be the first scene it won't fill the
                     * first slot in broadcastLookupDatas. This is important
                     * because the first slot is used for the single scene
                     * when using replace scenes. */
                    broadcastLookupDatas[i] = new SceneLookupData(s);
                }
            }

            /* Move identities
             * to holder scene to preserve them. 
             * Required if a single scene is specified. Cannot rely on
             * loadSingleScene since it is only true if the single scene
             * must be loaded, which may be false if it's already loaded on
             * the server. */
            //Do not run if running as client, and server is active. This would have already run as server.
            if (!asHost)
            {
                foreach (NetworkObject nob in data.SceneLoadData.MovedNetworkObjects)
                {
                    bool canMove = CanMoveNetworkObject(nob);
                    //NetworkObject might be null if client lost observation of it.
                    if (nob != null && CanMoveNetworkObject(nob))
                        UnitySceneManager.MoveGameObjectToScene(nob.gameObject, GetMovedObjectsScene());
                }
                /* Note: previously connection objects which were not in
                 * MovedNetworkObjects would be destroyed here by calling Despawn
                 * on them. This was required for Mirror but shouldn't be for FishNet.
                 * Code removed. */
            }

            /* Resetting SceneConnections. */
            /* If server and replacing scenes.
             * It's important to run this AFTER moving MovedNetworkObjects
             * so that they are no longer in the scenes they are leaving. Otherwise
             * the scene condition would pick them up as still in the leaving scene. */
            if (asServer && (replaceScenes != ReplaceOption.None))
            {
                //If global then remove all connections from all scenes.
                if (data.ScopeType == SceneScopeTypes.Global)
                {
                    Scene[] scenes = SceneConnections.Keys.ToArray();
                    foreach (Scene s in scenes)
                        RemoveAllConnectionsFromScene(s);
                }
                //Connections.
                else if (data.ScopeType == SceneScopeTypes.Connections)
                {
                    RemoveConnectionsFromNonGlobalScenes(data.Connections);
                }
            }


            /* Scene unloading if replacing scenes.
             * 
            * Unload all scenes except MovedObjectsHolder. Also don't
            * unload GlobalScenes if loading as connection. */
            List<Scene> unloadableScenes = new List<Scene>();
            //Do not run if running as client, and server is active. This would have already run as server.
            if ((replaceScenes != ReplaceOption.None) && !asHost)
            {
                //Unload all other scenes.
                for (int i = 0; i < UnitySceneManager.sceneCount; i++)
                {
                    Scene s = UnitySceneManager.GetSceneAt(i);
                    //MovedObjectsScene will never be unloaded.
                    if (s == GetMovedObjectsScene())
                        continue;
                    /* Scene is in one of the scenes being loaded.
                    * This can occur when trying to load additional clients
                    * into an existing scene. */
                    if (requestedLoadSceneNames.Contains(s.name))
                        continue;
                    //Same as above but using handles.
                    if (requestedLoadSceneHandles.Contains(s.handle))
                        continue;
                    /* Cannot unload global scenes. If
                     * replace scenes was used for a global
                     * load then global scenes would have bene reset
                     * before this. */
                    if (IsGlobalScene(s))
                        continue;
                    //If scene must be manually unloaded then it cannot be unloaded here.
                    if (_manualUnloadScenes.Contains(s))
                        continue;

                    HashSet<NetworkConnection> conns;
                    if (SceneConnections.TryGetValueIL2CPP(s, out conns))
                    {
                        //Still has clients in scene.
                        if (conns != null && conns.Count > 0)
                            continue;
                    }
                    //An offline scene.
                    else
                    {
                        //If not replacing all scenes then skip offline scenes.
                        if (replaceScenes != ReplaceOption.All)
                            continue;
                    }

                    unloadableScenes.Add(s);
                }
            }

            /* Start event. */
            if (unloadableScenes.Count > 0 || loadableScenes.Count > 0)
                InvokeOnSceneLoadStart(data);

            /* Before unloading if !asServer and !asHost and replacing scenes
             * then move all non scene networked objects to the moved
             * objects holder. Otherwise network objects would get destroyed
             * on the scene change and never respawned if server doesn't
             * have a reason to update visibility. */
            if (!data.AsServer && !asHost && (replaceScenes != ReplaceOption.None))
            {
                Scene s = GetMovedObjectsScene();
                foreach (NetworkObject nob in _networkManager.ClientManager.Objects.Spawned.Values)
                {
                    if (!nob.IsSceneObject)
                        UnitySceneManager.MoveGameObjectToScene(nob.gameObject, s);
                }
            }
            /* Unloading scenes. */
            for (int i = 0; i < unloadableScenes.Count; i++)
            {
                MoveClientHostObjects(unloadableScenes[i], asServer);
                //Unload one at a time.
                AsyncOperation async = UnitySceneManager.UnloadSceneAsync(unloadableScenes[i]);
                while (!async.isDone)
                    yield return null;
            }

            //Scenes which have been loaded.
            List<Scene> loadedScenes = new List<Scene>();
            List<AsyncOperation> asyncOperations = new List<AsyncOperation>();
            /* Scene loading.
            /* Use additive to not thread lock server. */
            for (int i = 0; i < loadableScenes.Count; i++)
            {
                //Start load async and wait for it to finish.
                LoadSceneParameters loadSceneParameters = new LoadSceneParameters()
                {
                    loadSceneMode = LoadSceneMode.Additive,
                    localPhysicsMode = data.SceneLoadData.Options.LocalPhysics
                };

                /* How much percentage each scene load can be worth
                * at maximum completion. EG: if there are two scenes
                * 1f / 2f is 0.5f. */
                float maximumIndexWorth = (1f / (float)loadableScenes.Count);

                AsyncOperation loadAsync;
                if (data.SceneLoadData.Options.Addressables)
                {
                    //loadAsync = Addressables
                    Debug.LogError($"Loading with addressables is not supported yet.");
                    loadAsync = null;
                }
                else
                {
                    loadAsync = UnitySceneManager.LoadSceneAsync(loadableScenes[i].Name, loadSceneParameters);
                }
                loadAsync.allowSceneActivation = false;
                asyncOperations.Add(loadAsync);
                while (loadAsync.progress < 0.9f)
                {
                    /* Total percent will be how much percentage is complete
                     * in total. Initialize it with a value based on how many
                     * scenes are already fully loaded. */
                    float totalPercent = (i * maximumIndexWorth);
                    //Add this scenes progress onto total percent.
                    totalPercent += Mathf.Lerp(0f, maximumIndexWorth, loadAsync.progress);
                    //Dispatch with total percent.
                    InvokeOnScenePercentChange(data, totalPercent);
                    yield return null;
                }

                //Add to loaded scenes.
                loadedScenes.Add(UnitySceneManager.GetSceneAt(UnitySceneManager.sceneCount - 1));
            }
            //When all scenes are loaded invoke with 100% done.
            InvokeOnScenePercentChange(data, 1f);


            /* Add to ManuallyUnloadScenes. */
            if (data.AsServer && !data.SceneLoadData.Options.AutomaticallyUnload)
            {
                foreach (Scene s in loadedScenes)
                    _manualUnloadScenes.Add(s);
            }
            /* Move identities to first scene. */
            if (!asHost)
            {
                //Find the first valid scene to move objects to.
                Scene firstValidScene = default;
                //If load call is stacking.
                if (!data.SceneLoadData.Options.DisallowStacking)
                {
                    Scene firstScene = GetFirstLookupScene(data.SceneLoadData.SceneLookupDatas);
                    /* If the first lookup data contains a handle and the scene
                     * is found for that handle then use that as the moved to scene.
                     * Nobs always move to the first specified scene. */
                    if (data.SceneLoadData.SceneLookupDatas[0].Handle != 0 && !string.IsNullOrEmpty(firstScene.name))
                    {
                        firstValidScene = firstScene;
                    }
                    //If handle is not specified then used the last scene that has the same name as the first lookupData.
                    else
                    {
                        Scene lastSameSceneName = default;
                        for (int i = 0; i < UnitySceneManager.sceneCount; i++)
                        {
                            Scene s = UnitySceneManager.GetSceneAt(i);
                            if (s.name == firstScene.name)
                                lastSameSceneName = s;
                        }

                        /* Shouldn't be possible since the scene will always exist either by 
                         * just being loaded or already loaded. */
                        if (string.IsNullOrEmpty(lastSameSceneName.name))
                        {
                            if (_networkManager.CanLog(LoggingType.Error))
                                Debug.LogError($"Scene {data.SceneLoadData.SceneLookupDatas[0].Name} could not be found in loaded scenes.");
                        }
                        else
                        {
                            firstValidScene = lastSameSceneName;
                        }
                    }
                }
                //Not stacking.
                else
                {
                    firstValidScene = GetFirstLookupScene(data.SceneLoadData.SceneLookupDatas);
                    //If not found by look then try firstloaded.
                    if (string.IsNullOrEmpty(firstValidScene.name))
                        firstValidScene = GetFirstLoadedScene();
                }

                //Gets first scene loaded this method call.
                Scene GetFirstLoadedScene()
                {
                    if (loadedScenes.Count > 0)
                        return loadedScenes[0];
                    else
                        return default;
                }
                //Gets first found scene in datas.
                Scene GetFirstLookupScene(SceneLookupData[] datas)
                {
                    foreach (SceneLookupData sld in datas)
                    {
                        Scene result = sld.GetScene(out _);
                        if (!string.IsNullOrEmpty(result.name))
                            return result;
                    }

                    return default;
                }

                //If firstValidScene is still invalid then throw.
                if (string.IsNullOrEmpty(firstValidScene.name))
                {
                    if (_networkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"Unable to move objects to a new scene because new scene lookup has failed.");
                }
                //Move objects.
                else
                {
                    Scene s = GetMovedObjectsScene();
                    s.GetRootGameObjects(_movingObjects);

                    foreach (GameObject go in _movingObjects)
                        UnitySceneManager.MoveGameObjectToScene(go, firstValidScene);
                }
            }

            //Activate loaded scenes.
            foreach (AsyncOperation item in asyncOperations)
                item.allowSceneActivation = true;

            //Wait until everything is loaded (done).
            while (true)
            {
                //Becomes false if not all scenes are IsDone.
                bool allLoaded = true;
                foreach (AsyncOperation item in asyncOperations)
                {
                    if (!item.isDone)
                    {
                        allLoaded = false;
                        break;
                    }
                }

                if (allLoaded)
                    break;
                else
                    yield return null;
            }

            /* Set active scene. */
            SetActiveScene();

            //Only the server needs to find scene handles to send to client. Client will send these back to the server.
            if (asServer)
            {
                //Populate broadcastLookupDatas with any loaded scenes.
                foreach (Scene s in loadedScenes)
                {
                    SetInFirstNullIndex(s);

                    //Sets scene in the first null index of broadcastLookupDatas.
                    void SetInFirstNullIndex(Scene scene)
                    {
                        for (int i = 0; i < broadcastLookupDatas.Length; i++)
                        {
                            if (broadcastLookupDatas[i] == null)
                            {
                                broadcastLookupDatas[i] = new SceneLookupData(scene);
                                return;
                            }
                        }

                        //If here there are no null entries.
                        if (_networkManager.CanLog(LoggingType.Error))
                            Debug.LogError($"Cannot add scene to broadcastLookupDatas, collection is full.");
                    }
                }
            }

            /* If running as server and server is
             * active then send scene changes to client.
             * Making sure server is still active should it maybe
             * have dropped during scene loading. */
            if (data.AsServer && _networkManager.IsServer)
            {
                //Tell clients to load same scenes.
                LoadScenesBroadcast msg = new LoadScenesBroadcast()
                {
                    QueueData = data
                };
                //Replace scene lookup datas with ones intended to broadcast to client.
                msg.QueueData.SceneLoadData.SceneLookupDatas = broadcastLookupDatas;
                //If networked scope then send to all.
                if (data.ScopeType == SceneScopeTypes.Global)
                {
                    NetworkConnection[] conns = _serverManager.Clients.Values.ToArray();
                    AddPendingLoad(conns);
                    _serverManager.Broadcast(msg, true);
                }
                //If connections scope then only send to connections.
                else if (data.ScopeType == SceneScopeTypes.Connections)
                {
                    AddPendingLoad(data.Connections);
                    for (int i = 0; i < data.Connections.Length; i++)
                    {
                        if (data.Connections[i].Authenticated)
                            data.Connections[i].Broadcast(msg, true);
                    }
                }
            }
            /* If running as client then send a message
             * to the server to tell them the scene was loaded.
             * This allows the server to add the client
             * to the scene for checkers. */
            else if (!data.AsServer && _networkManager.IsClient)
            {
                ClientScenesLoadedBroadcast msg = new ClientScenesLoadedBroadcast()
                {
                    SceneLookupDatas = data.SceneLoadData.SceneLookupDatas
                };
                _clientManager.Broadcast(msg);
            }

            InvokeOnSceneLoadEnd(data, requestedLoadSceneNames, loadedScenes);
        }

        /// <summary>
        /// Received on client when connection scenes must be loaded.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        private void OnLoadScenes(LoadScenesBroadcast msg)
        {
            //Null data is sent by the server when there are no start scenes to load.
            if (msg.QueueData == null)
            {
                TryInvokeLoadedStartScenes(_clientManager.Connection, false);
                return;
            }

            LoadQueueData qd = msg.QueueData;
            if (qd.ScopeType == SceneScopeTypes.Global)
                LoadGlobalScenesInternal(qd.SceneLoadData, qd.GlobalScenes, false);
            else
                LoadConnectionScenesInternal(new NetworkConnection[0], qd.SceneLoadData, qd.GlobalScenes, false);
        }
        #endregion

        #region UnloadScenes.
        /// <summary>
        /// Unloads scenes on the server and for all clients.
        /// </summary>
        /// <param name="sceneUnloadData">Data about which scenes to unload.</param>
        public void UnloadGlobalScenes(SceneUnloadData sceneUnloadData)
        {
            if (!CanExecute(true, true))
                return;

            UnloadGlobalScenesInternal(sceneUnloadData, _globalScenes, true);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="conns"></param>
        /// <param name="additiveScenes"></param>
        /// <param name="asServer"></param>
        private void UnloadGlobalScenesInternal(SceneUnloadData sceneUnloadData, string[] globalScenes, bool asServer)
        {
            UnloadQueueData uqd = new UnloadQueueData(SceneScopeTypes.Global, new NetworkConnection[0], sceneUnloadData, globalScenes, asServer);
            QueueOperation(uqd);
        }


        /// <summary>
        /// Unloads scenes on server and tells a connection to unload them as well. Other connections will not unload this scene.
        /// </summary>
        /// <param name="connection">Connection to unload scenes for.</param>
        /// <param name="sceneUnloadData">Data about which scenes to unload.</param>
        public void UnloadConnectionScenes(NetworkConnection connection, SceneUnloadData sceneUnloadData)
        {
            UnloadConnectionScenes(new NetworkConnection[] { connection }, sceneUnloadData);
        }
        /// <summary>
        /// Unloads scenes on server and tells connections to unload them as well. Other connections will not unload this scene.
        /// </summary>
        /// <param name="connections">Connections to unload scenes for.</param>
        /// <param name="sceneUnloadData">Data about which scenes to unload.</param>
        public void UnloadConnectionScenes(NetworkConnection[] connections, SceneUnloadData sceneUnloadData)
        {
            UnloadConnectionScenesInternal(connections, sceneUnloadData, _globalScenes, true);
        }

        /// <summary>
        /// Unloads scenes on server without telling any connections to unload them.
        /// </summary>
        /// <param name="sceneUnloadData">Data about which scenes to unload.</param>
        public void UnloadConnectionScenes(SceneUnloadData sceneUnloadData)
        {
            UnloadConnectionScenesInternal(new NetworkConnection[0], sceneUnloadData, _globalScenes, true);
        }
        /// <summary>
        /// Unloads scenes for connections.
        /// </summary>
        /// <param name="connections"></param>
        /// <param name="sceneUnloadData"></param>
        /// <param name="globalScenes"></param>
        /// <param name="asServer"></param>
        private void UnloadConnectionScenesInternal(NetworkConnection[] connections, SceneUnloadData sceneUnloadData, string[] globalScenes, bool asServer)
        {
            if (!CanExecute(asServer, true))
                return;
            if (SceneDataInvalid(sceneUnloadData, true))
                return;

            UnloadQueueData uqd = new UnloadQueueData(SceneScopeTypes.Connections, connections, sceneUnloadData, globalScenes, asServer);
            QueueOperation(uqd);
        }
        /// <summary>
        /// Loads scenes within QueuedSceneLoads.
        /// </summary>
        /// <returns></returns>
        private IEnumerator __UnloadScenes()
        {
            UnloadQueueData data = _queuedOperations[0] as UnloadQueueData;

            //If connection went inactive.
            if (!ConnectionActive(data.AsServer))
                yield break;

            /* Some actions should not run as client if server is also active.
            * This is to keep things from running twice. */
            bool asClientHost = (!data.AsServer && _networkManager.IsServer);
            ///True if running asServer.
            bool asServer = data.AsServer;

            //Get scenes to unload.
            Scene[] scenes = GetScenes(data.SceneUnloadData.SceneLookupDatas);
            /* No scenes found. Only run this if not asHost.
             * While asHost scenes will possibly not exist because
             * server side has already unloaded them. But rest of
             * the unload should continue. */
            if (scenes.Length == 0 && !asClientHost)
            {
                if (_networkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"No scenes were found to unload.");
                yield break;
            }

            /* Remove from global scenes
            * if server and scope is global.
            * All passed in scenes should be removed from global
            * regardless of if they're valid or not. If they are invalid,
            * then they shouldn't be in global to begin with. */
            if (asServer && data.ScopeType == SceneScopeTypes.Global)
            {
                RemoveFromGlobalScenes(data.SceneUnloadData.SceneLookupDatas);
                //Update queue data.
                data.GlobalScenes = _globalScenes;
            }

            /* Remove connections. */
            if (asServer)
            {
                foreach (Scene s in scenes)
                {
                    //If global then remove all connections.
                    if (data.ScopeType == SceneScopeTypes.Global)
                        RemoveAllConnectionsFromScene(s);
                    //Connections.
                    else if (data.ScopeType == SceneScopeTypes.Connections)
                        RemoveConnectionsFromScene(data.Connections, s);
                }
            }


            /* This will contain all scenes which can be unloaded.
             * The collection will be modified through various checks. */
            List<Scene> unloadableScenes = scenes.ToList();
            /* If asServer and KeepUnused then clear all unloadables.
             * The clients will still unload the scenes. */
            if ((asServer || asClientHost) && data.SceneUnloadData.Options.Mode == UnloadOptions.ServerUnloadModes.KeepUnused)
                unloadableScenes.Clear();
            /* Check to remove global scenes unloadableScenes.
             * This will need to be done if scenes are being unloaded
             * for connections. Global scenes cannot be unloaded as
             * connection. */
            if (data.ScopeType == SceneScopeTypes.Connections)
                RemoveGlobalScenes(unloadableScenes);
            //If set to unload unused only.
            if (data.SceneUnloadData.Options.Mode == UnloadOptions.ServerUnloadModes.UnloadUnused)
                RemoveOccupiedScenes(unloadableScenes);

            //If there are scenes to unload.
            if (unloadableScenes.Count > 0)
            {
                InvokeOnSceneUnloadStart(data);
                //Begin unloading.
                foreach (Scene s in unloadableScenes)
                {
                    MoveClientHostObjects(s, asServer);
                    /* Remove from manualUnloadedScenes.
                     * Scene may not be in this collection
                     * but removing is one call vs checking
                    * then removing. */
                    _manualUnloadScenes.Remove(s);
                    AsyncOperation async = UnitySceneManager.UnloadSceneAsync(s);
                    while (!async.isDone)
                        yield return null;
                }
            }

            /* If running as server then make sure server
             * is still active after the unloads. If so
             * send out unloads to clients. */
            if (asServer && ConnectionActive(true))
            {
                //Tell clients to unload same scenes.
                UnloadScenesBroadcast msg = new UnloadScenesBroadcast()
                {
                    QueueData = data
                };
                //Global.
                if (data.ScopeType == SceneScopeTypes.Global)
                {
                    _serverManager.Broadcast(msg, true);
                }
                //Connections.
                else if (data.ScopeType == SceneScopeTypes.Connections)
                {
                    if (data.Connections != null)
                    {
                        for (int i = 0; i < data.Connections.Length; i++)
                        {
                            if (data.Connections[i] != null)
                                data.Connections[i].Broadcast(msg, true);
                        }
                    }
                }
            }

            InvokeOnSceneUnloadEnd(data, unloadableScenes);
        }
        /// <summary>
        /// Received on clients when networked scenes must be unloaded.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        private void OnUnloadScenes(UnloadScenesBroadcast msg)
        {
            UnloadQueueData qd = msg.QueueData;
            if (qd.ScopeType == SceneScopeTypes.Global)
                UnloadGlobalScenesInternal(qd.SceneUnloadData, qd.GlobalScenes, false);
            else
                UnloadConnectionScenesInternal(new NetworkConnection[0], qd.SceneUnloadData, qd.GlobalScenes, false);
        }
        #endregion

        /// <summary>
        /// Moves objects to despawnLater in scene if they are visible to the clientHost.
        /// </summary>
        /// <param name="scene"></param>
        private void MoveClientHostObjects(Scene scene, bool asServer)
        {
            /* The asServer isn't really needed. I could only call
             * this method when asServer is true. But for the sake
             * of preventing user-error (me being the user this time)
             * I've included it into the parameters. */
            if (!asServer)
                return;
            //Don't need to perform if not host.
            if (!_networkManager.IsClient)
                return;

            NetworkConnection clientConn = _networkManager.ClientManager.Connection;
            /* It would be nice to see if the client wasn't even in the scene
             * here using SceneConnections but it's possible that the scene had been
             * wiped from SceneConnections earlier depending on how scenes are
             * loaded or unloaded. Instead we must iterate through spawned objects. */

            ListCache<NetworkObject> movingNobs = ListCaches.GetNetworkObjectCache();
            /* Rather than a get all networkobjects in scene
             * let's iterate the spawned objects instead. I imagine
             * in most scenarios iterating spawned would be faster.
             * That's a long one! */
            foreach (NetworkObject nob in _networkManager.ServerManager.Objects.Spawned.Values)
            {
                //Not in the scene being destroyed.
                if (nob.gameObject.scene != scene)
                    continue;
                //ClientHost doesn't have visibility.
                if (!nob.Observers.Contains(clientConn))
                    continue;

                /* If here nob is in the same being
                 * destroyed and clientHost has visiblity. */
                movingNobs.AddValue(nob);
            }

            int count = movingNobs.Written;
            if (count > 0)
            {
                Scene moveScene = GetDelayedDestroyScene();
                List<NetworkObject> collection = movingNobs.Collection;

                for (int i = 0; i < count; i++)
                {
                    NetworkObject nob = collection[i];
                    /* Force as not a scene object
                     * so that it becomes destroyed
                     * rather than disabled. */
                    nob.ClearRuntimeSceneObject();
                    /* If the object is already being despawned then
                     *just disable and move it. Otherwise despawn it
                    * on the server then move it. */
                    //Not deinitializing, despawn it then.
                    if (!nob.IsDeinitializing)
                        nob.Despawn();
                    else
                        nob.gameObject.SetActive(false);

                    UnitySceneManager.MoveGameObjectToScene(nob.gameObject, moveScene);
                }
            }
            ListCaches.StoreCache(movingNobs);
        }

        /// <summary>
        /// Returns if a connection is in a scene using SceneConnections.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        internal bool InSceneConnections(NetworkConnection conn, Scene scene)
        {
            if (!SceneConnections.TryGetValueIL2CPP(scene, out HashSet<NetworkConnection> hs))
                return false;
            else
                return hs.Contains(conn);
        }

        /// <summary>
        /// Adds the owner of nob to the gameObjects scene if there are no global scenes.
        /// </summary>
        public void AddOwnerToDefaultScene(NetworkObject nob)
        {
            //No owner.
            if (!nob.Owner.IsValid)
            {
                if (_networkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"NetworkObject {nob.name} does not have an owner.");
                return;
            }
            //Won't add to default if there are globals.
            if (_globalScenes != null && _globalScenes.Length > 0)
                return;

            AddConnectionToScene(nob.Owner, nob.gameObject.scene);
        }

        /// <summary>
        /// Adds a connection to a scene. This will always be called one connection at a time because connections are only added after they invidually validate loading the scene.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="conn"></param>
        private void AddConnectionToScene(NetworkConnection conn, Scene scene)
        {
            HashSet<NetworkConnection> hs;
            //Scene doesn't have any connections yet.
            bool inSceneConnections = SceneConnections.TryGetValueIL2CPP(scene, out hs);
            if (!inSceneConnections)
                hs = new HashSet<NetworkConnection>();

            bool added = hs.Add(conn);
            if (added)
            {
                conn.AddToScene(scene);

                //If not yet added to scene connections.
                if (!inSceneConnections)
                    SceneConnections[scene] = hs;

                NetworkConnection[] arrayConn = new NetworkConnection[] { conn };
                InvokeClientPresenceChange(scene, arrayConn, true, true);
                RebuildObservers(arrayConn.ToArray());
                InvokeClientPresenceChange(scene, arrayConn, true, false);

                /* Also need to rebuild all networkobjects
                * for connection so other players can
                * see them. */
                RebuildObservers(conn.Objects.ToArray());
            }
        }


        /// <summary>
        /// Removes connections from any scene which is not global.
        /// </summary>
        /// <param name="conns"></param>
        /// <param name="asd"></param>
        private void RemoveConnectionsFromNonGlobalScenes(NetworkConnection[] conns)
        {
            List<Scene> removedScenes = new List<Scene>();

            foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> item in SceneConnections)
            {
                Scene scene = item.Key;
                //Cannot remove from globla scenes.
                if (IsGlobalScene(scene))
                    continue;

                HashSet<NetworkConnection> hs = item.Value;
                List<NetworkConnection> connectionsRemoved = new List<NetworkConnection>();
                //Remove every connection from the scene.
                foreach (NetworkConnection c in conns)
                {
                    bool removed = hs.Remove(c);
                    if (removed)
                    {
                        c.RemoveFromScene(scene);
                        connectionsRemoved.Add(c);
                    }
                }

                //If hashset is empty then remove scene from SceneConnections.
                if (hs.Count == 0)
                    removedScenes.Add(scene);

                if (connectionsRemoved.Count > 0)
                {
                    NetworkConnection[] connectionsRemovedArray = connectionsRemoved.ToArray();
                    InvokeClientPresenceChange(scene, connectionsRemovedArray, false, true);
                    RebuildObservers(connectionsRemovedArray);
                    InvokeClientPresenceChange(scene, connectionsRemovedArray, false, false);
                }
            }

            foreach (Scene s in removedScenes)
                SceneConnections.Remove(s);

            /* Also rebuild observers for objects owned by connection.
             * This ensures other connections will lose visibility if
             * they no longer share a scene. */
            foreach (NetworkConnection c in conns)
                RebuildObservers(c.Objects.ToArray());
        }


        /// <summary>
        /// Removes connections from specified scenes.
        /// </summary>
        /// <param name="conns"></param>
        /// <param name="asd"></param>
        private void RemoveConnectionsFromScene(NetworkConnection[] conns, Scene scene)
        {
            HashSet<NetworkConnection> hs;
            //No hashset for scene, so no connections are in scene.
            if (!SceneConnections.TryGetValueIL2CPP(scene, out hs))
                return;

            List<NetworkConnection> connectionsRemoved = new List<NetworkConnection>();
            //Remove every connection from the scene.
            foreach (NetworkConnection c in conns)
            {
                bool removed = hs.Remove(c);
                if (removed)
                {
                    c.RemoveFromScene(scene);
                    connectionsRemoved.Add(c);
                }
            }

            //If hashset is empty then remove scene from SceneConnections.
            if (hs.Count == 0)
                SceneConnections.Remove(scene);

            if (connectionsRemoved.Count > 0)
            {
                NetworkConnection[] connectionsRemovedArray = connectionsRemoved.ToArray();
                InvokeClientPresenceChange(scene, connectionsRemovedArray, false, true);
                RebuildObservers(connectionsRemovedArray);
                InvokeClientPresenceChange(scene, connectionsRemovedArray, false, false);
            }

            /* Also rebuild observers for objects owned by connection.
            * This ensures other connections will lose visibility if
            * they no longer share a scene. */
            foreach (NetworkConnection c in conns)
                RebuildObservers(c.Objects.ToArray());
        }

        /// <summary>
        /// Removes all connections from a scene.
        /// </summary>
        /// <param name="scene"></param>
        private void RemoveAllConnectionsFromScene(Scene scene)
        {
            HashSet<NetworkConnection> hs;
            //No hashset for scene, so no connections are in scene.
            if (!SceneConnections.TryGetValueIL2CPP(scene, out hs))
                return;

            //On each connection remove them from specified scene.
            foreach (NetworkConnection c in hs)
                c.RemoveFromScene(scene);
            //Make hashset into list for presence change.
            NetworkConnection[] connectionsRemoved = hs.ToArray();

            //Clear hashset and remove entry from sceneconnections.
            hs.Clear();
            SceneConnections.Remove(scene);

            if (connectionsRemoved.Length > 0)
            {
                InvokeClientPresenceChange(scene, connectionsRemoved, false, true);
                RebuildObservers(connectionsRemoved);
                InvokeClientPresenceChange(scene, connectionsRemoved, false, false);
            }

            /* Also rebuild observers for objects owned by connection.
             * This ensures other connections will lose visibility if
             * they no longer share a scene. */
            foreach (NetworkConnection c in connectionsRemoved)
                RebuildObservers(c.Objects.ToArray());
        }

        #region Can Load/Unload Scene.
        /// <summary>
        /// Returns if a scene can be loaded locally.
        /// </summary>
        /// <returns></returns>
        private bool CanLoadScene(LoadQueueData qd, SceneLookupData sld)
        {
            bool foundByHandle;
            Scene s = sld.GetScene(out foundByHandle);
            //Try to find if scene is already loaded.
            bool alreadyLoaded = !string.IsNullOrEmpty(s.name);

            if (alreadyLoaded)
            {
                //Only servers can load the same scene multiple times for stacking.
                if (!qd.AsServer)
                    return false;
                //If can only load scenes which aren't loaded yet and scene is already loaded.
                if (qd.SceneLoadData.Options.DisallowStacking)
                    return false;
                /* Found by handle, this means the user is trying to specify
                 * exactly which scene to load into. When a handle is specified
                 * new instances will not be created, so a new scene cannot
                 * be loaded. */
                if (alreadyLoaded && foundByHandle)
                    return false;
            }

            //Fall through.
            return true;
        }
        #endregion

        #region Helpers.
        /// <summary>
        /// Rebuilds observers for networkObjects.
        /// </summary>
        /// <param name="networkObjects"></param>
        private void RebuildObservers(NetworkObject[] networkObjects)
        {
            foreach (NetworkObject nob in networkObjects)
            {
                if (nob != null && nob.IsSpawned)
                    _serverManager.Objects.RebuildObservers(nob);
            }
        }
        /// <summary>
        /// Rebuilds all NetworkObjects for connection.
        /// </summary>
        internal void RebuildObservers(NetworkConnection connection)
        {
            RebuildObservers(new NetworkConnection[] { connection });
        }
        /// <summary>
        /// Rebuilds all NetworkObjects for connections.
        /// </summary>
        internal void RebuildObservers(NetworkConnection[] connections)
        {
            foreach (NetworkConnection c in connections)
                _serverManager.Objects.RebuildObservers(c);
        }
        /// <summary>
        /// Invokes OnClientPresenceChange start or end.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="conns"></param>
        /// <param name="added"></param>
        /// <param name="start"></param>
        private void InvokeClientPresenceChange(Scene scene, NetworkConnection[] conns, bool added, bool start)
        {
            foreach (NetworkConnection c in conns)
            {
                ClientPresenceChangeEventArgs cpc = new ClientPresenceChangeEventArgs(scene, c, added);
                if (start)
                    OnClientPresenceChangeStart?.Invoke(cpc);
                else
                    OnClientPresenceChangeEnd?.Invoke(cpc);
            }
        }
        #endregion

        #region GetScene.
        /// <summary>
        /// Gets scenes from SceneLookupData.
        /// </summary>
        /// <param name="datas"></param>
        /// <returns></returns>
        private Scene[] GetScenes(SceneLookupData[] datas)
        {
            List<Scene> result = new List<Scene>();
            foreach (SceneLookupData sld in datas)
            {
                Scene s = sld.GetScene(out _);
                if (!string.IsNullOrEmpty(s.name))
                    result.Add(s);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Returns a scene by name.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public static Scene GetScene(string sceneName)
        {
            return UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
        }
        /// <summary>
        /// Returns a scene by handle.
        /// </summary>
        /// <param name="sceneHandle"></param>
        /// <returns></returns>
        public static Scene GetScene(int sceneHandle)
        {
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.handle == sceneHandle)
                    return s;
            }

            return new Scene();
        }
        #endregion

        /// <summary>
        /// Returns if GlobalScenes contains scene.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private bool IsGlobalScene(Scene scene)
        {
            for (int i = 0; i < _globalScenes.Length; i++)
            {
                if (_globalScenes[i] == scene.name)
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Removes datas from GlobalScenes.
        /// </summary>
        /// <param name="scenes"></param>
        private void RemoveFromGlobalScenes(Scene scene)
        {
            RemoveFromGlobalScenes(new SceneLookupData[] { SceneLookupData.CreateData(scene) });
        }
        /// <summary>
        /// Removes datas from GlobalScenes.
        /// </summary>
        /// <param name="scenes"></param>
        private void RemoveFromGlobalScenes(SceneLookupData[] datas)
        {
            List<string> newGlobalScenes = _globalScenes.ToList();
            int startCount = newGlobalScenes.Count;
            //Remove scenes.
            for (int i = 0; i < datas.Length; i++)
                newGlobalScenes.Remove(datas[i].Name);

            //If any were removed remake globalscenes.
            if (startCount != newGlobalScenes.Count)
                _globalScenes = newGlobalScenes.ToArray();
        }

        /// <summary>
        /// Removes GlobalScenes from scenes.
        /// </summary>
        /// <param name="scenes"></param>
        /// <returns></returns>
        private void RemoveGlobalScenes(List<Scene> scenes)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                foreach (string gs in _globalScenes)
                {
                    if (gs == scenes[i].name)
                    {
                        scenes.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        /// <summary>
        /// Removes occupied scenes from scenes.
        /// </summary>
        /// <param name="scenes"></param>
        private void RemoveOccupiedScenes(List<Scene> scenes)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (SceneConnections.TryGetValueIL2CPP(scenes[i], out _))
                {
                    scenes.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// Adds a pending load for a connection.
        /// </summary>
        private void AddPendingLoad(NetworkConnection conn)
        {
            AddPendingLoad(new NetworkConnection[] { conn });
        }
        /// <summary>
        /// Adds a pending load for a connection.
        /// </summary>
        private void AddPendingLoad(NetworkConnection[] conns)
        {
            foreach (NetworkConnection c in conns)
            {
                /* Make sure connection is active. This should always be true
                 * but perhaps disconnect happened as scene was loading on server
                * therefor it cannot be sent to the client. 
                * Also only authenticated clients can load scenes. */
                if (!c.IsActive || !c.Authenticated)
                    continue;

                if (_pendingClientSceneChanges.TryGetValue(c, out int result))
                    _pendingClientSceneChanges[c] = (result + 1);
                else
                    _pendingClientSceneChanges[c] = 1;
            }
        }
        /// <summary>
        /// Sets the first global scene as the active scene.
        /// If a global scene is not available then FallbackActiveScene is used.
        /// </summary>
        private void SetActiveScene()
        {
            Scene s = default;
            if (_globalScenes != null && _globalScenes.Length > 0)
                s = GetScene(_globalScenes[0]);

            /* If scene isn't set from global then make
             * sure currently active isn't the movedobjectscene.
             * If it is, then use the fallback scene. */
            if (string.IsNullOrEmpty(s.name) && UnitySceneManager.GetActiveScene() == _movedObjectsScene)
                s = GetFallbackActiveScene();

            //If was changed then update active scene.
            if (!string.IsNullOrEmpty(s.name))
                UnitySceneManager.SetActiveScene(s);

            OnActiveSceneSet?.Invoke();
            OnActiveSceneSetInternal?.Invoke();
        }

        /// <summary>
        /// Returns the FallbackActiveScene.
        /// </summary>
        /// <returns></returns>
        private Scene GetFallbackActiveScene()
        {
            if (string.IsNullOrEmpty(_fallbackActiveScene.name))
                _fallbackActiveScene = UnitySceneManager.CreateScene("FallbackActiveScene");

            return _fallbackActiveScene;
        }

        /// <summary>
        /// Returns the MovedObejctsScene.
        /// </summary>
        /// <returns></returns>
        private Scene GetMovedObjectsScene()
        {
            //Create moved objects scene. It will probably be used eventually. If not, no harm either way.
            if (string.IsNullOrEmpty(_movedObjectsScene.name))
                _movedObjectsScene = UnitySceneManager.CreateScene("MovedObjectsHolder");

            return _movedObjectsScene;
        }

        /// <summary>
        /// Returns the DelayedDestroyScene.
        /// </summary>
        /// <returns></returns>
        private Scene GetDelayedDestroyScene()
        {
            //Create moved objects scene. It will probably be used eventually. If not, no harm either way.
            if (string.IsNullOrEmpty(_delayedDestroyScene.name))
                _delayedDestroyScene = UnityEngine.SceneManagement.SceneManager.CreateScene("DelayedDestroy");

            return _delayedDestroyScene;
        }


        #region Sanity checks.
        /// <summary>
        /// Returns if a SceneLoadData is valid.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        private bool SceneDataInvalid(SceneLoadData data, bool error)
        {
            bool result = data.DataInvalid();
            if (result && error)
            {
                if (_networkManager.CanLog(LoggingType.Error))
                    Debug.LogError(INVALID_SCENELOADDATA);
            }

            return result;
        }
        /// <summary>
        /// Returns if a SceneLoadData is valid.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        private bool SceneDataInvalid(SceneUnloadData data, bool error)
        {
            bool result = data.DataInvalid();
            if (result && error)
            {
                if (_networkManager.CanLog(LoggingType.Error))
                    Debug.LogError(INVALID_SCENEUNLOADDATA);
            }

            return result;
        }
        /// <summary>
        /// Returns if connection is active for server or client in association with AsServer.
        /// </summary>
        /// <param name="asServer"></param>
        /// <returns></returns>
        private bool ConnectionActive(bool asServer)
        {
            return (asServer) ? _networkManager.IsServer : _networkManager.IsClient;
        }
        /// <summary>
        /// Returns if a method can execute.
        /// </summary>
        /// <param name="asServer"></param>
        /// <param name="warn"></param>
        /// <returns></returns>
        private bool CanExecute(bool asServer, bool warn)
        {
            bool result;
            if (asServer)
            {
                result = _networkManager.IsServer;
                if (!result && warn)
                {
                    if (_networkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Method cannot be called as the server is not active.");
                }
            }
            else
            {
                result = _networkManager.IsClient;
                if (!result && warn)
                {
                    if (_networkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Method cannot be called as the client is not active.");
                }
            }

            return result;
        }
        #endregion

    }
}