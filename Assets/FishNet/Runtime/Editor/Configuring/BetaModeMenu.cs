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
        private const string STABLE_REPLICATESTATES_DEFINE = "FISHNET_STABLE_REPLICATESTATES";
        private const string STABLE_RECURSIVE_DESPAWNS_DEFINE = "FISHNET_STABLE_RECURSIVE_DESPAWNS";
        #endregion

        #region Beta Recursive Despawns
#if FISHNET_STABLE_RECURSIVE_DESPAWNS
        [MenuItem("Tools/Fish-Networking/Beta/Enable for Recursive Despawns", false, -1101)]
        private static void EnableBetaRecursiveDespawns() => SetBetaRecursiveDespawns(useStable: false);
#else
        [MenuItem("Tools/Fish-Networking/Beta/Disable for Recursive Despawns", false, -1101)]
        private static void DisableBetaRecursiveDespawns() => SetBetaRecursiveDespawns(useStable: true);
#endif
        private static void SetBetaRecursiveDespawns(bool useStable)
        {
            bool result = DeveloperMenu.RemoveOrAddDefine(STABLE_RECURSIVE_DESPAWNS_DEFINE, removeDefine: !useStable);
            if (result)
                Debug.LogWarning($"Beta Recursive Despawns are now {GetBetaEnabledText(useStable)}.");
        }
        #endregion

        #region Beta ReplicateStates
#if FISHNET_STABLE_REPLICATESTATES
        [MenuItem("Tools/Fish-Networking/Beta/Enable for ReplicateStates", false, -1101)]
        private static void EnableBetaReplicateStates() => SetBetaReplicateStates(useStable: false);
#else
        [MenuItem("Tools/Fish-Networking/Beta/Disable for ReplicateStates", false, -1101)]
        private static void DisableBetaReplicateStates() => SetBetaReplicateStates(useStable: true);
#endif
        private static void SetBetaReplicateStates(bool useStable)
        {
            bool result = DeveloperMenu.RemoveOrAddDefine(STABLE_REPLICATESTATES_DEFINE, removeDefine: !useStable);
            if (result)
                Debug.LogWarning($"Beta ReplicateStates are now {GetBetaEnabledText(useStable)}.");
        }
        #endregion

        private static string GetBetaEnabledText(bool useStable)
        {
            return useStable ? "disabled" : "enabled";
        }
    }
}

#endif