
namespace FishNet.Managing.Scened
{
    /// <summary>
    /// Settings to apply when loading a scene.
    /// </summary>
    public class UnloadOptions
    {
        /// <summary>
        /// Conditions to unloading a scene on the server.
        /// </summary>
        public enum ServerUnloadModes
        {
            /// <summary>
            /// Unloads the scene if no more connections are within it.
            /// </summary>
            UnloadUnused = 0,
            /// <summary>
            /// Unloads scenes for connections but keeps scene loaded on server even if no connections are within it.
            /// </summary>
            KeepUnused = 1,
        }

        /// <summary>
        /// How to unload scenes on the server. UnloadUnused will unload scenes which have no more clients in them. KeepUnused will not unload a scene even when empty. ForceUnload will unload a scene regardless of if clients are still connected to it.
        /// </summary>
        [System.NonSerialized]
        public ServerUnloadModes Mode = ServerUnloadModes.UnloadUnused;
        /// <summary>
        /// True if scenes should be unloaded using addressables.
        /// </summary>
        public bool Addressables = false;
    }


}