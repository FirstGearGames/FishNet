#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FishNet.Editing.PrefabCollectionGenerator
{
	internal sealed class Settings
	{
		#region Types.
		public enum SearchScopeType : byte
		{
			EntireProject = 0,
			SpecificFolders = 1
		}
		#endregion

		#region Public.
		/// <summary>
		/// True if prefab generation is enabled.
		/// </summary>
		public bool Enabled;
		/// <summary>
		/// True to rebuild all prefabs during any change. False to only check changed prefabs.
		/// </summary>
		public bool FullRebuild;
		/// <summary>
		/// True to log results to console.
		/// </summary>
		public bool LogToConsole;
		/// <summary>
		/// True to automatically save assets when default prefabs change.
		/// </summary>
		public bool SaveChanges;
		/// <summary>
		/// Path where prefabs file is created.
		/// </summary>
		public string AssetPath;
		/// <summary>
		/// How to search for files.
		/// </summary>
		public SearchScopeType SearchScope = SearchScopeType.EntireProject;
		/// <summary>
		/// Folders to exclude when using SearchScopeType.SpecificFolders.
		/// </summary>
		public List<string> ExcludedFolders = new List<string>();
		/// <summary>
		/// Folders to include when using SearchScopeType.SpecificFolders.
		/// </summary>
		public List<string> IncludedFolders = new List<string>();
		#endregion

		#region Private.
		/// <summary>
		/// Library folder for project. Presumably where files are saved, but this is changing. This is going away in favor of FN config. //fnconfig.
		/// </summary>
		private static string DirectoryPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library");
		/// <summary>
		/// Full path of settings file. This is going away in favor of FN config. //fnconfig.
		/// </summary>
		private static string FilePath => Path.Combine(DirectoryPath, $"FishNet.Runtime.Editor.PrefabObjects.Generation.{nameof(Settings)}.json");
        #endregion

        public Settings()
		{
			Enabled = true;
			LogToConsole = true;
			FullRebuild = false;
			SaveChanges = true;
			SearchScope = SearchScopeType.EntireProject;

			AssetPath = $"Assets{Path.DirectorySeparatorChar}FishNet{Path.DirectorySeparatorChar}Runtime{Path.DirectorySeparatorChar}DefaultPrefabObjects.asset";
		}

		public void Save()
		{
			//Create save folder if it doesn't exist. This is going away in favor of FN config. //fnconfig.
			if (!Directory.Exists(DirectoryPath))
				Directory.CreateDirectory(DirectoryPath);

			File.WriteAllText(FilePath, JsonUtility.ToJson(this));
		}

		public static Settings Load()
		{
			try
			{
				if (File.Exists(FilePath))
					return JsonUtility.FromJson<Settings>(File.ReadAllText(FilePath));
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