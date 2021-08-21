using FishNet.Connection;

namespace FishNet.Managing.Scened.Data
{


    /// <summary>
    /// Used to load a scene for a targeted connection.
    /// </summary>
    public class LoadSceneQueueData
    {
        /// <summary>
        /// Clients which receive this SceneQueueData. If Networked, all clients do. If Connections, only the specified Connections do.
        /// </summary>
        [System.NonSerialized]
        public SceneScopeTypes ScopeType;
        /// <summary>
        /// Connections to load scenes for. Only valid on the server and when ScopeType is Connections.
        /// </summary>
        [System.NonSerialized]
        public NetworkConnection[] Connections = new NetworkConnection[0];
        /// <summary>
        /// Single scene to load.
        /// </summary>
        public SingleSceneData SingleScene = null;
        /// <summary>
        /// Additive scenes to load.
        /// </summary>
        public AdditiveScenesData AdditiveScenes = null;
        /// <summary>
        /// Current data on networked scenes.
        /// </summary>
        public NetworkedScenesData NetworkedScenes = null;
        /// <summary>
        /// True if to iterate this queue data as server.
        /// </summary>
        [System.NonSerialized]
        public readonly bool AsServer;
        /// <summary>
        /// Load options for this scene queue data. This is only available on the server.
        /// </summary>
        [System.NonSerialized]
        public readonly LoadOptions LoadOptions = new LoadOptions();
        /// <summary>
        /// Load params for this scene queue data. Value is only available on server.
        /// </summary>
        [System.NonSerialized]
        public readonly LoadParams LoadParams = new LoadParams();

        public LoadSceneQueueData() { }

        /// <summary>
        /// Creates a SceneQueueData.
        /// </summary>
        /// /// <param name="singleScene"></param>
        /// <param name="additiveScenes"></param>
        /// <param name="loadOnlyUnloaded"></param>
        public LoadSceneQueueData(SceneScopeTypes scopeType, NetworkConnection[] conns, SingleSceneData singleScene, AdditiveScenesData additiveScenes, LoadOptions loadOptions, LoadParams loadParams, NetworkedScenesData networkedScenes, bool asServer)
        {
            ScopeType = scopeType;
            Connections = conns;
            SingleScene = singleScene;
            AdditiveScenes = additiveScenes;
            LoadOptions = loadOptions;
            LoadParams = loadParams;
            NetworkedScenes = networkedScenes;
            AsServer = asServer;
        }
    }


}