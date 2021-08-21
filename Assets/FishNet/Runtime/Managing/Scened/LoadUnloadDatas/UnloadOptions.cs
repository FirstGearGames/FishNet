
namespace FishNet.Managing.Scened.Data
{
    public class UnloadOptions
    {
        public enum UnloadModes
        {
            UnloadUnused = 0,
            KeepUnused = 1,
            ForceUnload = 2
        }

        /// <summary>
        /// How to unload scenes on the server. UnloadUnused will unload scenes which have no more clients in them. KeepUnused will not unload a scene even when empty. ForceUnload will unload a scene regardless of if clients are still connected to it.
        /// </summary>
        [System.NonSerialized]
        public UnloadModes Mode = UnloadModes.UnloadUnused;
        /// <summary>
        /// Parameters which can be passed into a scene load. Params can be useful to link personalized data with scene load callbacks, such as a match Id.
        /// </summary>
        [System.NonSerialized]
        public object[] Params = null;
    }


}