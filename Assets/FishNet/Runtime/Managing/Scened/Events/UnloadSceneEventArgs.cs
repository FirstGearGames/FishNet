using FishNet.Managing.Scened.Data;

namespace FishNet.Managing.Scened.Eventing
{


    public struct SceneUnloadStartEventArgs
    {
        /// <summary>
        /// RawData used by the current scene action.
        /// </summary>
        public readonly UnloadQueueData RawData;

        public SceneUnloadStartEventArgs(UnloadQueueData sqd)
        {
            RawData = sqd;
        }
    }

    public struct SceneUnloadEndEventArgs
    {
        /// <summary>
        /// RawData used by the current scene action.
        /// </summary>
        public readonly UnloadQueueData RawData;
        /// <summary>
        /// Handles of scenes which were successfully unloaded.
        /// </summary>
        public int[] UnloadedSceneHandles;

        public SceneUnloadEndEventArgs(UnloadQueueData sqd, int[] unloadedHandles)
        {
            RawData = sqd;
            UnloadedSceneHandles = unloadedHandles;
        }
    }


}