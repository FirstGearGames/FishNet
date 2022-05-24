
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
    //PROSTART
#if UNITY_EDITOR
    : IPreprocessBuildWithReport, IPostprocessBuildWithReport
#endif
    //PROEND
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
            //PROSTART
            if (!StripBuild)
                return false;
            //Cannot remove server code if headless.
            if (ConfigurationData.IsHeadless)
                return false;

            return true;
            //PROSTART

            /* This is to protect non pro users from enabling this
             * without the extra logic code.  */
#pragma warning disable CS0162 // Unreachable code detected
            return false;
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
    /// <summary>
    /// Returns if to remove server logic.
    /// </summary>
    /// <returns></returns>
    public static bool RemoveClientLogic
    {
        get
        { 
        //PROSTART
        if (!StripBuild)
            return false;
        //Cannot remove server code if headless.
        if (!ConfigurationData.IsHeadless)
            return false;

        return true;
        //PROEND

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
            //PROSTART
            if (!ConfigurationData.IsBuilding || ConfigurationData.IsDevelopment)
                return false;
            //Stripping isn't enabled.
            if (!ConfigurationData.StripReleaseBuilds)
                return false;

            //Fall through.
            return true;
            //PROEND

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


    //PROSTART
    #region Pro stuff.
    private static object _compilationContext;
    public int callbackOrder => 0;

#if UNITY_EDITOR

    public void OnPreprocessBuild(BuildReport report)
    {
        CompilationPipeline.compilationStarted += CompilationPipelineOnCompilationStarted;
        CompilationPipeline.compilationFinished += CompilationPipelineOnCompilationFinished;

        //Set building values.
        ConfigurationData.IsBuilding = true;

        BuildOptions options = report.summary.options;
        ConfigurationData.IsHeadless = options.HasFlag(BuildOptions.EnableHeadlessMode);
        ConfigurationData.IsDevelopment = options.HasFlag(BuildOptions.Development);

        //Write to file.
        ConfigurationData.Write(GetAssetsPath(CONFIG_FILE_NAME), false);
    }
    /* Solution for builds ending with errors and not triggering OnPostprocessBuild.
    * Link: https://gamedev.stackexchange.com/questions/181611/custom-build-failure-callback
    */
    private void CompilationPipelineOnCompilationStarted(object compilationContext)
    {
        _compilationContext = compilationContext;
    }

    private void CompilationPipelineOnCompilationFinished(object compilationContext)
    {
        if (compilationContext != _compilationContext)
            return;

        _compilationContext = null;

        CompilationPipeline.compilationStarted -= CompilationPipelineOnCompilationStarted;
        CompilationPipeline.compilationFinished -= CompilationPipelineOnCompilationFinished;

        UnsetIsBuilding();
    }

    private void UnsetIsBuilding()
    {
        //Set building values.
        ConfigurationData.IsBuilding = false;
        ConfigurationData.IsHeadless = false;
        ConfigurationData.IsDevelopment = false;
        //Write to file.
        ConfigurationData.Write(GetAssetsPath(CONFIG_FILE_NAME), false);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (ConfigurationData.IsBuilding)
            UnsetIsBuilding();
    }
#endif
    #endregion
    //PROEND
}


