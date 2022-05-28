#if UNITY_EDITOR

using FishNet.Managing.Object;
using FishNet.Object;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FishNet.Editing
{
    internal sealed class Generator : AssetPostprocessor
    {
        public static bool IgnorePostProcess = false;
        public static bool SortCollection = false;

        private static IEnumerable<string> EnumerateDirectoriesRecursively(string directory, HashSet<string> excludedDirectories)
        {
            if (excludedDirectories.Contains(directory)) yield break;

            yield return directory;

            foreach (string level1NestedDirectory in Directory.EnumerateDirectories(directory))
            {
                if (excludedDirectories.Contains(level1NestedDirectory)) continue;

                foreach (string level2NestedDirectory in EnumerateDirectoriesRecursively(level1NestedDirectory, excludedDirectories))
                {
                    yield return level2NestedDirectory;
                }
            }
        }

        public static void Generate(Settings settings = null)
        {
            settings = settings ?? Settings.Load();

            if (!settings.Enabled) return;

            Stopwatch stopwatch = settings.LogToConsole ? Stopwatch.StartNew() : null;

            List<NetworkObject> networkObjectPrefabs = new List<NetworkObject>();

            if (settings.SearchScope == Settings.SearchScopeType.EntireProject)
            {
                foreach (string directory in EnumerateDirectoriesRecursively("Assets", new HashSet<string>(settings.ExcludedFolders)))
                {
                    foreach (string file in Directory.EnumerateFiles(directory, "*.prefab"))
                    {
                        NetworkObject networkObjectPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(file);

                        if (networkObjectPrefab != null) networkObjectPrefabs.Add(networkObjectPrefab);
                    }
                }
            }
            else if (settings.SearchScope == Settings.SearchScopeType.SpecificFolders)
            {
                foreach (string folder in settings.IncludedFolders.Distinct())
                {
                    bool includeSubfolders = folder[folder.Length - 1] == '*';

                    if (!Directory.Exists(includeSubfolders ? folder.Remove(folder.Length - 1) : folder)) continue;

                    foreach (string file in Directory.EnumerateFiles(includeSubfolders ? folder.Remove(folder.Length - 1) : folder, "*.prefab", includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    {
                        NetworkObject networkObjectPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(file);

                        if (networkObjectPrefab != null) networkObjectPrefabs.Add(networkObjectPrefab);
                    }
                }
            }

            DefaultPrefabObjects prefabCollection = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(settings.AssetPath);

            if (prefabCollection == null)
            {
                string directory = Path.GetDirectoryName(settings.AssetPath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);

                    AssetDatabase.Refresh();
                }

                prefabCollection = ScriptableObject.CreateInstance<DefaultPrefabObjects>();

                AssetDatabase.CreateAsset(prefabCollection, settings.AssetPath);

                AssetDatabase.SaveAssets();
            }

            prefabCollection.Clear();

            prefabCollection.AddObjects(networkObjectPrefabs);

            if (SortCollection)
                prefabCollection.Sort();

            EditorUtility.SetDirty(prefabCollection);

            if (settings.LogToConsole)
            {
                stopwatch.Stop();

                UnityEngine.Debug.Log($"NetworkObject prefab collection '{Path.GetFileNameWithoutExtension(settings.AssetPath)}' generation took {stopwatch.ElapsedMilliseconds} milliseconds to complete. {networkObjectPrefabs.Count} NetworkObject prefabs were found.");
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (IgnorePostProcess) return;
            if (EditorApplication.isUpdating || EditorApplication.isCompiling) return;

            Settings settings = Settings.Load();

            if ((importedAssets.Length == 1 && importedAssets[0] == settings.AssetPath)
                || (deletedAssets.Length == 1 && deletedAssets[0] == settings.AssetPath)
                || (movedAssets.Length == 1 && movedAssets[0] == settings.AssetPath)) return;

            Generate(settings);
        }
    }
}

#endif
