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

            string proDefine = "FISHNET_PRO";
            string versionPrefix = "FISHNET_V";
            string thisVersion = $"{versionPrefix}3";
            string[] fishNetDefines = new string[]
            {
                "FISHNET",
                thisVersion,
                
            };
            bool modified = false;
            //Now add FN defines.
            foreach (string item in fishNetDefines)
                modified |= definesHs.Add(item);

            /* Remove pro define if not on pro. This might look a little
             * funny because the code below varies depending on if pro or not. */
            
#pragma warning disable CS0162 // Unreachable code detected
                modified |= definesHs.Remove(proDefine);
#pragma warning restore CS0162 // Unreachable code detected

            List<string> definesToRemove = new List<string>();
            int versionPrefixLength = versionPrefix.Length;
            //Remove old versions.
            foreach (string item in definesHs)
            {
                //Do not remove this version.
                if (item == thisVersion)
                    continue;

                //If length is possible to be a version prefix and is so then remove it.
                if (item.Length >= versionPrefixLength && item.Substring(0, versionPrefixLength) == versionPrefix)
                    definesToRemove.Add(item);
            }

            modified |= (definesToRemove.Count > 0);
            foreach (string item in definesToRemove)
            {
                definesHs.Remove(item);
                Debug.Log($"Removed unused Fish-Networking define {item}.");
            }

            if (modified)
            {
                Debug.Log("Added or removed Fish-Networking defines within player settings.");
                string changedDefines = string.Join(";", definesHs);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, changedDefines);
            }
        }
    }
}
#endif