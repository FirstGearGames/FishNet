#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FishNet.Object;
using FishNet.Managing.Object;
using System.IO;

namespace FishNet.Editing
{

    [InitializeOnLoad]
    internal static class DefaultPrefabsFinder
    {
        [System.NonSerialized]
        private static bool _initialized = false;
        private static DefaultPrefabObjects _defaultPrefabs = null;

        static DefaultPrefabsFinder()
        {
            EditorApplication.update += InitializeOnce;
        }


        /// <summary>
        /// FInds and sets the default prefabs reference.
        /// </summary>
        internal static DefaultPrefabObjects GetDefaultPrefabsFile(out bool justPopulated)
        {
            string[] guids;
            string[] objectPaths;

            if (_defaultPrefabs == null)
            {
                guids = AssetDatabase.FindAssets("t:ScriptableObject", new string[] { "Assets" });
                objectPaths = new string[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                    objectPaths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

                /* Find all network managers which use Single prefab linking
                 * as well all network object prefabs. */
                foreach (string item in objectPaths)
                {
                    //This will skip hidden unity types.
                    if (!item.EndsWith(".asset"))
                        continue;

                    DefaultPrefabObjects result = (DefaultPrefabObjects)AssetDatabase.LoadAssetAtPath(item, typeof(DefaultPrefabObjects));
                    if (result != null)
                    {
                        _defaultPrefabs = result;
                        break;
                    }

                }
            }

            justPopulated = false;
            //If not found then try to create file.
            if (_defaultPrefabs == null)
            {
                guids = AssetDatabase.FindAssets("t:asmdef", new string[] { "Assets" });
                objectPaths = new string[guids.Length];
                for (int i = 0; i < guids.Length; i++)
                    objectPaths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

                string fileName = "FishNet.Runtime.asmdef".ToLower();
                /* Find all network managers which use Single prefab linking
                 * as well all network object prefabs. */
                foreach (string item in objectPaths)
                {
                    //Found directory to create object in.
                    if (item.ToLower().Contains(fileName))
                    {
                        DefaultPrefabObjects dpo = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
                        //Get save directory.
                        string savePath = Path.GetDirectoryName(item);
                        AssetDatabase.CreateAsset(dpo, Path.Combine(savePath, $"{nameof(DefaultPrefabObjects)}.asset"));
                    }
                }

                //If still null.
                if (_defaultPrefabs == null)
                    Debug.LogWarning($"DefaultPrefabObjects not found. Prefabs list will not be automatically populated.");
            }
            else
            {
                justPopulated = PopulateDefaultPrefabs();
            }


            return _defaultPrefabs;
        }

        /// <summary>
        /// Initializes the default prefab.
        /// </summary>
        private static void InitializeOnce()
        {
            if (_initialized)
                return;
            _initialized = true;

            GetDefaultPrefabsFile(out _);

            if (_defaultPrefabs != null)
            {
                //Populate any missing.
                if (_defaultPrefabs.GetObjectCount() == 0)
                    PopulateDefaultPrefabs();
            }
        }


        /// <summary>
        /// Finds all NetworkObjects in project and adds them to defaultPrefabs.
        /// </summary>
        /// <returns>True if was populated from assets.</returns>
        internal static bool PopulateDefaultPrefabs()
        {
            if (_defaultPrefabs.GetObjectCount() > 0)
                return false;

            string[] guids;
            string[] objectPaths;

            guids = AssetDatabase.FindAssets("t:GameObject", null);
            objectPaths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                objectPaths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

            /* Find all network managers which use Single prefab linking
             * as well all network object prefabs. */
            foreach (string item in objectPaths)
            {
                GameObject go = (GameObject)AssetDatabase.LoadAssetAtPath(item, typeof(GameObject));
                if (go.TryGetComponent<NetworkObject>(out NetworkObject nob))
                    _defaultPrefabs.AddObject(nob);
            }

            //Only print if some were added.
            if (_defaultPrefabs.GetObjectCount() > 0)
                Debug.Log($"Default prefabs was populated with {_defaultPrefabs.GetObjectCount()} prefabs.");

            EditorUtility.SetDirty(_defaultPrefabs);
            return true;
        }

    }


}
#endif