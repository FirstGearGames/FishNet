using FishNet.Utility.Extension;
using UnityEngine.SceneManagement;
#if UNITY_6000_5_OR_NEWER
using SceneHandle = System.UInt64;
#else
using SceneHandle = System.Int32;
#endif

namespace FishNet.Managing.Scened
{
    public struct UnloadedScene
    {
        public readonly string Name;
        public readonly SceneHandle Handle;

        public UnloadedScene(Scene s)
        {
            Name = s.name;
            Handle = s.GetRawHandle();
        }

        public UnloadedScene(string name, int handle)
        {
            Name = name;
            Handle = Scenes.ToRawHandle(handle);
        }

        /// <summary>
        /// Returns a scene based on handle.
        /// Result may not be valid as some Unity versions discard of the scene information after unloading.
        /// </summary>
        /// <returns></returns>
        public Scene GetScene()
        {
            int loadedScenes = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < loadedScenes; i++)
            {
                Scene s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.GetRawHandle() == Handle)
                    return s;
            }

            return default;
        }
    }
}