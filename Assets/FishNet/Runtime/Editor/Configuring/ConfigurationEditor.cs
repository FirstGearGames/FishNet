#if UNITY_EDITOR
using FishNet.Editing.PrefabCollectionGenerator;
using FishNet.Object;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEditor;
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
        private const string STABLE_DEFINE = "FISHNET_STABLE_MODE";
        private const string QOL_ATTRIBUTES_DEFINE = "DISABLE_QOL_ATTRIBUTES";
        private const string DEVELOPER_ONLY_WARNING = "If you are not a developer or were not instructed to do this by a developer things are likely to break. You have been warned.";
        #endregion


        #region Release mode.
#if !FISHNET_STABLE_MODE
        [MenuItem("Tools/Fish-Networking/Switch to Stable", false, -1101)]
        private static void SwitchToStable()
        {
            bool result = RemoveOrAddDefine(STABLE_DEFINE, false);
            if (result)
                Debug.LogWarning($"Fish-Networking has been switched to Stable. Please note that experimental features may not function in this mode.");
        }
#else
        [MenuItem("Tools/Fish-Networking/Switch to Beta", false, -1101)]
        private static void SwitchToBeta()
        {
            bool result = RemoveOrAddDefine(STABLE_DEFINE, true);
            if (result)
                Debug.LogWarning($"Fish-Networking has been switched to Beta.");

        }
#endif
        #endregion
    
        #region QOL Attributes
#if DISABLE_QOL_ATTRIBUTES
        [MenuItem("Tools/Fish-Networking/Utility/Quality of Life Attributes/Enable", false, -999)]
        private static void EnableQOLAttributes()
        {
            bool result = RemoveOrAddDefine(QOL_ATTRIBUTES_DEFINE, true);
            if (result)
                Debug.LogWarning($"Quality of Life Attributes have been enabled.");
        }
#else
        [MenuItem("Tools/Fish-Networking/Utility/Quality of Life Attributes/Disable", false, -998)]
        private static void DisableQOLAttributes()
        {
            bool result = RemoveOrAddDefine(QOL_ATTRIBUTES_DEFINE, false);
            if (result)
                Debug.LogWarning($"Quality of Life Attributes have been disabled. {DEVELOPER_ONLY_WARNING}");
        }
#endif
        #endregion


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


    public class RebuildSceneIdMenu : MonoBehaviour
    {
        /// <summary>
        /// Rebuilds sceneIds for open scenes.
        /// </summary>
        [MenuItem("Tools/Fish-Networking/Rebuild SceneIds", false, 20)]
        public static void RebuildSceneIds()
        {
#if PARRELSYNC
            if (ParrelSync.ClonesManager.IsClone() && ParrelSync.Preferences.AssetModPref.Value)
            {
                Debug.Log("Cannot perform this operation on a ParrelSync clone");
                return;
            }
#endif
            if (ApplicationState.IsPlaying())
            {
                Debug.Log($"SceneIds cannot be rebuilt while in play mode.");
                return;
            }

            int checkedObjects = 0;
            int checkedScenes = 0;
            int changedObjects = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded)
                {
                    Debug.Log($"Skipped scene {s.name} because it is not loaded.");
                    continue;
                }

                checkedScenes++;
                NetworkObject.CreateSceneId(s, out int changed, out int found);
                checkedObjects += found;
                changedObjects += changed;
            }

            string saveText = (changedObjects > 0) ? " Please save your open scenes." : string.Empty;
            Debug.Log($"SceneIds were generated for {changedObjects} object(s) over {checkedScenes} scene(s). {checkedObjects} object(s) were checked in total..{saveText}");
        }


    }

    public class RefreshDefaultPrefabsMenu : MonoBehaviour
    {
        /// <summary>
        /// Rebuilds the DefaultPrefabsCollection file.
        /// </summary>
        [MenuItem("Tools/Fish-Networking/Refresh Default Prefabs", false, 22)]
        public static void RebuildDefaultPrefabs()
        {
#if PARRELSYNC
            if (ParrelSync.ClonesManager.IsClone() && ParrelSync.Preferences.AssetModPref.Value)
            {
                Debug.Log("Cannot perform this operation on a ParrelSync clone");
                return;
            }
#endif
            Debug.Log("Refreshing default prefabs.");
            Generator.GenerateFull(null, true);
        }

    }


    public class RemoveDuplicateNetworkObjectsMenu : MonoBehaviour
    {
        /// <summary>
        /// Iterates all network object prefabs in the project and open scenes, removing NetworkObject components which exist multiple times on a single object.
        /// </summary>
        [MenuItem("Tools/Fish-Networking/Remove Duplicate NetworkObjects", false, 21)]

        public static void RemoveDuplicateNetworkObjects()
        {
#if PARRELSYNC
            if (ParrelSync.ClonesManager.IsClone() && ParrelSync.Preferences.AssetModPref.Value)
            {
                Debug.Log("Cannot perform this operation on a ParrelSync clone");
                return;
            }
#endif
            List<NetworkObject> foundNobs = new List<NetworkObject>();

            foreach (string path in Generator.GetPrefabFiles("Assets", new HashSet<string>(), true))
            {
                NetworkObject nob = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
                if (nob != null)
                    foundNobs.Add(nob);
            }

            //Now add scene objects.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);

                List<NetworkObject> nobs = CollectionCaches<NetworkObject>.RetrieveList();
                Scenes.GetSceneNetworkObjects(s, false, false, true, ref nobs);
                foundNobs.AddRange(nobs);
                CollectionCaches<NetworkObject>.Store(nobs);
            }

            //Remove duplicates.
            int removed = 0;
            foreach (NetworkObject nob in foundNobs)
            {
                int count = nob.RemoveDuplicateNetworkObjects();
                if (count > 0)
                    removed += count;
            }
            
            Debug.Log($"Removed {removed} duplicate NetworkObjects.");
            if (removed > 0)
                RebuildSceneIdMenu.RebuildSceneIds();
        }

    }




}
#endif