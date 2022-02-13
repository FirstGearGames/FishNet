#if UNITY_EDITOR
using FishNet.Editing;
using System;
using System.IO;
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
            return (a.StripReleaseBuilds != b.StripReleaseBuilds);
        }
        /// <summary>
        /// Copies all values from source to target.
        /// </summary>
        public static void CopyTo(this ConfigurationEditor.ConfigurationData source, ConfigurationEditor.ConfigurationData target)
        {
            target.StripReleaseBuilds = source.StripReleaseBuilds;
        }
    }

    [InitializeOnLoad]
    internal class ConfigurationEditor : EditorWindow
    {
        #region Types.
        public class ConfigurationData
        {
            public bool StripReleaseBuilds = false;
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
        private const string CONFIG_FILE_NAME = "Config.json";
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
            string runtimePath = Finding.GetFishNetRuntimePath(error);
            if (runtimePath != string.Empty)
            {
                _configurationFilePath = Path.Combine(runtimePath, CONFIG_FILE_NAME);
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

        private void OnGUI()
        {
            if (_reloadFile)
                LoadConfiguration();

            if (Configuration == null)
                return;
            Configuration.CopyTo(_comparerConfiguration);

            GUILayout.BeginVertical();
            GUILayout.BeginScrollView(scrollPos, GUILayout.Width(800), GUILayout.Height(800));

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.Box(EditingConstants.PRO_ASSETS_LOCKED_TEXT, GUILayout.Width(200f));
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);

            Configuration.StripReleaseBuilds = EditorGUILayout.ToggleLeft("* Strip Release Builds", Configuration.StripReleaseBuilds);


            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            if (Configuration.HasChanged(_comparerConfiguration))
                SaveConfiguration();
        }

        Vector2 scrollPos;

        //scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(800), GUILayout.Height(800))
    }
}
#endif