using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened.Data
{
    /// <summary>
    /// Settings to apply when loading a scene.
    /// </summary>
    public class LoadOptions
    {
        /// <summary>
        /// True if to automatically unload the loaded scenes when they are no longer being used.
        /// </summary>
        [System.NonSerialized]
        public bool AutomaticallyUnload = true;
        /// <summary>
        /// True if to only load scenes which are not yet loaded. When false a scene may load multiple times; this is known as scene stacking. Only the server is able to stack scenes; clients will load a single instance.
        /// </summary>
        [System.NonSerialized]
        public bool DisallowStacking = true;
        /// <summary>
        /// LocalPhysics mode to use when loading this scene. Generally this will only be used when applying scene stacking. Only used by the server.
        /// https://docs.unity3d.com/ScriptReference/SceneManagement.LocalPhysicsMode.html
        /// </summary>
        [System.NonSerialized]
        public LocalPhysicsMode LocalPhysics = LocalPhysicsMode.None;
    }


}