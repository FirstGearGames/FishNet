#if UNITY_EDITOR
using FishNet.Documenting;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FishNet.Editing.Upgrading
{
    /* IMPORTANT IMPORTANT IMPORTANT IMPORTANT 
    * If you receive errors about missing Mirror components,
    * such as NetworkIdentity, then remove MIRROR and any other
    * MIRROR defines.
    * Project Settings -> Player -> Other -> Scripting Define Symbols.
    * 
    * If you are also using my assets add FGG_ASSETS to the defines, and
    * then remove it after running this script. */
    [APIExclude]
    public class UpgradeFromV3ToV4Menu : MonoBehaviour
    {
        public const string EnabledWarning = "FishNet 3 to FishNet 4 upgrade helpers are currently enabled. This will result in longer compile times for code changes. If you are no longer seeing any errors in your console related to upgrading consider disabling this feature in the Fish-Networking menu, Utility, Upgrading.";
        private const string DISABLE_V3TOV4_HELPERS_DEFINE = "FISHNET_DISABLE_V3TOV4_HELPERS";

#if !FISHNET_DISABLE_V3TOV4_HELPERS
        [MenuItem("Tools/Fish-Networking/Utility/Upgrading/FishNet 3 to FishNet 4/Disable Helpers", false, -1100)]
        private static void Disable3To4Helpers()
        {
            bool result = RemoveOrAddDefine(DISABLE_V3TOV4_HELPERS_DEFINE, false);
            if (result)
                Debug.LogWarning($"Version 3 to Version 4 migration helpers have been disabled.");
        }
#else
        [MenuItem("Tools/Fish-Networking/Utility/Upgrading/FishNet 3 to FishNet 4/Enable Helpers", false, -1100)]
        private static void Enable3To4Helpers()
        {
            bool result = RemoveOrAddDefine(DISABLE_V3TOV4_HELPERS_DEFINE, true);
            if (result)
                Debug.LogWarning($"Version 3 to Version 4 migration helpers have been enabled.");
        }
#endif

        private static bool RemoveOrAddDefine(string define, bool removeDefine)
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            HashSet<string> definesHs = new HashSet<string>();
            string[] currentArr = currentDefines.Split(';');

            //Add any define which doesn't contain MIRROR.
            foreach (string item in currentArr)
                definesHs.Add(item);

            int startingCount = definesHs.Count;

            if (removeDefine)
                definesHs.Remove(define);
            else
                definesHs.Add(define);

            bool modified = (definesHs.Count != startingCount);
            if (modified)
            {
                string changedDefines = string.Join(";", definesHs);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, changedDefines);
            }

            return modified;
        }



    }
}
#endif
