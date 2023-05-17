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
        /// Handles of scenes which were successfully unloaded.
        /// </summary>
        [Obsolete("Use UnloadedScenesV2")]  //Remove on 2023/06/01
        public int[] UnloadedSceneHandles;
        /// <summary>
        /// Names of scenes which were successfully unloaded.
        /// </summary>
        [Obsolete("Use UnloadedScenesV2")] //Remove on 2023/06/01
        public string[] UnloadedSceneNames;

        /// <summary>
        /// Scenes which were successfully unloaded.
        /// This collection may be populated with empty scenes depending on engine version.
        /// </summary>
        public List<Scene> UnloadedScenes;
        /// <summary>
        /// Unloaded scenes with names and handles cached.
        /// This will be renamed as UnloadedScenes in Fish-Networking version 4.
        /// </summary>
        public List<UnloadedScene> UnloadedScenesV2;

        internal SceneUnloadEndEventArgs(UnloadQueueData sqd, List<Scene> unloadedScenes, List<UnloadedScene> newUnloadedScenes)
        {
            QueueData = sqd;
            UnloadedScenes = unloadedScenes;
            UnloadedScenesV2 = newUnloadedScenes;

#pragma warning disable CS0618 // Type or member is obsolete
            UnloadedSceneNames = new string[newUnloadedScenes.Count];
            UnloadedSceneHandles = new int[newUnloadedScenes.Count];            
            for (int i = 0; i < newUnloadedScenes.Count; i++)
            {
                UnloadedSceneNames[i] = newUnloadedScenes[i].Name;
                UnloadedSceneHandles[i] = newUnloadedScenes[i].Handle;
            }
#pragma warning restore CS0618
        }
    }


}