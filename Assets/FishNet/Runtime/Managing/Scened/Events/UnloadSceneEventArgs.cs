using FishNet.Managing.Scened.Data;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Eventing
{


    public struct UnloadSceneStartEventArgs
    {
        /// <summary>
        /// RawData used by the current scene action.
        /// </summary>
        public readonly UnloadSceneQueueData RawData;

        public UnloadSceneStartEventArgs(UnloadSceneQueueData sqd)
        {
            RawData = sqd;
        }
    }

    public struct UnloadSceneEndEventArgs
    {
        /// <summary>
        /// RawData used by the current scene action.
        /// </summary>
        public readonly UnloadSceneQueueData RawData;
        /// <summary>
        /// Handles of scenes which were successfully unloaded.
        /// </summary>
        public int[] UnloadedSceneHandles;

        public UnloadSceneEndEventArgs(UnloadSceneQueueData sqd, int[] unloadedHandles)
        {
            RawData = sqd;
            UnloadedSceneHandles = unloadedHandles;
        }
    }


}