
using FishNet.Configuring;
using System.IO;
using UnityEngine;
using System.Xml.Serialization;

#if UNITY_EDITOR
using UnityEditor.Compilation;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEditor.Build;
#endif

public class CodeStripping
    
{

    /// <summary>
    /// 
    /// </summary>
    private static ConfigurationData _configurationData;
    /// <summary>
    /// ConfigurationData to use.
    /// </summary>
    internal static ConfigurationData ConfigurationData
    {
        get
        {
            if (_configurationData == null)
                _configurationData = GetConfigurationData();
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
    /// True if making a release build for client.
    /// </summary>
    public static bool ReleasingForClient => (ConfigurationData.IsBuilding && !ConfigurationData.IsHeadless && !ConfigurationData.IsDevelopment);
    /// <summary>
    /// True if making a release build for server.
    /// </summary>
    public static bool ReleasingForServer => (ConfigurationData.IsBuilding && ConfigurationData.IsHeadless && !ConfigurationData.IsDevelopment);
    /// <summary>
    /// Returns if to remove server logic.
    /// </summary>
    /// <returns></returns>
    public static bool RemoveServerLogic
    {
        get
        {
            

        /* This is to protect non pro users from enabling this
         * without the extra logic code.  */
#pragma warning disable CS0162 // Unreachable code detected
        return false;
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
    /// <summary>
    /// True if building and stripping is enabled.
    /// </summary>
    public static bool StripBuild
    {
        get
        {
            

            /* This is to protect non pro users from enabling this
             * without the extra logic code.  */
#pragma warning disable CS0162 // Unreachable code detected
            return false;
#pragma warning restore CS0162 // Unreachable code detected
        }
    }

    /// <summary>
    /// File name for configuration disk data.
    /// </summary>
    public const string CONFIG_FILE_NAME = "FishNet.Config.XML";
    /// <summary>
    /// Old file name for configuration disk data.
    /// </summary>
    public const string CONFIG_FILE_NAME_OLD = "FishNet.Config.json";


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
    internal static ConfigurationData GetConfigurationData()
    {
        //return new ConfigurationData();
        if (_configurationData == null || !_configurationData.Loaded)
        {
            //Check to kill old file.
            string oldConfigPath = GetAssetsPath(CONFIG_FILE_NAME_OLD); //Remove on 2022/06/01
            if (File.Exists(oldConfigPath))
            {
                try
                {
                    File.Delete(oldConfigPath);
                }
                finally { }
            }

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


