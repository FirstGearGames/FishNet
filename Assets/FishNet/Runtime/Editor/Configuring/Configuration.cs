#if UNITY_EDITOR
using FishNet.Editing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;

namespace FishNet.Configuring.Editing
{
    internal static class ConfigurationExtension
    {
        /// <summary>
        /// Returns if a differs from b.
        /// </summary>
        public static bool HasChanged(this ConfigurationEditor.ConfigurationData a, ConfigurationEditor.ConfigurationData b)
        {
            return (a.StripReleaseBuilds != b.StripReleaseBuilds || a.SpawnCompression != b.SpawnCompression || a.SyncCompression != b.SyncCompression);
        }
        /// <summary>
        /// Copies all values from source to target.
        /// </summary>
        public static void CopyTo(this ConfigurationEditor.ConfigurationData source, ConfigurationEditor.ConfigurationData target)
        {
            target.StripReleaseBuilds = source.StripReleaseBuilds;
            target.SpawnCompression = source.SpawnCompression;
            target.SyncCompression = source.SyncCompression;
        }
    }

    [InitializeOnLoad]
    internal class ConfigurationEditor : EditorWindow
    {
        public enum QuaternionCompression
        {
            Compress32,
            Compress64,
            Uncompressed
        }

        #region Types.
        public class ConfigurationData
        {
            public bool StripReleaseBuilds = false;

            public QuaternionCompression SpawnCompression = QuaternionCompression.Compress32;

            public QuaternionCompression SyncCompression = QuaternionCompression.Compress32;

        }
        #endregion

        #region Public.
        /// <summary>
        /// Current configuration data.
        /// </summary>
        internal static ConfigurationData Configuration = new ConfigurationData();
        #endregion

        #region Private.
        /// <summary>
        /// File name for configuration disk data.
        /// </summary>
        private const string CONFIG_FILE_NAME = "FishNet.Config.json";
        /// <summary>
        /// Used to compare if ConfigurationData has changed.
        /// </summary>
        private static ConfigurationData _comparerConfiguration = new ConfigurationData();
        /// <summary>
        /// True if initialized.
        /// </summary>
        [System.NonSerialized]
        private static bool _initialized;
        /// <summary>
        /// True to reload the configuration file.
        /// </summary>
        [System.NonSerialized]
        private static bool _reloadFile = true;
        /// <summary>
        /// Save and load path for configuration disk data.
        /// </summary>
        private static string _configurationFilePath;
        #endregion

        static ConfigurationEditor()
        {
            EditorApplication.update += InitializeOnce;
        }
        ~ConfigurationEditor()
        {
            EditorApplication.update -= InitializeOnce;
        }

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        private static void InitializeOnce()
        {
            if (_initialized)
                return;
            _initialized = true;

            SetConfigurationPath(false);
        }

        /// <summary>
        /// Sets ConfigurationFilePath.
        /// </summary>
        private static bool SetConfigurationPath(bool error)
        {
            string appPath = Application.dataPath;
            if (appPath != string.Empty)
            {
                _configurationFilePath = Path.Combine(appPath, CONFIG_FILE_NAME);
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Loads ConfigurationData from disk.
        /// </summary>

        private static void LoadConfiguration()
        {
            if (!SetConfigurationPath(true))
                return;

            try
            {
                //File is on disk.
                if (File.Exists(_configurationFilePath))
                {
                    string json = File.ReadAllText(_configurationFilePath);
                    Configuration = JsonUtility.FromJson<ConfigurationData>(json);
                }
                //Not on disk, make new instance of data.
                else
                {
                    Configuration = new ConfigurationData();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"There was a problem loading Fish-Networking configuration. Message: {ex.Message}.");
            }

            SetDefineSymbols(Configuration);
        }

        /// <summary>
        /// Saves ConfigurationData to disk.
        /// </summary>
        private void SaveConfiguration()
        {
            if (!SetConfigurationPath(true))
                return;

            //Not yet set.
            if (Configuration == null)
                Configuration = new ConfigurationData();

            string json = JsonUtility.ToJson(Configuration);
            try
            {
                File.WriteAllText(_configurationFilePath, json);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"There was a problem saving Fish-Networking configuration. Message: {ex.Message}.");
            }
        }


        [MenuItem("Fish-Networking/Configuration", false, 0)]
        public static void ShowConfiguration()
        {
            LoadConfiguration();
            EditorWindow window = GetWindow<ConfigurationEditor>();
            window.titleContent = new GUIContent("Fish-Networking Configuration");
            //Dont worry about capping size until it becomes a problem.
            //const int width = 200;
            //const int height = 100;
            //float x = (Screen.currentResolution.width - width);
            //float y = (Screen.currentResolution.height - height);
            //window.minSize = new Vector2(width, height);
            //window.maxSize = new Vector2(x, y);
        }

        private static readonly string[] FN_CONFIG_SYMBOLS = new string[]
        {
            "FN_QUAT_SPAWN_32", "FN_QUAT_SPAWN_64", "FN_QUAT_SYNC_32", "FN_QUAT_SYNC_64"
        };

        static void SetDefineSymbols(ConfigurationData data)
        {
            List<string> symbols = new List<string>();

            string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            List<string> allDefines = definesString.Split(';').ToList();

            allDefines = allDefines.Except(FN_CONFIG_SYMBOLS).ToList();


            if (data.SpawnCompression == QuaternionCompression.Compress32)
                allDefines.Add("FN_QUAT_SPAWN_32");
            if (data.SpawnCompression == QuaternionCompression.Compress64)
                allDefines.Add("FN_QUAT_SPAWN_64");
            
            if (data.SyncCompression == QuaternionCompression.Compress32)
                allDefines.Add("FN_QUAT_SYNC_32");
            if (data.SyncCompression == QuaternionCompression.Compress64)
                allDefines.Add("FN_QUAT_SYNC_64");
            
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup,
                string.Join(";", allDefines.ToArray()));
        }

        private void OnGUI()
        {
            if (_reloadFile)
                LoadConfiguration();

            if (Configuration == null)
                return;
            Configuration.CopyTo(_comparerConfiguration);

            GUILayout.BeginVertical();
            GUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(700), GUILayout.Height(800));

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.Box(EditingConstants.PRO_ASSETS_LOCKED_TEXT, GUILayout.Width(200f));
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            GUILayout.BeginVertical();
            
            Configuration.SpawnCompression = (QuaternionCompression)EditorGUILayout.EnumPopup("Quat. spawn compression", Configuration.SpawnCompression, GUILayout.Width(290));
            Configuration.SyncCompression = (QuaternionCompression)EditorGUILayout.EnumPopup("Quat. sync compression", Configuration.SyncCompression, GUILayout.Width(290));

            GUILayout.EndVertical();
            GUILayout.Space(5f);
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            Configuration.StripReleaseBuilds = EditorGUILayout.ToggleLeft("* Strip Release Builds", Configuration.StripReleaseBuilds);
            GUILayout.EndHorizontal();

            if (Configuration.StripReleaseBuilds)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                GUILayout.Box("NOTICE: development builds will not have code stripped. Additionally, if you plan to run as host disable code stripping.", GUILayout.Width(170f));
                GUILayout.EndHorizontal();
            }


            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            if (Configuration.HasChanged(_comparerConfiguration))
                SaveConfiguration();
        }

    }
}
#endif