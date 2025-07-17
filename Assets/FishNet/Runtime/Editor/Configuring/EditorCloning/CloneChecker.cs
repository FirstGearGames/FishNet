using UnityEngine;

namespace FishNet.Configuring.EditorCloning
{
    public static class CloneChecker
    {
        /// <summary>
        /// Returns true if this editor is a multiplayer clone.
        /// </summary>
        /// <returns></returns>
        public static bool IsMultiplayerClone(out EditorCloneType editorCloneType)
        {
            if (Application.dataPath.ToLower().Contains("library/vp/"))
            {
                editorCloneType = EditorCloneType.UnityMultiplayer;
                return true;
            }

#if PARRELSYNC && UNITY_EDITOR
            if (ParrelSync.ClonesManager.IsClone())
            {
                editorCloneType = EditorCloneType.ParrelSync;
                return true;
            }
#endif
            editorCloneType = EditorCloneType.None;

            return false;
        }
    }
}