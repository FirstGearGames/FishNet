#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FishNet.Object;
using FishNet.Managing.Object;
using System.IO;
using System.Collections.Generic;
using System;

namespace FishNet.Editing
{

    [InitializeOnLoad]
    internal static class DefaultPrefabsFinder
    {
        /// <summary>
        /// True if initialized.
        /// </summary>
        [System.NonSerialized]
        private static bool _initialized = false;
        /// <summary>
        /// Found default prefabs.
        /// </summary>
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
            if (_defaultPrefabs == null)
            {
                List<UnityEngine.Object> results = Finding.GetScriptableObjects<DefaultPrefabObjects>(true, true);
                if (results.Count > 0)
                    _defaultPrefabs = (DefaultPrefabObjects)results[0];
            }

            justPopulated = false;
            //If not found then try to create file.
            if (_defaultPrefabs == null)
            {
                DefaultPrefabObjects dpo = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
                //Get save directory.
                string savePath = Finding.GetFishNetRuntimePath(true);
                AssetDatabase.CreateAsset(dpo, Path.Combine(savePath, $"{nameof(DefaultPrefabObjects)}.asset"));
            }

            //If still null.
            if (_defaultPrefabs == null)
                Debug.LogWarning($"DefaultPrefabObjects not found. Prefabs list will not be automatically populated.");
            else
                justPopulated = PopulateDefaultPrefabs();

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

            Finding.GetFishNetRuntimePath(false);
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

            List<GameObject> gameObjects = Finding.GetGameObjects(true, true, true);
            foreach (GameObject go in gameObjects)
            {
                if (go.TryGetComponent(out NetworkObject nob))
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