using System.IO;
using System.Xml.Serialization;

#if UNITY_EDITOR
using UnityEditor.Compilation;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEditor.Build;
#endif

namespace FishNet.Configuring
{


    public class Configuration
    {

        /// <summary>
        /// 
        /// </summary>
        private static ConfigurationData _configurationData;
        /// <summary>
        /// ConfigurationData to use.
        /// </summary>
        public static ConfigurationData ConfigurationData
        {
            get
            {
                if (_configurationData == null)
                    _configurationData = LoadConfigurationData();
                if (_configurationData == null)
                    throw new System.Exception("Fish-Networking ConfigurationData could not be loaded. Certain features such as code-stripping may not function.");
                return _configurationData;
            }
            private set
            {
                _configurationData = value;
            }
        }

        /// <summary>
        /// File name for configuration disk data.
        /// </summary>
        public const string CONFIG_FILE_NAME = "FishNet.Config.XML";

        /// <summary>
        /// Returns the path for the configuration file.
        /// </summary>
        /// <returns></returns>
        internal static string GetAssetsPath(string additional = "")
        {
            string a = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets");
            if (additional != "")
                a = Path.Combine(a, additional);
            return a;
        }
        /// <summary>
        /// Returns FishNetworking ConfigurationData.
        /// </summary>
        /// <returns></returns>
        internal static ConfigurationData LoadConfigurationData()
        {
            //return new ConfigurationData();
            if (_configurationData == null || !_configurationData.Loaded)
            {
                string configPath = GetAssetsPath(CONFIG_FILE_NAME);
                //string configPath = string.Empty;
                //File is on disk.
                if (File.Exists(configPath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ConfigurationData));
                    FileStream fs = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    _configurationData = (ConfigurationData)serializer.Deserialize(fs);
                    fs.Close();

                    _configurationData.Loaded = true;
                }
                else
                {
                    //If null then make a new instance.
                    if (_configurationData == null)
                        _configurationData = new ConfigurationData();
                    //Don't unset loaded, if its true then it should have proper info.
                    //_configurationData.Loaded = false;
                }
            }

            return _configurationData;

        }

    }


}
