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
            string[] fishNetDefines = new string[]
            {
                "FISHNET",
                //PROSTART
                proDefine,
                //PROEND
            };
            bool modified = false;
            //Now add FN defines.
            foreach (string item in fishNetDefines)
                modified |= definesHs.Add(item);

            /* Remove pro define if not on pro. This might look a little
             * funny because the code below varies depending on if pro or not. */
            //PROSTART
            if (1 == 2)
                //PROEND
#pragma warning disable CS0162 // Unreachable code detected
                modified |= definesHs.Remove(proDefine);
#pragma warning restore CS0162 // Unreachable code detected

            if (modified)
            {
                Debug.Log("Added Fish-Networking defines to player settings.");
                string changedDefines = string.Join(";", definesHs);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, changedDefines);
            }
        }
    }
}
#endif