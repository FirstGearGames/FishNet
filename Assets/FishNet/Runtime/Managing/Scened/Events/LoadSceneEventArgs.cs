using FishNet.Managing.Scened.Data;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Eventing
{

    public struct LoadSceneStartEventArgs
    {
        /// <summary>
        /// RawData used by the current scene action.
        /// </summary>
        public readonly LoadSceneQueueData RawData;

        public LoadSceneStartEventArgs(LoadSceneQueueData sqd)
        {
            RawData = sqd;
        }
    }



    public struct LoadScenePercentEventArgs
    {
        /// <summary>
        /// RawData used by the current scene action.
        /// </summary>
        public readonly LoadSceneQueueData RawData;
        /// <summary>
        /// Percentage of change completion. 1f is equal to 100 complete.
        /// </summary>
        public readonly float Percent;

        public LoadScenePercentEventArgs(LoadSceneQueueData sqd, float percent)
        {
            RawData = sqd;
            Percent = percent;
        }
    }



    public struct LoadSceneEndEventArgs
    {
        /// <summary>
        /// RawData used by the current scene action.
        /// </summary>
        public readonly LoadSceneQueueData RawData;
        /// <summary>
        /// Scenes which were loaded.
        /// </summary>
        public readonly Scene[] LoadedScenes;
        /// <summary>
        /// Scenes which were skipped because they were already loaded.
        /// </summary>
        public readonly string[] SkippedSceneNames;

        public LoadSceneEndEventArgs(LoadSceneQueueData sqd, Scene[] loaded, string[] skipped)
        {
            RawData = sqd;
            LoadedScenes = loaded;
            SkippedSceneNames = skipped;
        }
    }

}
