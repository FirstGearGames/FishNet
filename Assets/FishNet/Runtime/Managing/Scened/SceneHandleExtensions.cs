using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened
{
    internal static class SceneHandleExtensions
    {
        /// <summary>
        /// Returns the raw handle identifier of a scene as a ulong.
        /// </summary>
        /// <remarks>
        /// Unity 6000.5+ changed Scene.handle to an EntityId struct exposing GetRawData().
        /// Earlier versions expose handle as an int.
        /// </remarks>
        public static ulong GetHandleId(this Scene s)
        {
#if UNITY_6000_5_OR_NEWER
            return s.handle.GetRawData();
#else
            return (ulong)(uint)s.handle;
#endif
        }
    }
}
