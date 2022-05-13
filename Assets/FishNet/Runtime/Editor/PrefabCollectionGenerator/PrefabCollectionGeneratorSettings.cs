#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FishNet.Runtime.Editor
{
	internal sealed class PrefabCollectionGeneratorSettings
	{
		private static string DirectoryPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library");

		private static string FilePath => Path.Combine(DirectoryPath, $"FishNet.Editor.{nameof(PrefabCollectionGeneratorSettings)}.json");

		public enum SearchScope
		{
			EntireProject,
			SpecificFolders,
		}

		public string assetPath;

		public SearchScope searchScope;

		public List<string> excludedFolders;
		public List<string> includedFolders;

		public PrefabCollectionGeneratorSettings()
		{
			assetPath = $"Assets{Path.DirectorySeparatorChar}FishNet{Path.DirectorySeparatorChar}DefaultPrefabObjects.asset";

			searchScope = SearchScope.EntireProject;

			excludedFolders = new List<string>();
			includedFolders = new List<string>();
		}

		public void Save()
		{
			if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);

			File.WriteAllText(FilePath, JsonUtility.ToJson(this));
		}

		public static PrefabCollectionGeneratorSettings Load()
		{
			try
			{
				if (File.Exists(FilePath)) return JsonUtility.FromJson<PrefabCollectionGeneratorSettings>(File.ReadAllText(FilePath));
			}
			catch (Exception ex)
			{
				Debug.LogError($"An error has occurred when loading the prefab collection generator settings: {ex}");
			}

			return new PrefabCollectionGeneratorSettings();
		}
	}
}

#endif
