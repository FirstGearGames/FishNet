//using System; //Remove on 2022/06/01
//using System.IO;
//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEditor.Compilation;
//#endif
//using UnityEngine;

//namespace FishNet.Configuring
//{
//#if UNITY_EDITOR
//    [InitializeOnLoad]
//#endif
//    internal class Configuration
//    {

//        #region Public.
//        /// <summary>
//        /// Save and load path for configuration disk data.
//        /// </summary>       
//        internal static string ConfigurationFilePath;
//        /// <summary> 
//        /// Current configuration data.
//        /// </summary>
//        internal static ConfigurationData ConfigurationData { get; private set; }
//        /// <summary>
//        /// True if the editor is currently compiling assemblies.
//        /// </summary>
//        private static bool _isCompiling;
//        #endregion

//        #region Const.
//        /// <summary>
//        /// File name for configuration disk data.
//        /// </summary>
//        private const string CONFIG_FILE_NAME = "FishNet.Config.json";
//        #endregion

//        static Configuration()
//        {
//#if UNITY_EDITOR
//            CompilationPipeline.compilationStarted += CompilationPipeline_compilationStarted;
//            CompilationPipeline.compilationFinished += CompilationPipeline_compilationFinished;
//#endif
//        }
//        ~Configuration()
//        {
//#if UNITY_EDITOR
//            CompilationPipeline.compilationStarted -= CompilationPipeline_compilationStarted;
//            CompilationPipeline.compilationFinished -= CompilationPipeline_compilationFinished;
//#endif
//        }


//        private static void CompilationPipeline_compilationFinished(object obj)
//        {
//            _isCompiling = false;
//        }

//        private static void CompilationPipeline_compilationStarted(object obj)
//        {
//            LoadConfiguration();
//            _isCompiling = true;
//        }

//        /// <summary>
//        /// Sets ConfigurationFilePath.
//        /// </summary>
//        private static bool SetConfigurationPath(bool error)
//        {
//            if (_isCompiling)
//                return false;

//            try
//            {
//                string appPath =Application.dataPath;
//                //Set configuration file path.
//                if (appPath != string.Empty)
//                {
//                    ConfigurationFilePath = Path.Combine(appPath, CONFIG_FILE_NAME);
//                    return true;
//                }
//                //App path not found.
//                else
//                {
//                    Debug.LogError($"Application dataPath could not be found. Fish-Networking configuration will not function.");
//                    return false;
//                }
//            }
//            catch (Exception ex)
//            {
//                Debug.LogError("An error occurred while setting configuration path. " + ex.Message);
//                return false;
//            }
            
//        }
//        /// <summary>
//        /// Loads ConfigurationData from disk.
//        /// </summary>
//        internal static void LoadConfiguration()
//        {
//            if (!SetConfigurationPath(true))
//                return;

//            try
//            {
//                //File is on disk.
//                if (File.Exists(ConfigurationFilePath))
//                {
//                    string json = File.ReadAllText(ConfigurationFilePath);
//                    ConfigurationData = JsonUtility.FromJson<ConfigurationData>(json);
//                }
//                else
//                {
//                    ConfigurationData = new ConfigurationData();
//                }
//            }
//            catch (Exception ex)
//            {
//                Debug.LogError($"There was a problem loading Fish-Networking configuration. Message: {ex.Message}.");
//            }
//        }

//    }
//}
