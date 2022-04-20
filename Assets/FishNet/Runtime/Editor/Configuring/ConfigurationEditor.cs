#if UNITY_EDITOR
using FishNet.Editing;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using UnityEditor;
using UnityEngine;

namespace FishNet.Configuring.Editing
{

    [InitializeOnLoad]
    internal class ConfigurationEditor : EditorWindow
    {

        #region Private.
        /// <summary>
        /// Used to compare if ConfigurationData has changed.
        /// </summary>
        private static ConfigurationData _comparerConfiguration = new ConfigurationData();
        /// <summary>
        /// True to reload the configuration file.
        /// </summary>
        //[System.NonSerialized]
        //private static bool _reloadFile = true;
        #endregion

        /// <summary>
        /// Saves ConfigurationData to disk.
        /// </summary>
        private void SaveConfiguration()
        {
            string path = CodeStripping.GetAssetsPath(CodeStripping.CONFIG_FILE_NAME);
            CodeStripping.ConfigurationData.Write(path, true);
        }


        [MenuItem("Fish-Networking/Configuration", false, 0)]
        public static void ShowConfiguration()
        {
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
            //if (_reloadFile)
            //    Configuration.LoadConfiguration();

            ConfigurationData data = CodeStripping.GetConfigurationData();

            if (data == null)
                return;
            data.CopyTo(_comparerConfiguration);

            GUILayout.BeginVertical();
            GUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(500), GUILayout.Height(800));

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.Box(EditingConstants.PRO_ASSETS_LOCKED_TEXT, GUILayout.Width(200f));
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            data.StripReleaseBuilds = EditorGUILayout.ToggleLeft("* Strip Release Builds", data.StripReleaseBuilds);
            GUILayout.EndHorizontal();

            if (data.StripReleaseBuilds)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(40f);
                GUILayout.Box("NOTICE: development builds will not have code stripped. Additionally, if you plan to run as host disable code stripping.", GUILayout.Width(170f));
                GUILayout.EndHorizontal();
            }


            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            if (data.HasChanged(_comparerConfiguration))
                SaveConfiguration();
        }

    }
}
#endif
