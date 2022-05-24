﻿#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

using UnitySettingsProviderAttribute = UnityEditor.SettingsProviderAttribute;
using UnitySettingsProvider = UnityEditor.SettingsProvider;

namespace FishNet.Editing
{
	internal static class SettingsProvider
	{
		private static readonly Regex SlashRegex = new Regex(@"[\\//]");

		private static Settings _settings;

		private static GUIContent _folderIcon;
		private static GUIContent _deleteIcon;

		private static Vector2 _scrollVector;

		private static bool _showFolders;

		[UnitySettingsProvider]
		private static UnitySettingsProvider Create()
		{
			return new UnitySettingsProvider("Project/Fish-Networking/Prefab Objects Generator", SettingsScope.Project)
			{
				label = "Prefab Objects Generator",

				guiHandler = OnGUI,

				keywords = new string[]
				{
					"Fish",
					"Networking",
					"Prefab",
					"Objects",
					"Generator",
				},
			};
		}

		private static void OnGUI(string searchContext)
		{
			if (_settings == null) _settings = Settings.Load();

			if (_folderIcon == null) _folderIcon = EditorGUIUtility.IconContent("d_FolderOpened Icon");

			if (_deleteIcon == null) _deleteIcon = EditorGUIUtility.IconContent("P4_DeletedLocal");

			EditorGUI.BeginChangeCheck();

			GUIStyle scrollViewStyle = new GUIStyle()
			{
				padding = new RectOffset(10, 10, 10, 10),
			};

			_scrollVector = EditorGUILayout.BeginScrollView(_scrollVector, scrollViewStyle);

			_settings.isEnabled = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(_settings.isEnabled)), _settings.isEnabled);

			_settings.enableLogging = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(_settings.enableLogging)), _settings.enableLogging);

			GUILayoutOption iconWidthConstraint = GUILayout.MaxWidth(32.0f);

			GUILayoutOption iconHeightConstraint = GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight);

			EditorGUILayout.BeginHorizontal();

			string oldAssetPath = _settings.assetPath;

			string newAssetPath = EditorGUILayout.DelayedTextField(ObjectNames.NicifyVariableName(nameof(_settings.assetPath)), oldAssetPath);

			if (GUILayout.Button(_folderIcon, iconWidthConstraint, iconHeightConstraint))
			{
				if (TrySaveFilePathInsideAssetsFolder(null, Application.dataPath, "DefaultPrefabObjects", "asset", out string result))
				{
					newAssetPath = result;
				}
				else
				{
					EditorWindow.focusedWindow.ShowNotification(new GUIContent($"{ObjectNames.NicifyVariableName(nameof(_settings.assetPath))} must be inside the Assets folder."));
				}
			}

			if (!newAssetPath.Equals(oldAssetPath, StringComparison.OrdinalIgnoreCase))
			{
				if (newAssetPath.StartsWith($"Assets{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
				{
					if (File.Exists(newAssetPath))
					{
						EditorWindow.focusedWindow.ShowNotification(new GUIContent("Another asset already exists at the new path."));
					}
					else
					{
						Generator.AutoTrigger = false;

						if (File.Exists(oldAssetPath)) AssetDatabase.MoveAsset(oldAssetPath, newAssetPath);

						_settings.assetPath = newAssetPath;

						Generator.AutoTrigger = true;
					}
				}
				else
				{
					EditorWindow.focusedWindow.ShowNotification(new GUIContent($"{ObjectNames.NicifyVariableName(nameof(_settings.assetPath))} must be inside the Assets folder."));
				}
			}

			EditorGUILayout.EndHorizontal();

			_settings.searchScope = (Settings.SearchScope)EditorGUILayout.EnumPopup(ObjectNames.NicifyVariableName(nameof(_settings.searchScope)), _settings.searchScope);

			if (_settings.searchScope == Settings.SearchScope.EntireProject)
			{
				EditorGUILayout.HelpBox("Searching the entire project for prefabs can become very slow. Consider switching the search scope to specific folders instead.", MessageType.Warning);

				if (GUILayout.Button("Switch")) _settings.searchScope = Settings.SearchScope.SpecificFolders;
			}

			List<string> folders = null;

			string foldersName = null;

			if (_settings.searchScope == Settings.SearchScope.EntireProject)
			{
				folders = _settings.excludedFolders;

				foldersName = ObjectNames.NicifyVariableName(nameof(_settings.excludedFolders));
			}
			else if (_settings.searchScope == Settings.SearchScope.SpecificFolders)
			{
				folders = _settings.includedFolders;

				foldersName = ObjectNames.NicifyVariableName(nameof(_settings.includedFolders));
			}

			string folderName = foldersName.Substring(0, foldersName.Length - 1);

			if ((_showFolders = EditorGUILayout.Foldout(_showFolders, $"{foldersName} ({folders.Count})")) && folders != null)
			{
				EditorGUI.indentLevel++;

				for (int i = 0; i < folders.Count; i++)
				{
					EditorGUILayout.BeginHorizontal();

					string oldFolder = folders[i];

					string newFolder = SlashRegex.Replace(EditorGUILayout.DelayedTextField(oldFolder), Path.DirectorySeparatorChar.ToString());

					if (!newFolder.Equals(oldFolder, StringComparison.OrdinalIgnoreCase))
					{
						if (newFolder.StartsWith($"Assets{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
						{
							folders[i] = newFolder;
						}
						else
						{
							EditorWindow.focusedWindow.ShowNotification(new GUIContent($"{folderName} must be inside the Assets folder."));
						}
					}

					if (GUILayout.Button(_folderIcon, iconWidthConstraint, iconHeightConstraint))
					{
						if (TryOpenFolderPathInsideAssetsFolder(null, Application.dataPath, null, out string result))
						{
							folders[i] = result;
						}
						else
						{
							EditorWindow.focusedWindow.ShowNotification(new GUIContent($"{folderName} must be inside the Assets folder."));
						}
					}

					if (GUILayout.Button(_deleteIcon, iconWidthConstraint, iconHeightConstraint)) folders.RemoveAt(i);

					EditorGUILayout.EndHorizontal();
				}

				EditorGUI.indentLevel--;

				if (_settings.searchScope == Settings.SearchScope.SpecificFolders) EditorGUILayout.HelpBox("You can include subfolders by appending an asterisk (*) to a path.", MessageType.None);

				if (GUILayout.Button("Browse"))
				{
					if (TryOpenFolderPathInsideAssetsFolder(null, Application.dataPath, null, out string result))
					{
						folders.Add(result);
					}
					else
					{
						EditorWindow.focusedWindow.ShowNotification(new GUIContent($"{folderName} must be inside the Assets folder."));
					}
				}
			}

			if (EditorGUI.EndChangeCheck()) _settings.Save();

			if (GUILayout.Button("Generate")) Generator.Generate();

			EditorGUILayout.HelpBox("Consider pressing 'Generate' after changing the settings.", MessageType.Info);

			EditorGUILayout.EndScrollView();
		}

		private static bool TrySaveFilePathInsideAssetsFolder(string title, string directory, string name, string extension, out string result)
		{
			result = null;

			string selectedPath = EditorUtility.SaveFilePanel(title, directory, name, extension);

			if (selectedPath.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
			{
				result = SlashRegex.Replace(selectedPath.Remove(0, Path.GetDirectoryName(Application.dataPath).Length + 1), Path.DirectorySeparatorChar.ToString());

				return true;
			}

			return false;
		}

		private static bool TryOpenFolderPathInsideAssetsFolder(string title, string folder, string name, out string result)
		{
			result = null;

			string selectedPath = EditorUtility.OpenFolderPanel(title, folder, name);

			if (selectedPath.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
			{
				result = SlashRegex.Replace(selectedPath.Remove(0, Path.GetDirectoryName(Application.dataPath).Length + 1), Path.DirectorySeparatorChar.ToString());

				return true;
			}

			return false;
		}
	}
}

#endif