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
		public static bool AutoTrigger = true;

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

			if (!settings.isEnabled) return;

			Stopwatch stopwatch = settings.enableLogging ? Stopwatch.StartNew() : null;

			List<NetworkObject> networkObjectPrefabs = new List<NetworkObject>();

			if (settings.searchScope == Settings.SearchScope.EntireProject)
			{
				foreach (string directory in EnumerateDirectoriesRecursively("Assets", new HashSet<string>(settings.excludedFolders)))
				{
					foreach (string file in Directory.EnumerateFiles(directory, "*.prefab"))
					{
						NetworkObject networkObjectPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(file);

						if (networkObjectPrefab != null) networkObjectPrefabs.Add(networkObjectPrefab);
					}
				}
			}
			else if (settings.searchScope == Settings.SearchScope.SpecificFolders)
			{
				foreach (string folder in settings.includedFolders.Distinct())
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

			DefaultPrefabObjects prefabCollection = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(settings.assetPath);

			if (prefabCollection == null)
			{
				string directory = Path.GetDirectoryName(settings.assetPath);

				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);

					AssetDatabase.Refresh();
				}

				prefabCollection = ScriptableObject.CreateInstance<DefaultPrefabObjects>();

				AssetDatabase.CreateAsset(prefabCollection, settings.assetPath);

				AssetDatabase.SaveAssets();
			}

			prefabCollection.Clear();

			prefabCollection.AddObjects(networkObjectPrefabs);

			EditorUtility.SetDirty(prefabCollection);

			if (settings.enableLogging)
			{
				stopwatch.Stop();

				UnityEngine.Debug.Log($"NetworkObject prefab collection '{Path.GetFileNameWithoutExtension(settings.assetPath)}' generation took {stopwatch.ElapsedMilliseconds} milliseconds to complete. {networkObjectPrefabs.Count} NetworkObject prefabs were found.");
			}
		}

		private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			if (!AutoTrigger) return;

			Settings settings = Settings.Load();

			if ((importedAssets.Length == 1 && importedAssets[0] == settings.assetPath)
				|| (deletedAssets.Length == 1 && deletedAssets[0] == settings.assetPath)
				|| (movedAssets.Length == 1 && movedAssets[0] == settings.assetPath)) return;

			Generate(settings);
		}		
	}
}

#endif
