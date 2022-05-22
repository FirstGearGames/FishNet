﻿#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using FishNet.Configuring;

using UnitySettingsProviderAttribute = UnityEditor.SettingsProviderAttribute;
using UnitySettingsProvider = UnityEditor.SettingsProvider;

namespace FishNet.Runtime.Editor.Configuration
{
	internal static class SettingsProvider
	{
		private static Vector2 _scrollView;

		[UnitySettingsProvider]
		private static UnitySettingsProvider Create()
		{
			return new UnitySettingsProvider("Project/Fish-Networking/Configuration", SettingsScope.Project)
			{
				label = "Configuration",

				guiHandler = OnGUI,

				keywords = new string[]
				{
					"Fish",
					"Networking",
					"Configuration",
				},
			};
		}

		private static void OnGUI(string searchContext)
		{
			ConfigurationData configuration = CodeStripping.GetConfigurationData();

			if (configuration == null)
			{
				EditorGUILayout.HelpBox("Unable to load configuration data.", MessageType.Error);

				return;
			}

			EditorGUI.BeginChangeCheck();

			GUIStyle scrollViewStyle = new GUIStyle()
			{
				padding = new RectOffset(10, 10, 10, 10),
			};

			_scrollView = GUILayout.BeginScrollView(_scrollView, scrollViewStyle);

			EditorGUILayout.BeginHorizontal();

			GUIStyle toggleStyle = new GUIStyle(EditorStyles.toggle)
			{
				richText = true,
			};

			configuration.StripReleaseBuilds = GUILayout.Toggle(configuration.StripReleaseBuilds, $"{ObjectNames.NicifyVariableName(nameof(configuration.StripReleaseBuilds))} <color=yellow>(Pro Only)</color>", toggleStyle);

			EditorGUILayout.EndHorizontal();

			if (configuration.StripReleaseBuilds) EditorGUILayout.HelpBox("Development builds will not have code stripped. Additionally, if you plan to run as host disable code stripping.", MessageType.Warning);

			GUILayout.EndScrollView();

			if (EditorGUI.EndChangeCheck()) CodeStripping.ConfigurationData.Write(CodeStripping.GetAssetsPath(CodeStripping.CONFIG_FILE_NAME), true);
		}
	}
}

#endif
