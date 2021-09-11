using FishNet.Managing.Scened.Data;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Eventing
{

    public struct SceneLoadStartEventArgs
    {
        /// <summary>
        /// RawData used by this current scene action.
        /// </summary>
        public readonly LoadQueueData RawData;

        public SceneLoadStartEventArgs(LoadQueueData lqd)
        {
            RawData = lqd;
        }
    }



    public struct SceneLoadPercentEventArgs
    {
        /// <summary>
        /// RawData used by this current scene action.
        /// </summary>
        public readonly LoadQueueData RawData;
        /// <summary>
        /// Percentage of change completion. 1f is equal to 100 complete.
        /// </summary>
        public readonly float Percent;

        public SceneLoadPercentEventArgs(LoadQueueData lqd, float percent)
        {
            RawData = lqd;
            Percent = percent;
        }
    }



    public struct SceneLoadEndEventArgs
    {
        /// <summary>
        /// RawData used by this current scene action.
        /// </summary>
        public readonly LoadQueueData RawData;
        /// <summary>
        /// Scenes which were loaded.
        /// </summary>
        public readonly Scene[] LoadedScenes;
        /// <summary>
        /// Scenes which were skipped because they were already loaded.
        /// </summary>
        public readonly string[] SkippedSceneNames;

        public SceneLoadEndEventArgs(LoadQueueData lqd, Scene[] loaded, string[] skipped)
        {
            RawData = lqd;
            LoadedScenes = loaded;
            SkippedSceneNames = skipped;
        }
    }

}
