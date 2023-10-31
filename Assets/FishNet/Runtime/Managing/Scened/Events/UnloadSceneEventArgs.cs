using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened
{

    /// <summary>
    /// Data container about a scene unload start.
    /// </summary>
    public struct SceneUnloadStartEventArgs
    {
        /// <summary>
        /// Queue data used by the current scene action.
        /// </summary>
        public readonly UnloadQueueData QueueData;

        internal SceneUnloadStartEventArgs(UnloadQueueData sqd)
        {
            QueueData = sqd;
        }
    }

    /// <summary>
    /// Data container about a scene unload end.
    /// </summary>
    public struct SceneUnloadEndEventArgs
    {
        /// <summary>
        /// Queue data used by the current scene action.
        /// </summary>
        public readonly UnloadQueueData QueueData;
        /// <summary>
        /// Scenes which were successfully unloaded.
        /// This collection may be populated with empty scenes depending on engine version.
        /// </summary>
        public List<Scene> UnloadedScenes;

        internal SceneUnloadEndEventArgs(UnloadQueueData sqd, List<Scene> unloadedScenes, List<UnloadedScene> newUnloadedScenes)
        {
            QueueData = sqd;
            UnloadedScenes = unloadedScenes;
        }
    }


}