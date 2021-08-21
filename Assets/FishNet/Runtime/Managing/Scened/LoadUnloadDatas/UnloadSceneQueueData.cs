using FishNet.Connection;
using System.Collections.Generic;
using System.Linq;

namespace FishNet.Managing.Scened.Data
{


    public class UnloadSceneQueueData
    {
        /// <summary>
        /// Clients which receive this SceneQueueData. If Networked, all clients do. If Connections, only the specified Connections do.
        /// </summary>
        [System.NonSerialized]
        public readonly SceneScopeTypes ScopeType;
        /// <summary>
        /// Additive scenes to unload.
        /// </summary>
        public AdditiveScenesData AdditiveScenes = null;
        /// <summary>
        /// Current data on networked scenes.
        /// </summary>
        public NetworkedScenesData NetworkedScenes = null;
        /// <summary>
        /// Connections to unload scenes for. Only valid on the server and when ScopeType is Connections.
        /// </summary>
        [System.NonSerialized]
        public NetworkConnection[] Connections;
        /// <summary>
        /// True if to iterate this queue data as server.
        /// </summary>
        [System.NonSerialized]
        public readonly bool AsServer;
        /// <summary>
        /// Unload options for this scene queue data. This is only available on the server.
        /// </summary>
        [System.NonSerialized]
        public readonly UnloadOptions UnloadOptions = new UnloadOptions();
        /// <summary>
        /// Unload params for this scene queue data.
        /// </summary>
        [System.NonSerialized]
        public readonly UnloadParams UnloadParams = new UnloadParams();


        public UnloadSceneQueueData() { }
        /// <summary>
        /// Creates a SceneQueueData.
        /// </summary>
        /// /// <param name="singleScene"></param>
        /// <param name="additiveScenes"></param>
        public UnloadSceneQueueData(SceneScopeTypes scopeType, NetworkConnection[] conns, AdditiveScenesData additiveScenes, UnloadOptions unloadOptions, UnloadParams unloadParams, NetworkedScenesData networkedScenes, bool asServer)
        {
            ScopeType = scopeType;
            Connections = conns;
            AdditiveScenes = additiveScenes;
            UnloadOptions = unloadOptions;
            UnloadParams = unloadParams;
            NetworkedScenes = networkedScenes;
            AsServer = asServer;
        }

      
    }



}