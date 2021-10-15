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
        public int[] UnloadedSceneHandles;

        internal SceneUnloadEndEventArgs(UnloadQueueData sqd, int[] unloadedHandles)
        {
            QueueData = sqd;
            UnloadedSceneHandles = unloadedHandles;
        }
    }


}