#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FishNet
{
    internal static class ScriptingDefines
    {
        [InitializeOnLoadMethod]
        public static void AddDefineSymbols()
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            /* Convert current defines into a hashset. This is so we can
             * determine if any of our defines were added. Only save playersettings
             * when a define is added. */
            HashSet<string> definesHs = new HashSet<string>();
            string[] currentArr = currentDefines.Split(';');
            //Add current defines into hs.
            foreach (string item in currentArr)
                definesHs.Add(item);

            string[] fishNetDefines = new string[]
            {
                "FISHNET"
            };
            bool added = false;
            //Now add FN defines.
            foreach (string item in fishNetDefines)
                added |= definesHs.Add(item);

            if (added)
            {
                Debug.Log("Added Fish-Networking defines to player settings.");
                string changedDefines = string.Join(";", definesHs);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, changedDefines);
            }
        }
    }
}
#endif