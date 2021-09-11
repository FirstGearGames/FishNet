using FishNet.Connection;
using FishNet.Serializing.Helping;

namespace FishNet.Managing.Scened.Data
{


    /// <summary>
    /// Used to load a scene for a targeted connection.
    /// </summary>
   //[CodegenIncludeInternal]
    public class LoadQueueData
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
        /// SceneLoadData to use.
        /// </summary>
        public SceneLoadData SceneLoadData = null;
        /// <summary>
        /// Current global scenes.
        /// </summary>
        public string[] GlobalScenes = new string[0];
        /// <summary>
        /// True if to iterate this queue data as server.
        /// </summary>
        [System.NonSerialized]
        public readonly bool AsServer;

        public LoadQueueData() { }

        /// <summary>
        /// Creates a SceneQueueData.
        /// </summary>
        /// /// <param name="singleScene"></param>
        /// <param name="additiveScenes"></param>
        /// <param name="loadOnlyUnloaded"></param>
        public LoadQueueData(SceneScopeTypes scopeType, NetworkConnection[] conns, SceneLoadData sceneLoadData, string[] globalScenes, bool asServer)
        {
            ScopeType = scopeType;
            Connections = conns;
            SceneLoadData = sceneLoadData;
            GlobalScenes = globalScenes;
            AsServer = asServer;
        }
    }


}