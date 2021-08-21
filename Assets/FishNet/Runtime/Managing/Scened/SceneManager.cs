using FishNet.Managing.Scened.Broadcast;
using FishNet.Connection;
using FishNet.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet.Managing.Scened.Data;
using FishNet.Managing.Scened.Eventing;
using FishNet.Managing.Server;
using FishNet.Managing.Client;
using FishNet.Transporting;

namespace FishNet.Managing.Scened
{
    public class SceneManager : MonoBehaviour
    {
        #region Types.
        /// <summary>
        /// Data about a scene which is to be loaded. Generated when processing scene queue data.
        /// </summary>
        private class LoadableScene
        {
            public LoadableScene(string sceneName, LoadSceneMode loadMode)
            {
                SceneName = sceneName;
                LoadMode = loadMode;
            }

            public readonly string SceneName;
            public readonly LoadSceneMode LoadMode;
        }
        #endregion

        #region Public.
        /// <summary>
        /// Called when a client loads initial scenes after connecting.
        /// </summary>
        public event Action<NetworkConnection> OnClientLoadedStartScenes;
        /// <summary>
        /// Dispatched when a scene change queue has begun. This will only call if a scene has succesfully begun to load or unload. The queue may process any number of scene events. For example: if a scene is told to unload while a load is still in progress, then the unload will be placed in the queue.
        /// </summary>
        public event Action OnSceneQueueStart;
        /// <summary>
        /// Dispatched when the scene queue is emptied.
        /// </summary>
        public event Action OnSceneQueueEnd;
        /// <summary>
        /// Dispatched when a scene load starts.
        /// </summary>
        public event Action<LoadSceneStartEventArgs> OnLoadSceneStart;
        /// <summary>
        /// Dispatched when completion percentage changes while loading a scene. Value is between 0f and 1f, while 1f is 100% done. Can be used for custom progress bars when loading scenes.
        /// </summary>
        public event Action<LoadScenePercentEventArgs> OnLoadScenePercentChange;
        /// <summary>
        /// Dispatched when a scene load ends.
        /// </summary>
        public event Action<LoadSceneEndEventArgs> OnLoadSceneEnd;
        /// <summary>
        /// Dispatched when a scene load starts.
        /// </summary>
        public event Action<UnloadSceneStartEventArgs> OnUnloadSceneStart;
        /// <summary>
        /// Dispatched when a scene load ends.
        /// </summary>
        public event Action<UnloadSceneEndEventArgs> OnUnloadSceneEnd;
        /// <summary>
        /// Dispatched before the server rebuilds observers when the clients presence changes for a scene.
        /// </summary>
        public event Action<ClientPresenceChangeEventArgs> OnClientPresenceChangeStart;
        /// <summary>
        /// Dispatched after the server rebuilds observers when the clients presence changes for a scene.
        /// </summary>
        public event Action<ClientPresenceChangeEventArgs> OnClientPresenceChangeEnd;
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
        private NetworkedScenesData _networkedScenes = new NetworkedScenesData();
        /// <summary>
        /// Most recently used LoadParams while loading a networked scene.
        /// </summary>
        private LoadParams _networkedLoadParams = new LoadParams();
        /// <summary>
        /// Scenes to load or unload, in order.
        /// </summary>
        private List<object> _queuedSceneOperations = new List<object>();
        /// <summary>
        /// Scenes which connections are registered as existing.
        /// </summary>
        public Dictionary<Scene, HashSet<NetworkConnection>> SceneConnections { get; private set; } = new Dictionary<Scene, HashSet<NetworkConnection>>();
        /// <summary>
        /// Scenes which must be manually unloaded, even when emptied.
        /// </summary>
        private HashSet<Scene> _manualUnloadScenes = new HashSet<Scene>();
        /// <summary>
        /// Scene containing moved objects when changing single scene. On client this will contain all objects moved until the server destroys them.
        /// Mirror only sends spawn messages once per-client, per server side scene load. If a scene load is performed only for specific connections
        /// then the server is not resetting their single scene, but rather the single scene for those connections only. Because of this, any objects
        /// which are to be moved will not receive a second respawn message, as they are never destroyed on server, only on client.
        /// While on server only this scene contains objects being moved temporarily, before being moved to the new scene.
        /// </summary>
        private Scene _movedObjectsScene;
        /// <summary>
        /// Becomes true when when a scene first successfully begins to load or unload. Value is reset to false when the scene queue is emptied.
        /// </summary>
        private bool _sceneQueueStartInvoked = false;
        #endregion

        #region Unity callbacks and initialization.
        private void Awake()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += SceneManager_SceneUnloaded;
        }

        private void Start()
        {
            _serverManager.OnAuthenticationResultInternal += _serverManager_OnAuthenticationResult;
            _networkManager.TransportManager.Transport.OnRemoteConnectionState += Transport_OnRemoteConnectionState;
            //No need to unregister since managers are on the same object.
            _clientManager.RegisterBroadcast<LoadScenesBroadcast>(OnLoadScenes);
            _clientManager.RegisterBroadcast<UnloadScenesBroadcast>(OnUnloadScenes);
            _serverManager.RegisterBroadcast<ClientScenesLoadedBroadcast>(OnClientScenesLoaded);
        }

        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        private void Transport_OnRemoteConnectionState(RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionStates.Stopped)
            {
                NetworkConnection conn;
                if (_serverManager.Clients.TryGetValue(args.ConnectionId, out conn))
                    ClientDisconnected(conn);
            }
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void FirstInitialize(NetworkManager manager)
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

            /* Networked scenes. 
             * These scenes cannot be stacked so looking up by name
             * is okay. If the scene unloaded name matches a networked
             * scene name then the networked scene is no longer loaded. */
            //Single.
            if (scene.name == _networkedScenes.Single)
                _networkedScenes.Single = string.Empty;
            //Additive.
            if (_networkedScenes.Additive.Length > 0)
            {
                List<string> newAdditives = _networkedScenes.Additive.ToList();
                newAdditives.Remove(scene.name);
                _networkedScenes.Additive = newAdditives.ToArray();
            }
        }
        #endregion

        #region Initial synchronizing.
        /// <summary>
        /// Invokes OnClientLoadedStartScenes if connection just loaded start scenes.
        /// </summary>
        /// <param name="connection"></param>
        private void TryInvokeLoadedStartScenes(NetworkConnection connection)
        {
            if (connection.SetLoadedStartScenes())
                OnClientLoadedStartScenes?.Invoke(connection);
        }

        /// <summary>
        /// Called when authenitcator has concluded a result for a connection. Boolean is true if authentication passed, false if failed. This invokes before OnClientAuthenticated so FishNet may run operations on authenticated clients before user code does.
        /// </summary>
        /// <param name="obj"></param>
        private void _serverManager_OnAuthenticationResult(NetworkConnection conn, bool passed)
        {
            if (!passed)
                return;
            if (!conn.IsValid)
                return;

            //No networked scenes to load.
            if (string.IsNullOrEmpty(_networkedScenes.Single) && (_networkedScenes.Additive == null || _networkedScenes.Additive.Length == 0))
            {
                AddToScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), conn);
                TryInvokeLoadedStartScenes(conn);
                return;
            }
            //Networked scenes to load.
            else
            {
                //If there is no single networked scene then add client to current scene.
                if (string.IsNullOrEmpty(_networkedScenes.Single))
                    AddToScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), conn);
            }

            SingleSceneData ssd = null;
            //If a single scene exist.
            if (!string.IsNullOrEmpty(_networkedScenes.Single))
                ssd = new SingleSceneData(_networkedScenes.Single);

            AdditiveScenesData asd = null;
            //If additive scenes exist.
            if (_networkedScenes.Additive.Length > 0)
                asd = new AdditiveScenesData(_networkedScenes.Additive);

            /* Client will only load what is unloaded. This is so
             * if they are on the scene with the networkmanager or other
             * ddols, the ddols wont be loaded multiple times. */
            LoadSceneQueueData sqd = new LoadSceneQueueData(SceneScopeTypes.Networked, null, ssd, asd, new LoadOptions(), _networkedLoadParams, _networkedScenes, false);

            //Send message to load the networked scenes.
            LoadScenesBroadcast msg = new LoadScenesBroadcast()
            {
                SceneQueueData = sqd
            };

            conn.Broadcast(msg, true);
        }
        #endregion

        #region Player disconnect.
        /// <summary>
        /// Received when a player disconnects from the server.
        /// </summary>
        /// <param name="conn"></param> //finish.
        private void ClientDisconnected(NetworkConnection conn)
        {
            RemoveFromSceneConnections(conn);
            /* Check if any scenes are left empty after removing player.
             * If so then mark them to be unloaded so long as they aren't
             * maunally unload, not active scene, and not a networked scene. */
            //Scenes to unload because there are no more observers.
            List<SceneReferenceData> scenesToUnload = new List<SceneReferenceData>();
            //Current active scene.
            Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> item in SceneConnections)
            {
                //todo move the unload check requirements such as active scene, networked, manual ect to the CanUnloadMethod and update it properly.
                if (item.Value.Count == 0 && item.Key != activeScene &&
                    !_manualUnloadScenes.Contains(item.Key) && !IsNetworkedScene(item.Key.name, _networkedScenes))
                    scenesToUnload.Add(new SceneReferenceData(item.Key));
            }

            //If scenes should be unloaded.
            if (scenesToUnload.Count > 0)
            {
                AdditiveScenesData asd = new AdditiveScenesData(scenesToUnload.ToArray());
                UnloadConnectionScenes(new NetworkConnection[] { null }, asd);
            }
        }
        #endregion

        #region Server received messages.
        /// <summary>
        /// Received on server when a client loads scenes.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        private void OnClientScenesLoaded(NetworkConnection conn, ClientScenesLoadedBroadcast msg)
        {
            //Todo make sure client is actually supposed to be in these scenes before adding.
            List<Scene> scenesLoaded = new List<Scene>();
            //Build scenes for events.
            foreach (SceneReferenceData item in msg.SceneDatas)
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    Scene s;
                    //If handle exist then get scene by the handle.
                    if (item.Handle != 0)
                        s = GetSceneByHandle(item.Handle);
                    //Otherwise get it by the name.
                    else
                        s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(item.Name);

                    if (!string.IsNullOrEmpty(s.name))
                        scenesLoaded.Add(s);
                }
            }

            //Add to scenes.
            for (int i = 0; i < scenesLoaded.Count; i++)
                AddToScene(scenesLoaded[i], conn);

            TryInvokeLoadedStartScenes(conn);
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
            OnSceneQueueStart?.Invoke();
        }
        /// <summary>
        /// Checks if OnQueueEnd should invoke, and if so invokes.
        /// </summary>
        private void TryInvokeOnQueueEnd()
        {
            if (!_sceneQueueStartInvoked)
                return;

            _sceneQueueStartInvoked = false;
            OnSceneQueueEnd?.Invoke();
        }
        /// <summary>
        /// Invokes that a scene load has started. Only called when valid scenes will be loaded.
        /// </summary>
        /// <param name="sqd"></param>
        private void InvokeOnSceneLoadStart(LoadSceneQueueData sqd)
        {
            TryInvokeOnQueueStart();
            OnLoadSceneStart?.Invoke(new LoadSceneStartEventArgs(sqd));
        }
        /// <summary>
        /// Invokes that a scene load has ended. Only called after a valid scene has loaded.
        /// </summary>
        /// <param name="sqd"></param>
        private void InvokeOnSceneLoadEnd(LoadSceneQueueData sqd, List<string> requestedLoadScenes, List<Scene> loadedScenes)
        {
            //Make new list to not destroy original data.
            List<string> skippedScenes = requestedLoadScenes.ToList();
            //Remove loaded scenes from requested scenes.
            for (int i = 0; i < loadedScenes.Count; i++)
                skippedScenes.Remove(loadedScenes[i].name);

            LoadSceneEndEventArgs args = new LoadSceneEndEventArgs(sqd, loadedScenes.ToArray(), skippedScenes.ToArray());
            OnLoadSceneEnd?.Invoke(args);
        }
        /// <summary>
        /// Invokes that a scene unload has started. Only called when valid scenes will be unloaded.
        /// </summary>
        /// <param name="sqd"></param>
        private void InvokeOnSceneUnloadStart(UnloadSceneQueueData sqd)
        {
            TryInvokeOnQueueStart();
            OnUnloadSceneStart?.Invoke(new UnloadSceneStartEventArgs(sqd));
        }
        /// <summary>
        /// Invokes that a scene unload has ended. Only called after a valid scene has unloaded.
        /// </summary>
        /// <param name="sqd"></param>
        private void InvokeOnSceneUnloadEnd(UnloadSceneQueueData sqd, List<Scene> unloadedScenes)
        {
            int[] handles = new int[unloadedScenes.Count];
            OnUnloadSceneEnd?.Invoke(new UnloadSceneEndEventArgs(sqd, handles));
        }
        /// <summary>
        /// Invokes when completion percentage changes while unloading or unloading a scene. Value is between 0f and 1f, while 1f is 100% done.
        /// </summary>
        /// <param name="value"></param>
        private void InvokeOnScenePercentChange(LoadSceneQueueData sqd, float value)
        {
            value = Mathf.Clamp(value, 0f, 1f);
            OnLoadScenePercentChange?.Invoke(new LoadScenePercentEventArgs(sqd, value));
        }
        #endregion

        #region Scene queue processing.
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

            while (_queuedSceneOperations.Count > 0)
            {
                //If a load scene.
                if (_queuedSceneOperations[0] is LoadSceneQueueData)
                    yield return StartCoroutine(__LoadScenes());
                //If an unload scene.
                else if (_queuedSceneOperations[0] is UnloadSceneQueueData)
                    yield return StartCoroutine(__UnloadScenes());

                _queuedSceneOperations.RemoveAt(0);
            }

            TryInvokeOnQueueEnd();
        }
        #endregion

        #region LoadScenes
        /// <summary>
        /// Loads scenes on the server and for all clients. Future clients will automatically load these scenes.
        /// </summary>
        /// <param name="singleScene">Single scene to load. Use null to opt-out of single scene loading.</param>
        /// <param name="additiveScenes">Additive scenes to load. Use null to opt-out of additive scene loading.</param>
        /// <param name="loadParams">Unload parameters which may be read from events during load. When used with Networked scenes the most recently set params will be sent.</param>
        public void LoadNetworkedScenes(SingleSceneData singleScene, AdditiveScenesData additiveScenes, LoadParams loadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            if (loadParams == null)
                loadParams = new LoadParams();

            _networkedLoadParams = loadParams;
            LoadScenesInternal(SceneScopeTypes.Networked, null, singleScene, additiveScenes, new LoadOptions(), loadParams, _networkedScenes, true);
        }
        /// <summary>
        /// Loads scenes on server and tells connections to load them as well. Other connections will not load this scene.
        /// </summary>
        /// <param name="conn">Connections to load scenes for.</param>
        /// <param name="singleScene">Single scene to load. Use null to opt-out of single scene loading.</param>
        /// <param name="additiveScenes">Additive scenes to load. Use null to opt-out of additive scene loading.</param>
        /// <param name="loadOptions">Additional LoadOptions for this action.</param>
        /// <param name="loadParams">Unload parameters which may be read from events during load.</param>
        public void LoadConnectionScenes(NetworkConnection conn, SingleSceneData singleScene, AdditiveScenesData additiveScenes, LoadOptions loadOptions = null, LoadParams loadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            LoadConnectionScenes(new NetworkConnection[] { conn }, singleScene, additiveScenes, loadOptions, loadParams);
        }
        /// <summary>
        /// Loads scenes on server and tells connections to load them as well. Other connections will not load this scene.
        /// </summary>
        /// <param name="conns">Connections to load scenes for.</param>
        /// <param name="singleScene">Single scene to load. Use null to opt-out of single scene loading.</param>
        /// <param name="additiveScenes">Additive scenes to load. Use null to opt-out of additive scene loading.</param>
        /// <param name="loadParams">Unload parameters which may be read from events during load.</param>
        public void LoadConnectionScenes(NetworkConnection[] conns, SingleSceneData singleScene, AdditiveScenesData additiveScenes, LoadOptions loadOptions = null, LoadParams loadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            if (loadOptions == null)
                loadOptions = new LoadOptions();
            if (loadParams == null)
                loadParams = new LoadParams();

            LoadScenesInternal(SceneScopeTypes.Connections, conns, singleScene, additiveScenes, loadOptions, loadParams, _networkedScenes, true);
        }
        /// <summary>
        /// Loads scenes on server without telling clients to load the scenes.
        /// </summary>
        /// <param name="conns">Connections to load scenes for.</param>
        /// <param name="singleScene">Single scene to load. Use null to opt-out of single scene loading.</param>
        /// <param name="additiveScenes">Additive scenes to load. Use null to opt-out of additive scene loading.</param>
        /// <param name="loadParams">Unload parameters which may be read from events during load.</param>
        public void LoadConnectionScenes(SingleSceneData singleScene, AdditiveScenesData additiveScenes, LoadOptions loadOptions = null, LoadParams loadParams = null)
        {
            if (!CanExecute(true, true))
                return;


            if (loadOptions == null)
                loadOptions = new LoadOptions();
            if (loadParams == null)
                loadParams = new LoadParams();

            LoadScenesInternal(SceneScopeTypes.Connections, null, singleScene, additiveScenes, loadOptions, loadParams, _networkedScenes, true);
        }
        /// <summary>
        /// 
        /// </summary>
        private void LoadScenesInternal(SceneScopeTypes scope, NetworkConnection[] conns, SingleSceneData singleScene, AdditiveScenesData additiveScenes, LoadOptions loadOptions, LoadParams loadParams, NetworkedScenesData networkedScenes, bool asServer)
        {
            if (conns == null)
                conns = new NetworkConnection[0];
            //Add to scene queue data.        
            _queuedSceneOperations.Add(new LoadSceneQueueData(scope, conns, singleScene, additiveScenes, loadOptions, loadParams, networkedScenes, asServer));
            /* If only one entry then scene operations are not currently in progress.
             * Should there be more than one entry then scene operations are already 
             * occuring. The coroutine will automatically load in order. */

            if (_queuedSceneOperations.Count == 1)
                StartCoroutine(__ProcessSceneQueue());
        }

        /// <summary>
        /// Loads a connection scene queue data. This behaves just like a networked scene load except it sends only to the specified connections, and it always loads as an additive scene on server.
        /// </summary>
        /// <returns></returns>
        private IEnumerator __LoadScenes()
        {
            LoadSceneQueueData sqd = _queuedSceneOperations[0] as LoadSceneQueueData;
            RemoveInvalidSceneQueueData(ref sqd);
            /* No single or additive scene data. They were
             * empty or removed due to being invalid. */
            if (sqd.SingleScene == null && sqd.AdditiveScenes == null)
                yield break;

            /* It's safe to assume that every entry in single scene or additive scenes
             * are valid so long as SingleScene or AdditiveScenes are not null. */
            //True if running as client, while network server is active.
            bool asClientServerActive = (!sqd.AsServer && _networkManager.IsServer);

            //Create moved objects scene. It will probably be used eventually. If not, no harm either way.
            if (string.IsNullOrEmpty(_movedObjectsScene.name))
                _movedObjectsScene = UnityEngine.SceneManagement.SceneManager.CreateScene("MovedObjectsHolder");
            //Scenes processed by a client during this method.
            HashSet<SceneReferenceData> clientProcessedScenes = new HashSet<SceneReferenceData>();
            //SceneDatas generated for single and additive scenes within this SceneQueueData which are already loaded, or have been.
            SceneReferenceData singleSceneReferenceData = new SceneReferenceData();
            List<SceneReferenceData> additiveSceneReferenceDatas = new List<SceneReferenceData>();
            //Single scene which is loaded, or is to be loaded. Will contain a valid scene if a single scene is specified.
            Scene singleScene = new Scene();
            //True if a connections load and is client only.
            bool connectionsAndClientOnly = (sqd.ScopeType == SceneScopeTypes.Connections && !_networkManager.IsServer);
            //True if a single scene is specified, whether it needs to be loaded or not.
            bool singleSceneSpecified = (sqd.SingleScene != null && !string.IsNullOrEmpty(sqd.SingleScene.SceneReferenceData.Name));

            /* Scene queue data scenes.
            * All scenes in the scene queue data whether they will be loaded or not. */
            List<string> requestedLoadScenes = new List<string>();
            if (sqd.SingleScene != null)
                requestedLoadScenes.Add(sqd.SingleScene.SceneReferenceData.Name);
            if (sqd.AdditiveScenes != null)
            {
                for (int i = 0; i < sqd.AdditiveScenes.SceneReferenceDatas.Length; i++)
                    requestedLoadScenes.Add(sqd.AdditiveScenes.SceneReferenceDatas[i].Name);
            }

            /* Add to client processed scenes. */
            if (!sqd.AsServer)
            {
                /* Add all scenes to client processed scenes, wether loaded or not.
                 * This is so client can tell the server they have those scenes ready
                 * afterwards, and server will update observers. */
                if (sqd.SingleScene != null)
                    clientProcessedScenes.Add(sqd.SingleScene.SceneReferenceData);

                if (sqd.AdditiveScenes != null)
                {
                    for (int i = 0; i < sqd.AdditiveScenes.SceneReferenceDatas.Length; i++)
                        clientProcessedScenes.Add(sqd.AdditiveScenes.SceneReferenceDatas[i]);
                }
            }

            /* Set networked scenes.
             * If server, and networked scope. */
            if (sqd.AsServer && sqd.ScopeType == SceneScopeTypes.Networked)
            {
                //If single scene specified then reset networked scenes.
                if (singleSceneSpecified)
                    _networkedScenes = new NetworkedScenesData();

                if (sqd.SingleScene != null)
                    _networkedScenes.Single = sqd.SingleScene.SceneReferenceData.Name;
                if (sqd.AdditiveScenes != null)
                {
                    /* Start by including current networked additive scenes
                     * into newNetworkedScenes. Any additionally loaded additive
                     * scenes will be added to this collection, and then converted
                     * back into the networked additive scenes array. */
                    List<string> newNetworkedScenes = _networkedScenes.Additive.ToList();
                    foreach (SceneReferenceData item in sqd.AdditiveScenes.SceneReferenceDatas)
                    {
                        /* Add to additive only if it doesn't already exist.
                         * This is because the same scene cannot be loaded
                         * twice as a networked scene, though it can if loading for a connection. */
                        if (!_networkedScenes.Additive.Contains(item.Name))
                            newNetworkedScenes.Add(item.Name);
                    }

                    _networkedScenes.Additive = newNetworkedScenes.ToArray();
                }

                //Update queue data.
                sqd.NetworkedScenes = _networkedScenes;
            }

            /* LoadableScenes and SceneReferenceDatas.
            /* Will contain scenes which may be loaded.
             * Scenes might not be added to loadableScenes
             * if for example loadOnlyUnloaded is true and
             * the scene is already loaded. */
            List<LoadableScene> loadableScenes = new List<LoadableScene>();
            bool loadSingleScene = false;
            //Do not run if running as client, and server is active. This would have already run as server.
            if (!asClientServerActive)
            {
                //Add single.
                if (sqd.SingleScene != null)
                {
                    loadSingleScene = CanLoadScene(sqd.SingleScene.SceneReferenceData, sqd.LoadOptions.LoadOnlyUnloaded, sqd.AsServer);
                    //If can load.
                    if (loadSingleScene)
                        loadableScenes.Add(new LoadableScene(sqd.SingleScene.SceneReferenceData.Name, LoadSceneMode.Single));
                    //If cannot load, see if it already exist, and if so add to server scene datas.
                    else
                        singleScene = TryAddToServerSceneDatas(sqd.AsServer, sqd.SingleScene.SceneReferenceData, ref singleSceneReferenceData);
                }
                //Add additives.
                if (sqd.AdditiveScenes != null)
                {
                    foreach (SceneReferenceData sceneData in sqd.AdditiveScenes.SceneReferenceDatas)
                    {
                        if (CanLoadScene(sceneData, sqd.LoadOptions.LoadOnlyUnloaded, sqd.AsServer))
                            loadableScenes.Add(new LoadableScene(sceneData.Name, LoadSceneMode.Additive));
                        else
                            TryAddToServerSceneDatas(sqd.AsServer, sceneData, ref additiveSceneReferenceDatas);
                    }
                }
            }

            /* Resetting SceneConnections. */
            if (sqd.AsServer)
            {
                //Networked.
                if (sqd.ScopeType == SceneScopeTypes.Networked)
                {
                    if (loadSingleScene)
                    {
                        /* Removing all scene connections will cause a rebuild per scene removed.
                         * It's not ideal when it would work just the same to clear SceneConnections
                         * and let the observers rebuild when objects are destroyed (which will happen anyway).
                         * But to preserve expected behavior this method has to be called which will invoke presence
                         * changed and rebuild for the scene. */
                        RemoveAllSceneConnections();
                    }
                }
                //Connections.
                else if (sqd.ScopeType == SceneScopeTypes.Connections)
                {
                    /* If only certain connections then remove connections
                    * from all scenes. They will be placed into new scenes
                    * once they confirm the scenes have loaded on their end. */
                    if (singleSceneSpecified && sqd.Connections != null)
                    {
                        for (int i = 0; i < sqd.Connections.Length; i++)
                            RemoveFromAllSceneConnections(sqd.Connections);
                    }
                }
            }

            /* Move identities
             * to holder scene to preserve them. 
             * Required if a single scene is specified. Cannot rely on
             * loadSingleScene since it is only true if the single scene
             * must be loaded, which may be false if it's already loaded on
             * the server. */
            //Do not run if running as client, and server is active. This would have already run as server.
            if (!asClientServerActive)
            {
                NetworkObject[] movedIdents = (singleSceneSpecified) ?
                    sqd.SingleScene.MovedNetworkObjects : sqd.AdditiveScenes.MovedNetworkObjects;
                foreach (NetworkObject ni in movedIdents)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ni.gameObject, _movedObjectsScene);

                if (singleSceneSpecified)
                {
                    /* Destroy non-moved player objects.
                     * Only runs on the server. */
                    if (sqd.AsServer && sqd.LoadOptions.RemovePlayerObjects && sqd.Connections != null)
                    {
                        //For every connection see which objects need to be removed.
                        foreach (NetworkConnection c in sqd.Connections)
                        {
                            if (c == null)
                                continue;
                            if (c.Objects.Count == 0)
                                continue;

                            List<NetworkObject> nobsToDestroy = new List<NetworkObject>();
                            //Go through every owned object.
                            foreach (NetworkObject nob in c.Objects)
                            {
                                bool inMovedObjects = false;
                                for (int z = 0; z < sqd.SingleScene.MovedNetworkObjects.Length; z++)
                                {
                                    //If in moved objects.
                                    if (nob == sqd.SingleScene.MovedNetworkObjects[z])
                                    {
                                        inMovedObjects = true;
                                        break;
                                    }
                                }
                                //If not in moved objects then add to destroy.
                                if (!inMovedObjects)
                                    nobsToDestroy.Add(nob);
                            }
                            //Destroy objects as required.
                            for (int i = 0; i < nobsToDestroy.Count; i++)
                                _serverManager.Despawn(nobsToDestroy[i].gameObject);
                        }
                    }
                }
            }

            /* Scene unloading.
             * 
            /* Unload all scenes (except moved objects scene). */
            /* Make a list for scenes which will be unloaded rather
            * than unload during the iteration. This is to prevent a
            * collection has changed error. 
            *
            * unloadableScenes is created so that if either unloadableScenes
            * or loadableScenes has value, the OnLoadStart event knows to dispatch. */
            List<Scene> unloadableScenes = new List<Scene>();
            //If a single is specified then build scenes to unload.
            //Do not run if running as client, and server is active. This would have already run as server.
            if (singleSceneSpecified && !asClientServerActive)
            {
                //Unload all other scenes.
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    Scene s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    //True if scene is unused.
                    bool unusedScene;
                    //If client only unload regardless.
                    if (_networkManager.IsClient && !_networkManager.IsServer)
                    {
                        unusedScene = true;
                    }
                    //Unused checks only apply if loading for connections and is server.
                    else if (sqd.ScopeType == SceneScopeTypes.Connections && sqd.AsServer)
                    {
                        //If scene must be manually unloaded then it cannot be unloaded here.
                        if (_manualUnloadScenes.Contains(s))
                        {
                            unusedScene = false;
                        }
                        //Not in manual unload, check if empty.
                        else
                        {
                            //If found in scenes set unused if has no connections.
                            if (SceneConnections.TryGetValue(s, out HashSet<NetworkConnection> conns))
                                unusedScene = (conns.Count == 0);
                            //If not found then set unused.
                            else
                                unusedScene = true;
                        }
                    }
                    /* Networked will always be unused, since scenes will change for
                     * everyone resulting in old scenes being wiped from everyone. */
                    else if (sqd.ScopeType == SceneScopeTypes.Networked)
                    {
                        unusedScene = true;
                    }
                    //Unhandled scope type. This should never happen.
                    else
                    {
                        Debug.LogWarning("Unhandled scope type for unused check.");
                        unusedScene = true;
                    }

                    //True if the scene being checked to unload is in scene queue data.
                    bool inSceneQueueData = requestedLoadScenes.Contains(s.name);
                    /* canUnload becomes true when the scene is
                     * not in the scene queue data, and when it passes
                     * CanUnloadScene conditions. */
                    bool canUnload = (
                        unusedScene &&
                        s.name != _movedObjectsScene.name &&
                        !inSceneQueueData &&
                        CanUnloadScene(s.name, sqd.NetworkedScenes)
                        );
                    //If not scene being changed to and not the object holder scene.
                    if (canUnload)
                        unloadableScenes.Add(s);
                }
            }

            /* Start event. */
            if (unloadableScenes.Count > 0 || loadableScenes.Count > 0)
                InvokeOnSceneLoadStart(sqd);

            /* Unloading scenes. */
            for (int i = 0; i < unloadableScenes.Count; i++)
            {
                //Unload one at a time.
                AsyncOperation async = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(unloadableScenes[i]);
                while (!async.isDone)
                    yield return null;
            }

            //Scenes which have been loaded.
            List<Scene> loadedScenes = new List<Scene>();
            /* Scene loading.
            /* Use additive to not thread lock server. */
            for (int i = 0; i < loadableScenes.Count; i++)
            {
                //Start load async and wait for it to finish.
                LoadSceneParameters loadSceneParameters = new LoadSceneParameters()
                {
                    loadSceneMode = LoadSceneMode.Additive,
                    localPhysicsMode = sqd.LoadOptions.LocalPhysics
                };

                AsyncOperation loadAsync = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(loadableScenes[i].SceneName, loadSceneParameters);
                while (!loadAsync.isDone)
                {
                    /* How much percentage each scene load can be worth
                     * at maximum completion. EG: if there are two scenes
                     * 1f / 2f is 0.5f. */
                    float maximumIndexWorth = (1f / (float)loadableScenes.Count);
                    /* Total percent will be how much percentage is complete
                     * in total. Initialize it with a value based on how many
                     * scenes are already fully loaded. */
                    float totalPercent = (i * maximumIndexWorth);
                    //Add this scenes progress onto total percent.
                    totalPercent += Mathf.Lerp(0f, maximumIndexWorth, loadAsync.progress);

                    //Dispatch with total percent.
                    InvokeOnScenePercentChange(sqd, totalPercent);

                    yield return null;
                }

                //After loaded, add to loaded scenes and datas.
                Scene lastLoadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(UnityEngine.SceneManagement.SceneManager.sceneCount - 1);
                SceneReferenceData sd = new SceneReferenceData()
                {
                    Handle = lastLoadedScene.handle,
                    Name = lastLoadedScene.name
                };
                //Add to loaded scenes.
                loadedScenes.Add(lastLoadedScene);

                /* Scene references */
                if (loadableScenes[i].LoadMode == LoadSceneMode.Single)
                {
                    singleSceneReferenceData = sd;
                    singleScene = lastLoadedScene;
                }
                else if (loadableScenes[i].LoadMode == LoadSceneMode.Additive)
                {
                    additiveSceneReferenceDatas.Add(sd);
                }
            }
            //When all scenes are loaded invoke with 100% done.
            InvokeOnScenePercentChange(sqd, 1f);

            /* Manual Unload Scenes. */
            if (sqd.AsServer && !sqd.LoadOptions.AutomaticallyUnload)
            {
                /* Go through every scene which was attempted to be loaded.
                 * Even if the scene wasn't loaded add it to the collection
                 * as some scenes won't load if they are already loaded. But,
                 * they may not be in the collection if the loading occurred outside
                 * of FSM. Since the user is telling us to load while not
                 * automatically unload, we should add it to manualUnloadScenes
                 * regardless. */
                if (sqd.SingleScene != null)
                {
                    Scene s = ReturnScene(sqd.SingleScene.SceneReferenceData);
                    if (s.IsValid())
                        _manualUnloadScenes.Add(s);
                }
                //Do the same as above for additives.
                if (sqd.AdditiveScenes != null)
                {
                    for (int i = 0; i < sqd.AdditiveScenes.SceneReferenceDatas.Length; i++)
                    {
                        Scene s = ReturnScene(sqd.AdditiveScenes.SceneReferenceDatas[i]);
                        if (s.IsValid())
                            _manualUnloadScenes.Add(s);
                    }
                }
            }

            /* Move identities to new single scene or first additive scene. */
            //Do not run if running as client, and server is active. This would have already run as server.
            if (!asClientServerActive)
            {
                NetworkObject[] movedNobs = (singleSceneSpecified) ?
                    sqd.SingleScene.MovedNetworkObjects : sqd.AdditiveScenes.MovedNetworkObjects;

                if (movedNobs.Length > 0)
                {
                    Scene nextScene;
                    if (singleSceneSpecified)
                    {
                        nextScene = singleScene;
                    }
                    else
                    {
                        /* If scenes loaded count contains any value then select the first
                         * entry from loaded. */
                        if (loadedScenes.Count > 0)
                        {
                            nextScene = loadedScenes[0];
                        }
                        /* If no new scenes were loaded then try to find the first scene
                         * specified in scene reference datas. */
                        else
                        {
                            //Try to get the first scene in additives which is supposed to be loading.
                            SceneReferenceData srd = sqd.AdditiveScenes.SceneReferenceDatas[0];
                            nextScene = ReturnScene(srd);
                        }
                    }

                    if (string.IsNullOrEmpty(nextScene.name))
                    {
                        Debug.LogError("Loaded or specified additive scenes could not be found. Network Identities will not be moved.");
                    }
                    else
                    {
                        /* The identities were already cleaned but this is just incase something happened
                         * to them while scenes were loading. */
                        foreach (NetworkObject nob in movedNobs)
                        {
                            if (nob != null && nob.ObjectId != -1)
                                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(nob.gameObject, nextScene);
                        }
                    }
                }
            }

            /* Activate single scene. */
            if (singleSceneSpecified)
            {
                /* Set active scene.
                * If networked, since all clients will be changing.
                * Or if connectionsAndClientOnly. 
                * 
                * Cannot change active scene if client host because new objects will spawn
                * into the single scene intended for only certain connections; this will break observers. */
                if ((sqd.ScopeType == SceneScopeTypes.Networked && !asClientServerActive) || connectionsAndClientOnly)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(singleScene);
            }

            /* If running as server and server is
             * active then send scene changes to client. */
            if (sqd.AsServer && _networkManager.IsServer)
            {
                if (sqd.SingleScene != null)
                    sqd.SingleScene.SceneReferenceData = singleSceneReferenceData;
                if (sqd.AdditiveScenes != null)
                    sqd.AdditiveScenes.SceneReferenceDatas = additiveSceneReferenceDatas.ToArray();

                //Tell clients to load same scenes.
                LoadScenesBroadcast msg = new LoadScenesBroadcast()
                {
                    SceneQueueData = sqd
                };
                //If networked scope then send to all.
                if (sqd.ScopeType == SceneScopeTypes.Networked)
                {
                    _serverManager.Broadcast(msg, true);
                }
                //If connections scope then only send to connections.
                else if (sqd.ScopeType == SceneScopeTypes.Connections)
                {
                    if (sqd.Connections != null)
                    {
                        for (int i = 0; i < sqd.Connections.Length; i++)
                        {
                            if (sqd.Connections[i] != null)
                                sqd.Connections[i].Broadcast(msg, true);
                        }
                    }
                }
            }
            /* If running as client then send a message
             * to the server to tell them the scene was loaded.
             * This allows the server to add the client
             * to the scene for checkers. */
            else if (!sqd.AsServer && _networkManager.IsClient)
            {
                ClientScenesLoadedBroadcast msg = new ClientScenesLoadedBroadcast()
                {
                    SceneDatas = clientProcessedScenes.ToArray()
                };
                _clientManager.Broadcast(msg);
            }

            RemoveEmptySceneConnections();
            InvokeOnSceneLoadEnd(sqd, requestedLoadScenes, loadedScenes);
        }

        /// <summary>
        /// Tries to find a scene and if found adds it to the specified setter.
        /// </summary>
        /// <returns>Scene added if found.</returns>
        private Scene TryAddToServerSceneDatas(bool asServer, SceneReferenceData dataToLookup, ref List<SceneReferenceData> setter)
        {
            Scene s = ReturnSceneFromReferenceData(asServer, dataToLookup);
            SceneReferenceData d = ReturnReferenceData(s);
            //If found.
            if (d != null)
            {
                setter.Add(d);
                return s;
            }

            /* Fall through, scene or ref data couldn't be found or made. */
            return new Scene();
        }

        /// <summary>
        /// Tries to find a scene and if found sets it to the specified reference.
        /// </summary>
        /// <returns>Scene added if found.</returns>
        private Scene TryAddToServerSceneDatas(bool asServer, SceneReferenceData dataToLookup, ref SceneReferenceData setter)
        {
            Scene s = ReturnSceneFromReferenceData(asServer, dataToLookup);
            SceneReferenceData d = ReturnReferenceData(s);
            //If found.
            if (d != null)
            {
                setter = d;
                return s;
            }

            /* Fall through, scene or ref data couldn't be found or made. */
            return new Scene();
        }
        /// <summary>
        /// Returns a scene using reference data.
        /// </summary>
        /// <param name="asServer"></param>
        /// <param name="referenceData"></param>
        /// <returns></returns>
        private Scene ReturnSceneFromReferenceData(bool asServer, SceneReferenceData referenceData)
        {
            //True if as client and server is also active.
            bool asClientServerActive = (!asServer && _networkManager.IsServer);

            Scene s;
            /* If handle is specified and server is running then find by
            * handle. Only the server can lookup by handle. 
            * Otherwise look up by name. */

            if (referenceData.Handle != 0 && (asServer || asClientServerActive))
                s = GetSceneByHandle(referenceData.Handle);
            else
                s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(referenceData.Name);

            return s;
        }

        /// <summary>
        /// Returns a scene reference data for a scene.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private SceneReferenceData ReturnReferenceData(Scene s)
        {
            if (string.IsNullOrEmpty(s.name))
            {
                return null;
            }
            else
            {
                SceneReferenceData sd = new SceneReferenceData()
                {
                    Handle = s.handle,
                    Name = s.name
                };

                return sd;
            }
        }

        /// <summary>
        /// Received on client when connection scenes must be loaded.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        private void OnLoadScenes(LoadScenesBroadcast msg)
        {
            if (!CanExecute(false, true))
                return;

            LoadSceneQueueData sqd = msg.SceneQueueData;
            LoadScenesInternal(sqd.ScopeType, null, sqd.SingleScene, sqd.AdditiveScenes, sqd.LoadOptions, sqd.LoadParams, sqd.NetworkedScenes, false);
        }
        #endregion

        #region UnloadScenes.
        /// <summary>
        /// Unloads additive scenes on the server and for all clients.
        /// </summary>
        /// <param name="additiveScenes">Scenes to unload by string lookup.</param>
        /// <param name="unloadParams">Unload parameters which may be read from events during unload.
        public void UnloadNetworkedScenes(string[] additiveScenes, UnloadParams unloadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            AdditiveScenesData asd = new AdditiveScenesData(additiveScenes);
            UnloadNetworkedScenes(asd, unloadParams);
        }
        /// <summary>
        /// Unloads additive scenes on the server and for all clients.
        /// </summary>
        /// <param name="additiveScenes">Scenes to unload by scene references.</param>
        /// <param name="unloadParams">Unload parameters which may be read from events during unload.</param>
        public void UnloadNetworkedScenes(AdditiveScenesData additiveScenes, UnloadParams unloadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            UnloadScenesInternal(SceneScopeTypes.Networked, null, additiveScenes, new UnloadOptions(), unloadParams, _networkedScenes, true);
        }
        /// <summary>
        /// Unloads scenes on server and tells a connection to unload them as well. Other connections will not unload this scene.
        /// </summary>
        /// <param name="conn">Connections to unload scenes for.</param>
        /// <param name="additiveScenes">Scenes to unload by string lookup.</param>
        /// <param name="unloadOptions">Additional unload options for this action.</param>
        /// /// <param name="unloadParams">Unload parameters which may be read from events during unload.</param>
        public void UnloadConnectionScenes(NetworkConnection conn, string[] additiveScenes, UnloadOptions unloadOptions = null, UnloadParams unloadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            UnloadConnectionScenes(new NetworkConnection[] { conn }, additiveScenes, unloadOptions, unloadParams);
        }
        /// <summary>
        /// Unloads scenes on server and tells connections to unload them as well. Other connections will not unload this scene.
        /// </summary>
        /// <param name="conns">Connections to unload scenes for.</param>
        /// <param name="additiveScenes">Scenes to unload by string lookup.</param>
        /// <param name="unloadParams">Unload parameters which may be read from events during unload.</param>
        public void UnloadConnectionScenes(NetworkConnection[] conns, string[] additiveScenes, UnloadOptions unloadOptions = null, UnloadParams unloadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            AdditiveScenesData asd = new AdditiveScenesData(additiveScenes);
            UnloadConnectionScenes(conns, asd, unloadOptions, unloadParams);
        }
        /// <summary>
        /// Unloads scenes on server and tells connections to unload them as well. Other connections will not unload this scene.
        /// </summary>
        /// <param name="conn">Connection to unload scenes for.</param>
        /// <param name="additiveScenes">Scenes to unload by scene references.</param>
        /// <param name="unloadParams">Unload parameters which may be read from events during unload.</param>
        public void UnloadConnectionScenes(NetworkConnection conn, AdditiveScenesData additiveScenes, UnloadOptions unloadOptions = null, UnloadParams unloadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            UnloadConnectionScenes(new NetworkConnection[] { conn }, additiveScenes, unloadOptions, unloadParams);
        }
        /// <summary>
        /// Unloads scenes on server and tells connections to unload them as well. Other connections will not unload this scene.
        /// </summary>
        /// <param name="conns">Connections to unload scenes for.</param>
        /// <param name="additiveScenes">Scenes to unload by scene references.</param>
        /// <param name="unloadParams">Unload parameters which may be read from events during unload.</param>
        public void UnloadConnectionScenes(NetworkConnection[] conns, AdditiveScenesData additiveScenes, UnloadOptions unloadOptions = null, UnloadParams unloadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            if (unloadOptions == null)
                unloadOptions = new UnloadOptions();

            UnloadScenesInternal(SceneScopeTypes.Connections, conns, additiveScenes, unloadOptions, unloadParams, _networkedScenes, true);
        }
        /// <summary>
        /// Unloads scenes on server without telling anyconnections to unload them.
        /// </summary>
        /// <param name="additiveScenes">Scenes to unload by string lookup.</param>
        /// <param name="unloadOptions">Additional unload options for this action.</param>
        /// <param name="unloadParams">Unload parameters which may be read from events during unload.</param>
        public void UnloadConnectionScenes(string[] additiveScenes, UnloadOptions unloadOptions = null, UnloadParams unloadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            UnloadConnectionScenes(additiveScenes, unloadOptions, unloadParams);
        }
        /// <summary>
        /// Unloads scenes on server without telling anyconnections to unload them.
        /// </summary>
        /// <param name="additiveScenes">Scenes to unload by scene references.</param>
        /// <param name="unloadOptions">Additional unload options for this action.</param>
        /// <param name="unloadParams">Unload parameters which may be read from events during unload.</param>
        public void UnloadConnectionScenes(AdditiveScenesData additiveScenes, UnloadOptions unloadOptions = null, UnloadParams unloadParams = null)
        {
            if (!CanExecute(true, true))
                return;

            if (unloadOptions == null)
                unloadOptions = new UnloadOptions();

            UnloadScenesInternal(SceneScopeTypes.Connections, null, additiveScenes, unloadOptions, unloadParams, _networkedScenes, true);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="conns"></param>
        /// <param name="additiveScenes"></param>
        /// <param name="asServer"></param>
        private void UnloadScenesInternal(SceneScopeTypes scope, NetworkConnection[] conns, AdditiveScenesData additiveScenes, UnloadOptions unloadOptions, UnloadParams unloadParams, NetworkedScenesData networkedScenes, bool asServer)
        {
            _queuedSceneOperations.Add(new UnloadSceneQueueData(scope, conns, additiveScenes, unloadOptions, unloadParams, networkedScenes, asServer));
            /* If only one entry then scene operations are not currently in progress.
             * Should there be more than one entry then scene operations are already 
             * occuring. The coroutine will automatically load in order. */
            if (_queuedSceneOperations.Count == 1)
                StartCoroutine(__ProcessSceneQueue());
        }

        /// <summary>
        /// Loads scenes within QueuedSceneLoads.
        /// </summary>
        /// <returns></returns>
        private IEnumerator __UnloadScenes()
        {
            UnloadSceneQueueData sqd = _queuedSceneOperations[0] as UnloadSceneQueueData;

            /* Update visibilities. 
             *
             * This is to be done regardless of if a scene is unloaded or not.
             * A scene may not be unloaded because other clients could still be
             * in it, but visibility should still be removed for those
             * which are unloading. */
            if (sqd.AsServer)
                RemoveFromSceneConnections(sqd.AdditiveScenes, sqd.Connections);

            RemoveInvalidSceneQueueData(ref sqd);
            /* No additive scenes to unload. */
            if (sqd.AdditiveScenes == null)
                yield break;

            /* It's safe to assume that every entry in additive scenes
             * are valid so long as AdditiveScenes are not null. */
            //True if running as client, while network server is active.
            bool asClientServerActive = (!sqd.AsServer && _networkManager.IsServer);

            /* Remove from networked scenes.
            * If server and scope is networked. 
            * All passed in scenes should be removed from networked
            * regardless of if they're valid or not. If they are invalid,
            * then they shouldn't be in networked to begin with. */
            if (sqd.AsServer && sqd.ScopeType == SceneScopeTypes.Networked)
            {
                /* Current networked additive scenes. Unloaded scenes
                 * will be removed from this, and then the modified collection
                 * will be set back to the networked additive scenes array. */
                List<string> newNetworkedScenes = _networkedScenes.Additive.ToList();
                //Remove unloaded from networked scenes.
                foreach (SceneReferenceData item in sqd.AdditiveScenes.SceneReferenceDatas)
                    newNetworkedScenes.Remove(item.Name);

                _networkedScenes.Additive = newNetworkedScenes.ToArray();
                //Update queue data.
                sqd.NetworkedScenes = _networkedScenes;
            }

            /* Build unloadable scenes collection. */
            List<Scene> unloadableScenes = new List<Scene>();
            /* Do not run if running as client, and server is active. This would have already run as server.
             * This can still run as server, or client long as client is not also server. */
            if (!asClientServerActive)
            {
                foreach (SceneReferenceData item in sqd.AdditiveScenes.SceneReferenceDatas)
                {
                    Scene s;
                    /* If the handle exist and as server
                     * then unload using the handle. Otherwise
                     * unload using the name. Handles are used to
                     * unload scenes with the same name, which would
                     * only occur on the server since it can spawn multiple instances
                     * of the same scene. Client will always only have
                     * one copy of each scene so it must get the scene
                     * by name. */
                    if (item.Handle != 0 && sqd.AsServer)
                        s = GetSceneByHandle(item.Handle);
                    else
                        s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(item.Name);

                    //True if scene is unused.
                    bool unusedScene;
                    //If client only, unload regardless.
                    if (_networkManager.IsClient && !_networkManager.IsServer)
                    {
                        unusedScene = true;
                    }
                    //Unused checks only apply if loading for connections and is server.
                    else if (sqd.ScopeType == SceneScopeTypes.Connections && sqd.AsServer)
                    {
                        //If force unload.
                        if (sqd.UnloadOptions.Mode == UnloadOptions.UnloadModes.ForceUnload)
                        {
                            unusedScene = true;
                        }
                        //If can unload unused.
                        else if (sqd.UnloadOptions.Mode == UnloadOptions.UnloadModes.UnloadUnused)
                        {
                            //If found in scenes set unused if has no connections.
                            if (SceneConnections.TryGetValue(s, out HashSet<NetworkConnection> conns))
                                unusedScene = (conns.Count == 0);
                            //If not found then set unused.
                            else
                                unusedScene = true;
                        }
                        //If cannot unload unused then set unused as false;
                        else
                        {
                            unusedScene = false;
                        }
                    }
                    /* Networked will always be unused, since scenes will change for
                     * everyone resulting in old scenes being wiped from everyone. */
                    else if (sqd.ScopeType == SceneScopeTypes.Networked)
                    {
                        unusedScene = true;
                    }
                    //Unhandled scope type. This should never happen.
                    else
                    {
                        Debug.LogWarning("Unhandled scope type for unused check.");
                        unusedScene = true;
                    }

                    /* canUnload becomes true when the scene is
                     * not in the scene queue data, and when it passes
                     * CanUnloadScene conditions. */
                    bool canUnload = (
                        unusedScene &&
                        s.name != _movedObjectsScene.name &&
                        CanUnloadScene(s, sqd.NetworkedScenes)
                        );

                    if (canUnload)
                        unloadableScenes.Add(s);
                }
            }

            //If there are scenes to unload.
            if (unloadableScenes.Count > 0)
            {
                /* If there are still scenes to unload after connections pass.
                 * There may not be scenes to unload as if another connection still
                 * exist in the unloadable scenes, then they cannot be unloaded. */
                if (unloadableScenes.Count > 0)
                {
                    InvokeOnSceneUnloadStart(sqd);
                    /* Like with loading when changing to a new single
                     * network scene it would be possible to just wipe the
                     * SceneConnections then let rebuild occur when scene unloads
                     * but the presence change events wouldn't invoke. Similarly here
                     * remove from scenes using methods which will do the same but
                     * also invoke presence change. */
                    if (_networkManager.IsServer && sqd.AsServer)
                    {
                        for (int i = 0; i < unloadableScenes.Count; i++)
                        {
                            RemoveFromScene(unloadableScenes[i]);
                            _manualUnloadScenes.Remove(unloadableScenes[i]);
                        }
                    }
                    /* Unload scenes.
                    /* Use additive to not thread lock server. */
                    foreach (Scene s in unloadableScenes)
                    {
                        AsyncOperation async = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(s);
                        while (!async.isDone)
                            yield return null;
                    }
                }
            }

            /* If running as server and server is
             * active then send scene changes to client. */
            if (sqd.AsServer && _networkManager.IsServer)
            {
                //Tell clients to unload same scenes.
                UnloadScenesBroadcast msg = new UnloadScenesBroadcast()
                {
                    SceneQueueData = sqd
                };
                //If connections scope.
                if (sqd.ScopeType == SceneScopeTypes.Networked)
                {
                    _serverManager.Broadcast(msg, true);
                }
                //Networked scope.
                else if (sqd.ScopeType == SceneScopeTypes.Connections)
                {
                    if (sqd.Connections != null)
                    {
                        for (int i = 0; i < sqd.Connections.Length; i++)
                        {
                            if (sqd.Connections[i] != null)
                                sqd.Connections[i].Broadcast(msg, true);
                        }
                    }
                }
            }

            RemoveEmptySceneConnections();
            InvokeOnSceneUnloadEnd(sqd, unloadableScenes);
        }
        /// <summary>
        /// Received on clients when networked scenes must be unloaded.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        private void OnUnloadScenes(UnloadScenesBroadcast msg)
        {
            UnloadSceneQueueData sqd = msg.SceneQueueData;
            UnloadScenesInternal(sqd.ScopeType, sqd.Connections, sqd.AdditiveScenes, new UnloadOptions(), msg.SceneQueueData.UnloadParams, sqd.NetworkedScenes, false);
        }
        #endregion

        #region Add scene checkers.
        /// <summary>
        /// Adds a FlexSceneChecker to a scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="fsc"></param>
        public void AddToScene(Scene scene, NetworkConnection conn)
        {
            AddToSceneInternal(scene, conn);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="conn"></param>
        private void AddToSceneInternal(Scene scene, NetworkConnection conn)
        {
            if (string.IsNullOrEmpty(scene.name) || !conn.IsValid)
                return;

            HashSet<NetworkConnection> hs;
            /* If the scene hasn't been added to the collection
             * yet then add it with an empty hashset. The hashet
             * will be populated below. */
            if (!SceneConnections.TryGetValue(scene, out hs))
            {
                hs = new HashSet<NetworkConnection>();
                SceneConnections[scene] = hs;
            }

            bool added = hs.Add(conn);
            //Already added, no need to do anything further.
            if (!added)
                return;

            //Connections which have had their presence changed.
            List<NetworkConnection> changedPresences = new List<NetworkConnection>();
            if (added)
            {
                changedPresences.Add(conn);
                conn.AddToScene(scene);
            }

            if (changedPresences.Count > 0)
            {
                InvokeClientPresenceChange(scene, changedPresences, true, true);
                RebuildObservers(scene, changedPresences);
                InvokeClientPresenceChange(scene, changedPresences, true, false);
            }
        }
        #endregion

        #region Remove scene checkers.
        /// <summary>
        /// Removes a connection from a scene.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="conn"></param>
        public void RemoveFromScene(Scene scene, NetworkConnection conn)
        {
            if (!CanExecute(true, true))
                return;

            RemoveFromScene(scene, new NetworkConnection[] { conn });
        }
        /// <summary>
        /// Removes connections from a scene.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="conns"></param>
        public void RemoveFromScene(Scene scene, NetworkConnection[] conns)
        {
            if (!CanExecute(true, true))
                return;

            RemoveFromSceneInternal(scene, conns);
        }
        /// <summary>
        /// Removes all connections from a scene.
        /// </summary>
        /// <param name="scene"></param>
        public void RemoveFromScene(Scene scene)
        {
            if (!CanExecute(true, true))
                return;

            RemoveFromSceneInternal(scene);
        }
        /// <summary>
        /// Removes all connections from a scene.
        /// </summary>
        /// <param name="scene"></param>
        private void RemoveFromSceneInternal(Scene scene)
        {
            HashSet<NetworkConnection> hs;
            //No hashset for scene, so no connections are in scene.
            if (!SceneConnections.TryGetValue(scene, out hs))
                return;

            //Make hashset into list for presence change.
            List<NetworkConnection> changedPresences = hs.ToList();
            for (int i = 0; i < changedPresences.Count; i++)
                changedPresences[i].RemoveFromScene(scene);
            //Clear hashset and remove entry from sceneconnections.
            hs.Clear();
            SceneConnections.Remove(scene);

            //If connections to remove then invoke presence change and rebuild.
            if (hs.Count > 0)
            {
                InvokeClientPresenceChange(scene, changedPresences, false, true);
                RebuildObservers(scene, changedPresences);
                InvokeClientPresenceChange(scene, changedPresences, false, false);
            }
        }
        /// <summary>
        /// Removed connections from scene.
        /// </summary>
        private void RemoveFromSceneInternal(Scene scene, NetworkConnection[] connections)
        {
            if (connections == null || connections.Length == 0)
                return;

            //Connections which had their presences changed.
            List<NetworkConnection> changedPresences = new List<NetworkConnection>();

            for (int i = 0; i < connections.Length; i++)
            {
                if (connections[i].RemoveFromScene(scene))
                {
                    changedPresences.Add(connections[i]);
                    connections[i].RemoveFromScene(scene);
                }
            }

            if (changedPresences.Count > 0)
            {
                //Also remove from SceneConnections.
                if (SceneConnections.TryGetValue(scene, out HashSet<NetworkConnection> conns))
                {
                    for (int i = 0; i < changedPresences.Count; i++)
                        conns.Remove(changedPresences[i]);
                }

                InvokeClientPresenceChange(scene, changedPresences, false, true);
                RebuildObservers(scene, changedPresences);
                InvokeClientPresenceChange(scene, changedPresences, false, false);
            }
        }
        #endregion

        #region Remove Invalid Scenes.
        /// <summary>
        /// Removes invalid scene entries from a SceneQueueData.
        /// </summary>
        /// <param name="sceneDatas"></param>
        private void RemoveInvalidSceneQueueData(ref LoadSceneQueueData sqd)
        {
            //Check single scene.
            //If scene name is invalid.
            if (sqd.SingleScene == null ||
                sqd.SingleScene.SceneReferenceData == null || string.IsNullOrEmpty(sqd.SingleScene.SceneReferenceData.Name) ||
                //Loading for connection but already a single networked scene.
                (sqd.ScopeType == SceneScopeTypes.Connections && IsNetworkedScene(sqd.SingleScene.SceneReferenceData.Name, _networkedScenes))
                )
                sqd.SingleScene = null;

            //Check additive scenes.
            if (sqd.AdditiveScenes != null)
            {
                //Make all scene names into a list for easy removal.
                List<SceneReferenceData> listSceneReferenceDatas = sqd.AdditiveScenes.SceneReferenceDatas.ToList();
                for (int i = 0; i < listSceneReferenceDatas.Count; i++)
                {
                    //Scene name is null or empty.
                    if (listSceneReferenceDatas[i] == null || string.IsNullOrEmpty(listSceneReferenceDatas[i].Name))
                    {
                        listSceneReferenceDatas.RemoveAt(i);
                        i--;
                    }
                }
                //Set back to array.
                sqd.AdditiveScenes.SceneReferenceDatas = listSceneReferenceDatas.ToArray();

                //If additive scene names is null or has no length then nullify additive scenes.
                if (sqd.AdditiveScenes.SceneReferenceDatas == null || sqd.AdditiveScenes.SceneReferenceDatas.Length == 0)
                    sqd.AdditiveScenes = null;
            }

            //Connections.
            if (sqd.Connections != null && sqd.Connections.Length > 0)
            {
                List<NetworkConnection> listConnections = sqd.Connections.ToList();
                for (int i = 0; i < listConnections.Count; i++)
                {
                    if (listConnections[i] == null || listConnections[i].ClientId == -1)
                    {
                        listConnections.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        /// <summary>
        /// Removes invalid scene entries from a SceneQueueData.
        /// </summary>
        /// <param name="sceneDatas"></param>
        private void RemoveInvalidSceneQueueData(ref UnloadSceneQueueData sqd)
        {
            Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            //Check additive scenes.
            if (sqd.AdditiveScenes != null)
            {
                NetworkedScenesData networkedScenes = (_networkManager.IsServer) ? _networkedScenes : sqd.NetworkedScenes;
                //Make all scene names into a list for easy removal.
                List<SceneReferenceData> listSceneNames = sqd.AdditiveScenes.SceneReferenceDatas.ToList();
                for (int i = 0; i < listSceneNames.Count; i++)
                {
                    //If scene name is null or zero length/
                    if (string.IsNullOrEmpty(listSceneNames[i].Name) ||
                        //Or if scene name is active scene.
                        (activeScene != null && listSceneNames[i].Name == activeScene.name) ||
                        //If unloading as connection but scene is networked.
                        (sqd.ScopeType == SceneScopeTypes.Connections && IsNetworkedScene(listSceneNames[i].Name, networkedScenes))
                        )
                    {
                        listSceneNames.RemoveAt(i);
                        i--;
                    }
                }
                //Set back to array.
                sqd.AdditiveScenes.SceneReferenceDatas = listSceneNames.ToArray();

                //If additive scene names is null or has no length then nullify additive scenes.
                if (sqd.AdditiveScenes.SceneReferenceDatas == null || sqd.AdditiveScenes.SceneReferenceDatas.Length == 0)
                    sqd.AdditiveScenes = null;
            }
        }
        #endregion

        #region Can Load/Unload Scene.
        /// <summary>
        /// Returns if a scene name can be loaded.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="loadOnlyUnloaded"></param>
        /// <returns></returns>
        private bool CanLoadScene(SceneReferenceData sceneReferenceData, bool loadOnlyUnloaded, bool asServer)
        {
            /* When a handle is specified a scene can only be loaded if the handle does not exist.
             * This is regardless of loadOnlyUnloaded value. This is also only true for the server, as
             * only the server actually utilizies/manages handles. This feature exist so users may stack scenes
             * by setting loadOnlyUnloaded false, while also passing in a scene reference which to add a connection
             * to.
             * 
             * For example: if scene stacking is enabled(so, !loadOnlyUnloaded), and a player is the first to join Blue scene. Let's assume
             * the handle for that spawned scene becomes -10. Later, the server wants to add another player to the same
             * scene. They would generate the load scene data, passing in the handle of -10 for the scene to load. The
             * loader will then check if a scene is loaded by that handle, and if so add the player to that scene rather than
             * load an entirely new scene. If a scene does not exist then a new scene will be made. */
            if (asServer && sceneReferenceData.Handle != 0)
            {
                if (!string.IsNullOrEmpty(GetSceneByHandle(sceneReferenceData.Handle).name))
                    return false;
            }

            return CanLoadScene(sceneReferenceData.Name, loadOnlyUnloaded);
        }
        /// <summary>
        /// Returns if a scene name can be loaded.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="loadOnlyUnloaded"></param>
        /// <returns></returns>
        private bool CanLoadScene(string sceneName, bool loadOnlyUnloaded)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            if (!loadOnlyUnloaded || (loadOnlyUnloaded && !IsSceneLoaded(sceneName)))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Returns if a scene can be unloaded.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="scopeType"></param>
        /// <returns></returns>
        private bool CanUnloadScene(string sceneName, NetworkedScenesData networkedScenes)
        {
            //Not loaded.
            if (!IsSceneLoaded(sceneName))
                return false;

            /* Cannot unload networked scenes.
             * If a scene should be unloaded, that is networked,
             * then it must be removed from the networked scenes
             * collection first. */
            if (IsNetworkedScene(sceneName, networkedScenes))
                return false;

            return true;
        }

        /// <summary>
        /// Returns if a scene can be unloaded.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="scopeType"></param>
        /// <returns></returns>
        private bool CanUnloadScene(Scene scene, NetworkedScenesData networkedScenes)
        {
            return CanUnloadScene(scene.name, networkedScenes);
        }
        #endregion

        #region Remove From Scene Connections
        /// <summary>
        /// Removes all players from all scenes.
        /// </summary>
        private void RemoveAllSceneConnections()
        {
            foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> item in SceneConnections)
            {
                //Cache connections in scene then clear from scene.
                List<NetworkConnection> conns = item.Value.ToList();
                item.Value.Clear();

                InvokeClientPresenceChange(item.Key, conns, false, true);
                RebuildObservers(item.Key, conns);
                InvokeClientPresenceChange(item.Key, conns, false, false);
            }

            SceneConnections.Clear();
        }
        /// <summary>
        /// Removes a connection from all SceneConnection collections.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="removeEmptySceneConnections"></param>
        private void RemoveFromSceneConnections(NetworkConnection conn)
        {
            RemoveFromAllSceneConnections(new NetworkConnection[] { conn });
        }
        /// <summary>
        /// Removes connections from all SceneConnection collections.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="conns"></param>
        private void RemoveFromAllSceneConnections(NetworkConnection[] conns)
        {
            if (conns == null)
                return;

            foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> item in SceneConnections)
                RemoveFromScene(item.Key, conns);
        }

        /// <summary>
        /// Removes connections from specified scenes.
        /// </summary>
        /// <param name="conns"></param>
        /// <param name="asd"></param>
        private void RemoveFromSceneConnections(AdditiveScenesData asd, NetworkConnection[] conns)
        {
            //Build a collection of scenes which visibility is being removed from.
            Scene[] scenesToRemoveFrom = new Scene[asd.SceneReferenceDatas.Length];
            //Build scenes which connection is in using additive scenes data.
            for (int i = 0; i < asd.SceneReferenceDatas.Length; i++)
            {
                Scene s;
                if (asd.SceneReferenceDatas[i].Handle != 0)
                    s = GetSceneByHandle(asd.SceneReferenceDatas[i].Handle);
                else
                    s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(asd.SceneReferenceDatas[i].Name);

                if (!string.IsNullOrEmpty(s.name))
                    scenesToRemoveFrom[i] = s;
            }

            RemoveFromSceneConnections(scenesToRemoveFrom, conns);
        }

        /// <summary>
        /// Removes connections from specified scenes.
        /// </summary>
        /// <param name="conns"></param>
        /// <param name="asd"></param>
        private void RemoveFromSceneConnections(Scene[] scenes, NetworkConnection[] conns)
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!string.IsNullOrEmpty(scenes[i].name))
                    RemoveFromScene(scenes[i], conns);
            }
        }
        #endregion

        #region Helpers.
        /// <summary>
        /// Rebuilds all FlexSceneCheckers for a scene.
        /// </summary>
        internal void RebuildObservers(Scene scene, NetworkConnection connection)
        {
            foreach (NetworkObject nob in _serverManager.Objects.Spawned.Values)
            {
                if (nob.gameObject.scene.handle == scene.handle)
                    nob.RebuildObservers(connection);
            }
        }
        /// <summary>
        /// Rebuilds all FlexSceneCheckers for a scene.
        /// </summary>
        internal void RebuildObservers(Scene scene, List<NetworkConnection> connections)
        {
            foreach (NetworkObject nob in _serverManager.Objects.Spawned.Values)
            {
                if (nob.gameObject.scene.handle == scene.handle)
                    _serverManager.Objects.RebuildObservers(nob, connections);
            }
        }
        /// <summary>
        /// Invokes OnClientPresenceChange start or end.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="conns"></param>
        /// <param name="added"></param>
        /// <param name="start"></param>
        private void InvokeClientPresenceChange(Scene scene, List<NetworkConnection> conns, bool added, bool start)
        {
            for (int i = 0; i < conns.Count; i++)
            {
                ClientPresenceChangeEventArgs cpc = new ClientPresenceChangeEventArgs(scene, conns[i], added);
                if (start)
                    OnClientPresenceChangeStart?.Invoke(cpc);
                else
                    OnClientPresenceChangeEnd?.Invoke(cpc);
            }
        }
        /// <summary>
        /// Removes keys from SceneConnections which contain no value.
        /// </summary>
        private void RemoveEmptySceneConnections()
        {
            //Scenes to remove from SceneConnections.
            List<Scene> keysToRemove = new List<Scene>();
            //Find any scenes with no connections and add them to keys to remove.
            foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> item in SceneConnections)
            {
                if (item.Value.Count == 0)
                    keysToRemove.Add(item.Key);
            }

            for (int i = 0; i < keysToRemove.Count; i++)
                SceneConnections.Remove(keysToRemove[i]);
        }

        /// <summary>
        /// Returns if a sceneName is a networked scene.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        private bool IsNetworkedScene(string sceneName, NetworkedScenesData networkedScenes)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            //Matches single sene.
            if (networkedScenes.Single != null && sceneName == networkedScenes.Single)
                return true;

            //Matches at least one additive.
            if (networkedScenes.Additive != null)
            {
                if (networkedScenes.Additive.Contains(sceneName))
                    return true;
            }

            //Fall through, no matches.
            return false;
        }
        /// <summary>
        /// Returns if a scene is loaded.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        private bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name == sceneName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a scene by handle.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public Scene GetSceneByHandle(int handle)
        {
            return GetSceneByHandleInternal(handle);
        }
        /// <summary>
        /// Returns a scene by handle.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        private Scene GetSceneByHandleInternal(int handle)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                Scene s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.handle == handle)
                    return s;
            }

            //Fall through, not found.
            return new Scene();
        }
        #endregion

        #region ReturnScene.
        /// <summary>
        /// Returns a scene by name.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns></returns>
        public Scene ReturnScene(string sceneName)
        {
            return UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
        }
        /// <summary>
        /// Returns a scene by handle.
        /// </summary>
        /// <param name="sceneHandle"></param>
        /// <returns></returns>
        public Scene ReturnScene(int sceneHandle)
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
        /// <summary>
        /// Returns a scene by reference data.
        /// </summary>
        /// <param name="referenceData"></param>
        /// <returns></returns>
        public Scene ReturnScene(SceneReferenceData referenceData)
        {
            if (referenceData.Handle != 0)
                return ReturnScene(referenceData.Handle);
            else if (!string.IsNullOrEmpty(referenceData.Name))
                return ReturnScene(referenceData.Name);
            else
                return new Scene();
        }
        #endregion

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
                    Debug.LogWarning($"Method cannot be called as the server is not active.");
            }
            else
            {
                result = _networkManager.IsClient;
                if (!result && warn)
                    Debug.LogWarning($"Method cannot be called as the client is not active.");
            }

            return result;
        }

    }
}