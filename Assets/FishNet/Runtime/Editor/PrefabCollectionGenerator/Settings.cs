#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FishNet.Runtime.Editor.PrefabObjects.Generation
{
	internal sealed class Settings
	{
		private static string DirectoryPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library");

		private static string FilePath => Path.Combine(DirectoryPath, $"FishNet.Runtime.Editor.PrefabObjects.Generation.{nameof(Settings)}.json");

		public enum SearchScope
		{
			EntireProject,
			SpecificFolders,
		}

		public bool isEnabled;
		public bool enableLogging;

		public string assetPath;

		public SearchScope searchScope;

		public List<string> excludedFolders;
		public List<string> includedFolders;

		public Settings()
		{
			isEnabled = true;
			enableLogging = false;

			assetPath = $"Assets{Path.DirectorySeparatorChar}FishNet{Path.DirectorySeparatorChar}Runtime{Path.DirectorySeparatorChar}DefaultPrefabObjects.asset";

			searchScope = SearchScope.EntireProject;

			excludedFolders = new List<string>();
			includedFolders = new List<string>();
		}

		public void Save()
		{
			if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);

			File.WriteAllText(FilePath, JsonUtility.ToJson(this));
		}

		public static Settings Load()
		{
			try
			{
				if (File.Exists(FilePath)) return JsonUtility.FromJson<Settings>(File.ReadAllText(FilePath));
			}
			catch (Exception ex)
			{
				Debug.LogError($"An error has occurred when loading the prefab collection generator settings: {ex}");
			}

			return new Settings();
		}
	}
}

#endif
