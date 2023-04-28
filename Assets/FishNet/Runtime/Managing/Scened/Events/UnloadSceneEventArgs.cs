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
        /// This collection may be populated with empty scenes depending on engine version.
        /// </summary>
        public int[] UnloadedSceneHandles
        {
            get
            {
                if (_unloadedSceneHandlesCache == null)
                {
                    _unloadedSceneHandlesCache = new int[UnloadedScenes.Count];
                    for (int i = 0; i < _unloadedSceneHandlesCache.Length; i++)
                        _unloadedSceneHandlesCache[i] = UnloadedScenes[i].handle;
                }

                return _unloadedSceneHandlesCache;
            }
        }
        /// <summary>
        /// Scenes which were successfully unloaded.
        /// This collection may be populated with empty scenes depending on engine version.
        /// </summary>
        public List<Scene> UnloadedScenes;
        /// <summary>
        /// Cache result of UnloadedSceneHandles.
        /// </summary>
        private int[] _unloadedSceneHandlesCache;

        internal SceneUnloadEndEventArgs(UnloadQueueData sqd, List<Scene> unloadedScenes)
        {
            QueueData = sqd;
            UnloadedScenes = unloadedScenes;
            _unloadedSceneHandlesCache = null;
        }
    }


}