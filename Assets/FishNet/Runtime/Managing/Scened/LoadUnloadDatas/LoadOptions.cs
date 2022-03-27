using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened
{
    /// <summary>
    /// Settings to apply when loading a scene.
    /// </summary>
    public class LoadOptions
    {
        /// <summary>
        /// True if to automatically unload the loaded scenes when they are no longer being used by clients. This field only applies to scenes loaded for connections, not globally loaded scenes.
        /// </summary>
        [System.NonSerialized]
        public bool AutomaticallyUnload = true;
        /// <summary>
        /// True if to only load scenes which are not yet loaded. When false a scene may load multiple times; this is known as scene stacking. Only the server is able to stack scenes; clients will load a single instance. Global scenes cannot be stacked.
        /// </summary>
        [System.NonSerialized]
        public bool DisallowStacking = true; //Remove on 2022/06/01 in favor of AllowStacking.
        ///// <summary>
        ///// False if to only load scenes which are not yet loaded. When true a scene may load multiple times; this is known as scene stacking. Only the server is able to stack scenes; clients will load a single instance. Global scenes cannot be stacked.
        ///// </summary>
        //[System.NonSerialized]
        //public bool AllowStacking = true; //Remove on 2022/06/01 in favor of AllowStacking.
        /// <summary>
        /// LocalPhysics mode to use when loading this scene. Generally this will only be used when applying scene stacking. Only used by the server.
        /// https://docs.unity3d.com/ScriptReference/SceneManagement.LocalPhysicsMode.html
        /// </summary>
        [System.NonSerialized]
        public LocalPhysicsMode LocalPhysics = LocalPhysicsMode.None;
        /// <summary>
        /// True if scenes should be loaded using addressables.
        /// </summary>
        public bool Addressables = false;
    }


}