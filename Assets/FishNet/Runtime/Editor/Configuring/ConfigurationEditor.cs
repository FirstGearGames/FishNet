#if UNITY_EDITOR
using FishNet.Editing.PrefabCollectionGenerator;
using FishNet.Object;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using System.Reflection;
using FishNet.Configuring.EditorCloning;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Editing
{
    public class ConfigurationEditor : EditorWindow
    {
        [MenuItem("Tools/Fish-Networking/Configuration", false, 0)]
        public static void ShowConfiguration()
        {
            SettingsService.OpenProjectSettings("Project/Fish-Networking/Configuration");
        }
    }

    public class DeveloperMenu : MonoBehaviour
    {
        #region const.
        private const string QOL_ATTRIBUTES_DEFINE = "DISABLE_QOL_ATTRIBUTES";
        private const string DEVELOPER_ONLY_WARNING = "If you are not a developer or were not instructed to do this by a developer things are likely to break. You have been warned.";
        #endregion

        #region QOL Attributes
        #if DISABLE_QOL_ATTRIBUTES
        [MenuItem("Tools/Fish-Networking/Utility/Quality of Life Attributes/Enable", false, -999)]
        private static void EnableQOLAttributes()
        {
            bool result = RemoveOrAddDefine(QOL_ATTRIBUTES_DEFINE, removeDefine: true);
            if (result)
                Debug.LogWarning($"Quality of Life Attributes have been enabled.");
        }
        #else
        [MenuItem("Tools/Fish-Networking/Utility/Quality of Life Attributes/Disable", false, 0)]
        private static void DisableQOLAttributes()
        {
            bool result = RemoveOrAddDefine(QOL_ATTRIBUTES_DEFINE, removeDefine: false);
            if (result)
                Debug.LogWarning($"Quality of Life Attributes have been disabled. {DEVELOPER_ONLY_WARNING}");
        }
        #endif
        #endregion

        internal static bool RemoveOrAddDefine(string define, bool removeDefine)
        {
            #if UNITY_6000_1_OR_NEWER
            NamedBuildTarget activeTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            #endif

            #if UNITY_6000_1_OR_NEWER
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(activeTarget);
            #else
            string currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            #endif
            
            HashSet<string> definesHs = new();
            string[] currentArr = currentDefines.Split(';');

            // Add any define which doesn't contain MIRROR.
            foreach (string item in currentArr)
                definesHs.Add(item);

            int startingCount = definesHs.Count;

            if (removeDefine)
                definesHs.Remove(define);
            else
                definesHs.Add(define);

            bool modified = definesHs.Count != startingCount;
            if (modified)
            {
                string changedDefines = string.Join(";", definesHs);
                #if UNITY_6000_1_OR_NEWER
                PlayerSettings.SetScriptingDefineSymbols(activeTarget, changedDefines);
                #else
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, changedDefines);
                #endif
            }

            return modified;
        }

        /// <summary>
        /// Adds or removes a define for every build target rather than only the selected one.
        /// </summary>
        /// <remarks>Use this for defines which change what is sent over the network, so builds for different targets, such as a dedicated server and its clients, cannot disagree.</remarks>
        internal static bool RemoveOrAddDefineForAllBuildTargets(string define, bool removeDefine)
        {
            HashSet<string> visitedTargets = new();
            bool modified = false;

            /* Fields are iterated rather than values because an obsolete alias shares its
             * value with a current group, and the value's name could be either. */
            foreach (FieldInfo field in typeof(BuildTargetGroup).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsDefined(typeof(System.ObsoleteAttribute), inherit: false))
                    continue;
                BuildTargetGroup group = (BuildTargetGroup)field.GetValue(null);
                if (group == BuildTargetGroup.Unknown)
                    continue;

                NamedBuildTarget target;
                try
                {
                    target = NamedBuildTarget.FromBuildTargetGroup(group);
                }
                catch (System.ArgumentException)
                {
                    continue;
                }

                if (visitedTargets.Add(target.TargetName))
                    modified |= RemoveOrAddDefine(target, define, removeDefine);
            }

            // Dedicated server builds have their own defines, which no BuildTargetGroup maps to.
            if (visitedTargets.Add(NamedBuildTarget.Server.TargetName))
                modified |= RemoveOrAddDefine(NamedBuildTarget.Server, define, removeDefine);

            return modified;
        }

        /// <summary>
        /// Adds or removes a define for a build target.
        /// </summary>
        private static bool RemoveOrAddDefine(NamedBuildTarget target, string define, bool removeDefine)
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(target);
            HashSet<string> definesHs = new(currentDefines.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));

            bool modified = removeDefine ? definesHs.Remove(define) : definesHs.Add(define);
            if (modified)
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", definesHs));

            return modified;
        }
    }

    public class RefreshDefaultPrefabsMenu : MonoBehaviour
    {
        /// <summary>
        /// Rebuilds the DefaultPrefabsCollection file.
        /// </summary>
        [MenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs", false, 300)]
        public static void RebuildDefaultPrefabs()
        {
            if (!CloneChecker.CanGenerateFiles())
            {
                Debug.Log("Skipping prefab generation as clone settings does not allow it.");
                return;
            }
            Debug.Log("Refreshing default prefabs.");
            Generator.GenerateFull(null, true);
        }
    }
}

#endif