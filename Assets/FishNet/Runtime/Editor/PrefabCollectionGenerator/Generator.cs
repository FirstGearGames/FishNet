﻿#if UNITY_EDITOR

using FishNet.Managing.Object;
using FishNet.Object;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace FishNet.Editing.PrefabCollectionGenerator
{
    internal sealed class Generator : AssetPostprocessor
    {
        public Generator()
        {
            if (!_subscribed)
            {
                _subscribed = true;
                EditorApplication.update += OnEditorUpdate;
            }
        }
        ~Generator()
        {
            if (_subscribed)
            {
                _subscribed = false;
                EditorApplication.update -= OnEditorUpdate;
            }
        }

        #region Types.
        private struct SpecifiedFolder
        {
            public string Path;
            public bool Recursive;

            public SpecifiedFolder(string path, bool recursive)
            {
                Path = path;
                Recursive = recursive;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// True to ignore post process changes.
        /// </summary>
        public static bool IgnorePostProcess = false;
        #endregion

        #region Private.
        /// <summary>
        /// Last asset to import when there was only one imported asset and no other changes.
        /// </summary>
        private static string _lastSingleImportedAsset = string.Empty;
        /// <summary>
        /// Cached DefaultPrefabObjects reference.
        /// </summary>
        private static DefaultPrefabObjects _cachedDefaultPrefabs;
        /// <summary>
        /// True to refresh prefabs next update.
        /// </summary>
        private static bool _retryRefreshDefaultPrefabs;
        /// <summary>
        /// True if already subscribed to EditorApplication.Update.
        /// </summary>
        private static bool _subscribed;
        /// <summary>
        /// True if ran once since editor started.
        /// </summary>
        [System.NonSerialized]
        private static bool _ranOnce;
        /// <summary>
        /// Last paths of updated nobs during a changed update.
        /// </summary>
        [System.NonSerialized]
        private static List<string> _lastUpdatedNamePaths = new List<string>();
        /// <summary>
        /// Last frame changed was updated.
        /// </summary>
        [System.NonSerialized]
        private static int _lastUpdatedFrame = -1;
        /// <summary>
        /// Length of assets strings during the last update.
        /// </summary>
        [System.NonSerialized]
        private static int _lastUpdatedLengths = -1;
        #endregion

        public static string[] GetPrefabFiles(string startingPath, HashSet<string> excludedPaths, bool recursive)
        {
            //Opportunity to exit early if there are no excluded paths.
            if (excludedPaths.Count == 0)
            {
                string[] strResults = Directory.GetFiles(startingPath, "*.prefab", SearchOption.AllDirectories);
                return strResults;
            }
            //starting path is excluded.
            if (excludedPaths.Contains(startingPath))
                return new string[0];

            //Folders remaining to be iterated.
            List<string> enumeratedCollection = new List<string>() { startingPath };
            //Only check other directories if recursive.
            if (recursive)
            {
                //Find all folders which aren't excluded.
                for (int i = 0; i < enumeratedCollection.Count; i++)
                {
                    string[] allFolders = Directory.GetDirectories(enumeratedCollection[i], "*", SearchOption.TopDirectoryOnly);
                    for (int z = 0; z < allFolders.Length; z++)
                    {
                        string current = allFolders[z];
                        //Not excluded.
                        if (!excludedPaths.Contains(current))
                            enumeratedCollection.Add(current);
                    }
                }
            }

            //Valid prefab files.
            List<string> results = new List<string>();
            //Build files from folders.
            int count = enumeratedCollection.Count;
            for (int i = 0; i < count; i++)
            {
                string[] r = Directory.GetFiles(enumeratedCollection[i], "*.prefab", SearchOption.TopDirectoryOnly);
                results.AddRange(r);
            }

            return results.ToArray();
        }

        /// <summary>
        /// Removes paths which may overlap each other, such as sub directories.
        /// </summary>
        private static void RemoveOverlappingFolders(List<SpecifiedFolder> folders)
        {
            for (int z = 0; z < folders.Count; z++)
            {
                for (int i = 0; i < folders.Count; i++)
                {
                    //Do not check against self.
                    if (i == z)
                        continue;

                    //Duplicate.
                    if (folders[z].Path.Equals(folders[i].Path, System.StringComparison.OrdinalIgnoreCase))
                    {
                        UnityDebug.LogError($"The same path is specified multiple times in the DefaultPrefabGenerator settings. Remove the duplicate to clear this error.");
                        folders.RemoveAt(i);
                        break;
                    }

                    /* We are checking if i can be within
                     * z. This is only possible if i is longer
                     * than z. */
                    if (folders[i].Path.Length < folders[z].Path.Length)
                        continue;
                    /* Do not need to check if not recursive.
                     * Only recursive needs to be checked because
                     * a shorter recursive path could contain
                     * a longer path. */
                    if (!folders[z].Recursive)
                        continue;

                    //Compare paths.
                    string zPath = GetPathWithSeparator(folders[z].Path);
                    string iPath = zPath.Substring(0, zPath.Length);
                    //If paths match.
                    if (iPath.Equals(zPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        UnityDebug.LogError($"Path {folders[i].Path} is included within recursive path {folders[z].Path}. Remove path {folders[i].Path} to clear this error.");
                        folders.RemoveAt(i);
                        break;
                    }
                }
            }

            string GetPathWithSeparator(string txt)
            {
                return txt.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
            }
        }

        /// <summary>
        /// Returns a message to attach to logs if objects were dirtied.
        /// </summary>
        private static string GetDirtiedMessage(Settings settings, bool dirtied)
        {
            if (!settings.SaveChanges && dirtied)
                return " One or more NetworkObjects were dirtied. Please save your project.";
            else
                return string.Empty;
        }

        /// <summary>
        /// Updates prefabs by using only changed information.
        /// </summary>
        public static void GenerateChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, Settings settings = null)
        {
            if (settings == null)
                settings = Settings.Load();
            if (!settings.Enabled)
                return;

            bool log = settings.LogToConsole;
            Stopwatch sw = (log) ? Stopwatch.StartNew() : null;

            DefaultPrefabObjects prefabCollection = GetDefaultPrefabObjects(settings);
            //No need to error if nto found, GetDefaultPrefabObjects will.
            if (prefabCollection == null)
                return;

            int assetsLength = (importedAssets.Length + deletedAssets.Length + movedAssets.Length + movedFromAssetPaths.Length);
            List<string> changedNobPaths = new List<string>();

            System.Type goType = typeof(UnityEngine.GameObject);
            IterateAssetCollection(importedAssets);
            IterateAssetCollection(movedAssets);

            //True if dirtied by changes.
            bool dirtied;
            //First remove null entries.
            int startCount = prefabCollection.GetObjectCount();
            prefabCollection.RemoveNull();
            dirtied = (prefabCollection.GetObjectCount() != startCount);
            //First index which new objects will be added to.
            int firstAddIndex = (prefabCollection.GetObjectCount() - 1);

            //Iterates strings adding prefabs to collection.
            void IterateAssetCollection(string[] c)
            {
                foreach (string item in c)
                {
                    System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(item);
                    if (assetType != goType)
                        continue;

                    NetworkObject nob = AssetDatabase.LoadAssetAtPath<NetworkObject>(item);
                    if (nob != null)
                    {
                        changedNobPaths.Add(item);
                        prefabCollection.AddObject(nob, true);
                        dirtied = true;
                    }
                }
            }

            //To prevent out of range.
            if (firstAddIndex < 0 || firstAddIndex >= prefabCollection.GetObjectCount())
                firstAddIndex = 0;
            dirtied |= prefabCollection.SetAssetPathHashes(firstAddIndex);

            if (log && dirtied)
                UnityDebug.Log($"Default prefab generator updated prefabs in {sw.ElapsedMilliseconds}ms.{GetDirtiedMessage(settings, dirtied)}");

            //Check for redundancy.
            int frameCount = Time.frameCount;
            int changedCount = changedNobPaths.Count;
            if (frameCount == _lastUpdatedFrame && assetsLength == _lastUpdatedLengths && (changedCount == _lastUpdatedNamePaths.Count) && changedCount > 0)
            {
                bool allMatch = true;
                for (int i = 0; i < changedCount; i++)
                {
                    if (changedNobPaths[i] != _lastUpdatedNamePaths[i])
                    {
                        allMatch = false;
                        break;
                    }
                }

                /* If the import results are the same as the last attempt, on the same frame
                 * then there is likely an issue saving the assets. */
                if (allMatch)
                {
                    //Unset dirtied to prevent a save.
                    dirtied = false;
                    //Log this no matter what, it's critical.
                    UnityDebug.LogError($"Default prefab generator had a problem saving one or more assets. " +
                        $"This usually occurs when the assets cannot be saved due to missing scripts or serialization errors. " +
                        $"Please see above any prefabs which could not save any make corrections.");
                }

            }
            //Set last values.
            _lastUpdatedFrame = Time.frameCount;
            _lastUpdatedNamePaths = changedNobPaths;
            _lastUpdatedLengths = assetsLength;

            EditorUtility.SetDirty(prefabCollection);
            if (dirtied && settings.SaveChanges)
                AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Generates prefabs by iterating all files within settings parameters.
        /// </summary>
        public static void GenerateFull(Settings settings = null, bool forced = false)
        {
            if (settings == null)
                settings = Settings.Load();
            if (!forced && !settings.Enabled)
                return;
            bool log = settings.LogToConsole;

            Stopwatch sw = (log) ? Stopwatch.StartNew() : null;
            List<NetworkObject> foundNobs = new List<NetworkObject>();
            HashSet<string> excludedPaths = new HashSet<string>(settings.ExcludedFolders);

            //If searching the entire project.
            if (settings.SearchScope == Settings.SearchScopeType.EntireProject)
            {
                foreach (string path in GetPrefabFiles("Assets", excludedPaths, true))
                {
                    NetworkObject nob = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
                    if (nob != null)
                        foundNobs.Add(nob);
                }
            }
            //Specific folders.
            else if (settings.SearchScope == Settings.SearchScopeType.SpecificFolders)
            {
                List<SpecifiedFolder> folders = GetSpecifiedFolders(settings.IncludedFolders);
                RemoveOverlappingFolders(folders);

                foreach (SpecifiedFolder sf in folders)
                {
                    //If specified folder doesn't exist then continue.
                    if (!Directory.Exists(sf.Path))
                        continue;

                    foreach (string path in GetPrefabFiles(sf.Path, excludedPaths, sf.Recursive))
                    {
                        NetworkObject nob = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
                        if (nob != null)
                            foundNobs.Add(nob);
                    }
                }
            }
            //Unhandled.
            else
            {
                UnityDebug.LogError($"{settings.SearchScope} is not handled; default prefabs will not generator properly.");
            }

            DefaultPrefabObjects prefabCollection = GetDefaultPrefabObjects(settings);
            //No need to error if not found, GetDefaultPrefabObjects will throw.
            if (prefabCollection == null)
                return;

            //Clear and add built list.
            prefabCollection.Clear();
            prefabCollection.AddObjects(foundNobs, false);
            bool dirtied = prefabCollection.SetAssetPathHashes(0);

            if (log)
                UnityDebug.Log($"Default prefab generator found {prefabCollection.GetObjectCount()} prefabs in {sw.ElapsedMilliseconds}ms.{GetDirtiedMessage(settings, dirtied)}");

            EditorUtility.SetDirty(prefabCollection);
            if (settings.SaveChanges)
                AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Iterates folders building them into SpecifiedFolders.
        /// </summary>
        private static List<SpecifiedFolder> GetSpecifiedFolders(List<string> folders)
        {
            List<SpecifiedFolder> results = new List<SpecifiedFolder>();
            //Remove astericks.
            foreach (string path in folders)
            {
                int pLength = path.Length;
                if (pLength == 0)
                    continue;

                bool recursive;
                string p;
                //If the last character indicates resursive.
                if (path.Substring(pLength - 1, 1) == "*")
                {
                    p = path.Substring(0, pLength - 1);
                    recursive = true;
                }
                else
                {
                    p = path;
                    recursive = false;
                }

                results.Add(new SpecifiedFolder(p, recursive));
            }

            return results;
        }

        /// <summary>
        /// Returns the DefaultPrefabObjects file.
        /// </summary>
        private static DefaultPrefabObjects GetDefaultPrefabObjects(Settings settings = null)
        {
            if (settings == null)
                settings = Settings.Load();

            //Load the prefab collection
            string assetPath = settings.AssetPath;

            //If cached prefabs is not the same path as assetPath.
            if (_cachedDefaultPrefabs != null)
            {
                string foundPath = Path.GetFullPath(AssetDatabase.GetAssetPath(_cachedDefaultPrefabs));
                string assetPathFull = Path.GetFullPath(assetPath);
                if (foundPath != assetPathFull)
                    _cachedDefaultPrefabs = null;
            }

            //If cached is null try to get it.
            if (_cachedDefaultPrefabs == null)
            {
                //Only try to load it if file exist.
                if (File.Exists(assetPath))
                {
                    _cachedDefaultPrefabs = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(assetPath);
                    if (_cachedDefaultPrefabs == null)
                    {
                        //If already retried then throw an error.
                        if (_retryRefreshDefaultPrefabs)
                        {
                            UnityDebug.LogError("DefaultPrefabObjects file exists but it could not be loaded by Unity. Use the Fish-Networking menu to Refresh Default Prefabs.");
                        }
                        else
                        {
                            UnityDebug.Log("DefaultPrefabObjects file exists but it could not be loaded by Unity. Trying to reload the file next frame.");
                            _retryRefreshDefaultPrefabs = true;
                        }
                        return null;
                    }
                }
            }

            if (_cachedDefaultPrefabs == null)
            {
                UnityDebug.Log($"Creating a new DefaultPrefabsObject at {assetPath}.");
                string directory = Path.GetDirectoryName(assetPath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                _cachedDefaultPrefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
                AssetDatabase.CreateAsset(_cachedDefaultPrefabs, assetPath);
                AssetDatabase.SaveAssets();
            }

            if (_cachedDefaultPrefabs != null && _retryRefreshDefaultPrefabs)
                UnityDebug.Log("DefaultPrefabObjects found on the second iteration.");
            return _cachedDefaultPrefabs;
        }

        /// <summary>
        /// Called every frame the editor updates.
        /// </summary>
        private static void OnEditorUpdate()
        {
            if (!_retryRefreshDefaultPrefabs)
                return;

            GenerateFull();
            _retryRefreshDefaultPrefabs = false;
        }

        /// <summary>
        /// Called by Unity when assets are modified.
        /// </summary>
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Application.isPlaying)
                return;
            //If retrying next frame don't bother updating, next frame will do a full refresh.
            if (_retryRefreshDefaultPrefabs)
                return;
            //Post process is being ignored. Could be temporary or user has disabled this feature.
            if (IgnorePostProcess)
                return;
            /* Don't iterate if updating or compiling as that could cause an infinite loop
             * due to the prefabs being generated during an update, which causes the update
             * to start over, which causes the generator to run again, which... you get the idea. */
            if (EditorApplication.isCompiling)
                return;

            DefaultPrefabObjects prefabCollection = GetDefaultPrefabObjects();
            if (prefabCollection == null)
                return;
            Settings settings = Settings.Load();

            if (prefabCollection.GetObjectCount() == 0)
            {
                //If there are no prefabs then do a full rebuild. Odds of there being none are pretty much nill.
                GenerateFull(settings);
            }
            else
            {
                int totalChanges = importedAssets.Length + deletedAssets.Length + movedAssets.Length + movedFromAssetPaths.Length;
                //Nothing has changed. This shouldn't occur but unity is funny so we're going to check anyway.
                if (totalChanges == 0)
                    return;

                //normalizes path.
                string dpoPath = Path.GetFullPath(settings.AssetPath);
                //If total changes is 1 and the only changed file is the default prefab collection then do nothing.
                if (totalChanges == 1)
                {
                    //Do not need to check movedFromAssetPaths because that's not possible for this check.
                    if ((importedAssets.Length == 1 && Path.GetFullPath(importedAssets[0]) == dpoPath)
                        || (deletedAssets.Length == 1 && Path.GetFullPath(deletedAssets[0]) == dpoPath)
                        || (movedAssets.Length == 1 && Path.GetFullPath(movedAssets[0]) == dpoPath))
                        return;

                    /* If the only change is an import then check if the imported file
                     * is the same as the last, and if so check into returning early.
                     * For some reason occasionally when files are saved unity runs postprocess
                     * multiple times on the same file. */
                    string imported = (importedAssets.Length == 1) ? importedAssets[0] : null;
                    if (imported != null && imported == _lastSingleImportedAsset)
                    {
                        //If here then the file is the same. Make sure it's already in the collection before returning.
                        System.Type assetType = AssetDatabase.GetMainAssetTypeAtPath(imported);
                        //Not a gameObject, no reason to continue.
                        if (assetType != typeof(GameObject))
                            return;

                        NetworkObject nob = AssetDatabase.LoadAssetAtPath<NetworkObject>(imported);
                        //If is a networked object.
                        if (nob != null)
                        {
                            //Already added!
                            if (prefabCollection.Prefabs.Contains(nob))
                                return;
                        }
                    }
                    else if (imported != null)
                    {
                        _lastSingleImportedAsset = imported;
                    }
                }


                bool fullRebuild = settings.FullRebuild;
                /* If updating FN. This needs to be done a better way.
                 * Parsing the actual version file would be better. 
                 * I'll get to it next release. */
                if (!_ranOnce)
                {
                    _ranOnce = true;
                    fullRebuild = true;
                }
                else
                {
                    CheckForVersionFile(importedAssets);
                    CheckForVersionFile(deletedAssets);
                    CheckForVersionFile(movedAssets);
                    CheckForVersionFile(movedFromAssetPaths);
                }

                /* See if any of the changed files are the version file.
                * A new version file suggests an update. Granted, this could occur if
                * other assets imported a new version file as well but better
                * safe than sorry. */
                void CheckForVersionFile(string[] arr)
                {
                    string targetText = "VERSION.txt".ToLower();
                    int targetLength = targetText.Length;

                    for (int i = 0; i < arr.Length; i++)
                    {
                        string item = arr[i];
                        int itemLength = item.Length;
                        if (itemLength < targetLength)
                            continue;

                        item = item.ToLower();
                        int startIndex = (itemLength - targetLength);
                        if (item.Substring(startIndex, targetLength) == targetText)
                        {
                            fullRebuild = true;
                            return;
                        }
                    }
                }

                if (fullRebuild)
                    GenerateFull(settings);
                else
                    GenerateChanged(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths, settings);
            }
        }
    }
}

#endif
