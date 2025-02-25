#if UNITY_EDITOR
using FishNet.Editing.PrefabCollectionGenerator;
using FishNet.Object;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Editing.Beta
{
    public class BetaModeMenu : MonoBehaviour
    {
        #region const.
        private const string STABLE_SYNCTYPES_DEFINE = "FISHNET_STABLE_SYNCTYPES";
        #endregion

        #region Beta SyncTypes
#if FISHNET_STABLE_SYNCTYPES
        [MenuItem("Tools/Fish-Networking/Beta/Enable for SyncTypes", false, -1101)]
        private static void EnableBetaSyncTypes() => SetBetaSyncTypes(enabled: true);
#else
        [MenuItem("Tools/Fish-Networking/Beta/Disable for SyncTypes", false, -1101)]
        private static void DisableBetaSyncTypes() => SetBetaSyncTypes(enabled: true);
#endif
        private static void SetBetaSyncTypes(bool enabled)
        {
            bool result = DeveloperMenu.RemoveOrAddDefine(STABLE_SYNCTYPES_DEFINE, !enabled);
            if (result)
            {
                string enabledState = (enabled) ? "enabled" : "disabled";
                Debug.LogWarning($"Beta SyncTypes are now {enabledState}.");
            }
        }
        #endregion
    }
}

#endif