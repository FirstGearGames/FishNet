﻿#if UNITY_EDITOR
using FishNet.Editing.PrefabCollectionGenerator;
using FishNet.Object;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Editing
{
    public class ConfigurationEditor : EditorWindow
    {

        [MenuItem("Fish-Networking/Configuration", false, 0)]
        public static void ShowConfiguration()
        {
            SettingsService.OpenProjectSettings("Project/Fish-Networking/Configuration");
        }

    }

    public class DeveloperMenu : MonoBehaviour
    {
        #region const.
        private const string PREDICTIONV2_DEFINE = "PREDICTION_V2";
        private const string NETWORK_LOD_DEFINE = "NETWORK_LOD";
        private const string QOL_ATTRIBUTES_DEFINE = "DISABLE_QOL_ATTRIBUTES";
        private const string DEVELOPER_ONLY_WARNING = "If you are not a developer or were not instructed to do this by a developer things are likely to break. You have been warned.";
        #endregion

        #region PredictionV2.
        [MenuItem("Fish-Networking/Developer/PredictionV2/Enable", false, -999)]
        private static void EnablePredictionV2()
        {
            bool result = RemoveOrAddDefine(PREDICTIONV2_DEFINE, false);
            if (result)
                Debug.LogWarning($"PredictionV2 has been enabled. {DEVELOPER_ONLY_WARNING}");
        }
        [MenuItem("Fish-Networking/Developer/PredictionV2/Disable", false, -998)]
        private static void DisablePredictionV2()
        {
            bool result = RemoveOrAddDefine(PREDICTIONV2_DEFINE, true);
            if (result)
                Debug.Log("PredictionV2 has been disabled.");
        }
        #endregion

        #region Network LOD.
        [MenuItem("Fish-Networking/Developer/Network LOD/Enable", false, -999)]
        private static void EnableNetworkLOD()
        {
            bool result = RemoveOrAddDefine(NETWORK_LOD_DEFINE, false);
            if (result)
                Debug.LogWarning($"Network LOD has been enabled. {DEVELOPER_ONLY_WARNING}");
        }
        [MenuItem("Fish-Networking/Developer/Network LOD/Disable", false, -998)]
        private static void DisableNetworkLOD()
        {
            bool result = RemoveOrAddDefine(NETWORK_LOD_DEFINE, true);
            if (result)
                Debug.Log("Network LOD has been disabled.");
        }
        #endregion

        #region QOL Attributes
        [MenuItem("Fish-Networking/Developer/Quality of Life Attributes/Enable", false, -999)]
        private static void EnableQOLAttributes()
        {
            bool result = RemoveOrAddDefine(QOL_ATTRIBUTES_DEFINE, true);
            if (result)
                Debug.LogWarning($"Quality of Life Attributes have been enabled.");
        }
        [MenuItem("Fish-Networking/Developer/Quality of Life Attributes/Disable", false, -998)]
        private static void DisableQOLAttributes()
        {
            bool result = RemoveOrAddDefine(QOL_ATTRIBUTES_DEFINE, false);
            if (result)
                Debug.LogWarning($"Quality of Life Attributes have been disabled. {DEVELOPER_ONLY_WARNING}");
        }
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
        [MenuItem("Fish-Networking/Rebuild SceneIds", false, 20)]
        public static void RebuildSceneIds()
        {
#if PARRELSYNC
            if (ParrelSync.ClonesManager.IsClone() && ParrelSync.Preferences.AssetModPref.Value)
            {
                Debug.Log("Cannot perform this operation on a ParrelSync clone");
                return;
            }
#endif

            int generatedCount = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);

                List<NetworkObject> nobs = CollectionCaches<NetworkObject>.RetrieveList();
                SceneFN.GetSceneNetworkObjects(s, false, ref nobs);
                int nobCount = nobs.Count;
                for (int z = 0; z < nobCount; z++)
                {
                    NetworkObject nob = nobs[z];
                    nob.TryCreateSceneID();
                    EditorUtility.SetDirty(nob);
                }
                generatedCount += nobCount;

                CollectionCaches<NetworkObject>.Store(nobs);
            }

            Debug.Log($"Generated sceneIds for {generatedCount} objects over {SceneManager.sceneCount} scenes. Please save your open scenes.");
        }


    }

    public class RefreshDefaultPrefabsMenu : MonoBehaviour
    {
        /// <summary>
        /// Rebuilds the DefaultPrefabsCollection file.
        /// </summary>
        [MenuItem("Fish-Networking/Refresh Default Prefabs", false, 22)]
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
        [MenuItem("Fish-Networking/Remove Duplicate NetworkObjects", false, 21)]

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
                SceneFN.GetSceneNetworkObjects(s, false, ref nobs);
                int nobsCount = nobs.Count;
                for (int z = 0; z < nobsCount; z++)
                {
                    NetworkObject nob = nobs[z];
                    nob.TryCreateSceneID();
                    EditorUtility.SetDirty(nob);
                }
                for (int z = 0; z < nobsCount; z++)
                    foundNobs.Add(nobs[i]);

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

            Debug.Log($"Removed {removed} duplicate NetworkObjects. Please save your open scenes and project.");
        }

    }




}
#endif