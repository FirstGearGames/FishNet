#if UNITY_EDITOR
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace FishNet.Editing
{
    internal class PrefabProcessor : AssetPostprocessor
    {
        #region Private.   
        /// <summary>
        /// ScriptableObject to store default prefabs.
        /// </summary>
        private static DefaultPrefabObjects _defaultPrefabs;
        #endregion

        /// <summary>
        /// Called after assets are created or imported.
        /// </summary>
        /// <param name="importedAssets"></param>
        /// <param name="deletedAssets"></param>
        /// <param name="movedAssets"></param>
        /// <param name="movedFromAssetPaths"></param>
#if UNITY_2021_3_OR_NEWER
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
#else
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
#endif
        {

#if UNITY_2021_3_OR_NEWER
            if (didDomainReload)
                return;
#endif
            bool justPopulated;
            if (_defaultPrefabs == null)
                _defaultPrefabs = DefaultPrefabsFinder.GetDefaultPrefabsFile(out justPopulated);
            else
                justPopulated = DefaultPrefabsFinder.PopulateDefaultPrefabs();
            //Not found.
            if (_defaultPrefabs == null)
                return;

            //True if null must be removed as well.
            bool removeNull = (deletedAssets.Length > 0 || movedAssets.Length > 0 || movedFromAssetPaths.Length > 0);
            if (removeNull)
                _defaultPrefabs.RemoveNull();

            /* Only need to add new prefabs if not justPopulated.
            * justPopulated would have already picked up the new prefabs. */
            if (justPopulated)
                return;

            System.Type goType = typeof(UnityEngine.GameObject);
            foreach (string item in importedAssets)
            {
                System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(item);
                if (assetType != goType)
                    continue;

                GameObject go = (GameObject)AssetDatabase.LoadAssetAtPath(item, typeof(GameObject));
                //If is a gameobject.
                if (go != null)
                {

                    NetworkObject nob;
                    //Not a network object.
                    if (!go.TryGetComponent<NetworkObject>(out nob))
                        continue;

                    /* Check for duplicates because adding a component to a prefab will also call this function
                     * which will result in this function calling multiple times for the same object. */
                    _defaultPrefabs.AddObject(nob, true);
                }
            }

            EditorUtility.SetDirty(_defaultPrefabs);
        }

    }

}

#endif