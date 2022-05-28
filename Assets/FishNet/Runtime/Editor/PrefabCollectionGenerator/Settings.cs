#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FishNet.Editing
{
	internal sealed class Settings
	{
		private static string DirectoryPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library");

		private static string FilePath => Path.Combine(DirectoryPath, $"FishNet.Runtime.Editor.PrefabObjects.Generation.{nameof(Settings)}.json");

		public enum SearchScopeType
		{
			EntireProject,
			SpecificFolders,
		}

		public bool Enabled;
		public bool LogToConsole;
		public bool SortCollection;

		public string AssetPath;

		public SearchScopeType SearchScope;

		public List<string> ExcludedFolders;
		public List<string> IncludedFolders;

		public Settings()
		{
			Enabled = true;
			LogToConsole = true;
			SortCollection = false;

			AssetPath = $"Assets{Path.DirectorySeparatorChar}FishNet{Path.DirectorySeparatorChar}Runtime{Path.DirectorySeparatorChar}DefaultPrefabObjects.asset";

			SearchScope = SearchScopeType.EntireProject;

			ExcludedFolders = new List<string>();
			IncludedFolders = new List<string>();
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
