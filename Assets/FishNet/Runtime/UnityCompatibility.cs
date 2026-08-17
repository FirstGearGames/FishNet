using UnityEngine.SceneManagement;

namespace FishNet.Utility
{
    /// <summary>
    /// Compatibility helpers for Unity API changes across supported Unity versions.
    /// </summary>
    public static class UnityCompatibility
    {
        /// <summary>
        /// Returns a runtime object identifier using Unity's current object identity API.
        /// </summary>
        public static ulong GetObjectRuntimeId(UnityEngine.Object obj)
        {
#if UNITY_6000_5_OR_NEWER
            return obj.GetEntityId().GetRawData();
#else
            return unchecked((uint)obj.GetInstanceID());
#endif
        }

        /// <summary>
        /// Returns a raw scene handle value using Unity's current SceneHandle API.
        /// </summary>
        public static ulong GetSceneHandleRaw(Scene scene)
        {
#if UNITY_6000_5_OR_NEWER
            return scene.handle.GetRawData();
#else
            return unchecked((uint)(int)scene.handle);
#endif
        }

        /// <summary>
        /// Creates a SceneHandle from raw scene handle data.
        /// </summary>
        public static SceneHandle SceneHandleFromRaw(ulong rawHandle)
        {
#if UNITY_6000_5_OR_NEWER
            return SceneHandle.FromRawData(rawHandle);
#else
            return (SceneHandle)(int)rawHandle;
#endif
        }

        /// <summary>
        /// Returns true if the scene has a non-zero raw handle.
        /// </summary>
        public static bool HasValidSceneHandle(Scene scene)
        {
            return scene.IsValid() && GetSceneHandleRaw(scene) != 0;
        }
    }
}