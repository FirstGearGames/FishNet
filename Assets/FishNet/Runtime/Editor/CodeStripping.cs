
using FishNet.Configuring;
using System.IO;
using UnityEngine;
using System.Xml.Serialization;

#if UNITY_EDITOR
using FishNet.Editing.PrefabCollectionGenerator;
using UnityEditor.Compilation;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEditor.Build;
#endif

namespace FishNet.Configuring
{


    public class CodeStripping
    //PROSTART
#if UNITY_EDITOR
    : IPreprocessBuildWithReport, IPostprocessBuildWithReport
#endif
    //PROEND
    {

        /// <summary>
        /// True if making a release build for client.
        /// </summary>
        public static bool ReleasingForClient => (Configuration.Configurations.CodeStripping.IsBuilding && !Configuration.Configurations.CodeStripping.IsHeadless && !Configuration.Configurations.CodeStripping.IsDevelopment);
        /// <summary>
        /// True if making a release build for server.
        /// </summary>
        public static bool ReleasingForServer => (Configuration.Configurations.CodeStripping.IsBuilding && Configuration.Configurations.CodeStripping.IsHeadless && !Configuration.Configurations.CodeStripping.IsDevelopment);
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
                if (Configuration.Configurations.CodeStripping.IsHeadless)
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
                if (!Configuration.Configurations.CodeStripping.IsHeadless)
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
                if (!Configuration.Configurations.CodeStripping.IsBuilding || Configuration.Configurations.CodeStripping.IsDevelopment)
                    return false;
                //Stripping isn't enabled.
                if (!Configuration.Configurations.CodeStripping.StripReleaseBuilds)
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
        /// Technique to strip methods.
        /// </summary>
        public static StrippingTypes StrippingType => (StrippingTypes)Configuration.Configurations.CodeStripping.StrippingType;

        private static object _compilationContext;
        public int callbackOrder => 0;
#if UNITY_EDITOR

        public void OnPreprocessBuild(BuildReport report)
        {
            Generator.IgnorePostProcess = true;
            Generator.GenerateFull();
            CompilationPipeline.compilationStarted += CompilationPipelineOnCompilationStarted;
            CompilationPipeline.compilationFinished += CompilationPipelineOnCompilationFinished;

            //PROSTART
            //Set building values.
            Configuration.Configurations.CodeStripping.IsBuilding = true;

            BuildOptions options = report.summary.options;
#if UNITY_2021_2_OR_NEWER && !UNITY_ANDROID && !UNITY_IPHONE && !UNITY_WEBGL && !UNITY_WSA
            Configuration.Configurations.CodeStripping.IsHeadless = (report.summary.GetSubtarget<StandaloneBuildSubtarget>() == StandaloneBuildSubtarget.Server);
#else
            Configuration.Configurations.CodeStripping.IsHeadless = options.HasFlag(BuildOptions.EnableHeadlessMode);
#endif
            Configuration.Configurations.CodeStripping.IsDevelopment = options.HasFlag(BuildOptions.Development);

            //Write to file.
            Configuration.Configurations.Write(false);
            //PROEND
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

            BuildingEnded();
        }

        private void BuildingEnded()
        {
            //PROSTART
            //Set building values.
            Configuration.Configurations.CodeStripping.IsBuilding = false;
            Configuration.Configurations.CodeStripping.IsHeadless = false;
            Configuration.Configurations.CodeStripping.IsDevelopment = false;
            //Write to file.
            Configuration.Configurations.Write(false);
            //PROEND

            Generator.IgnorePostProcess = false;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            //PROSTART
            if (Configuration.Configurations.CodeStripping.IsBuilding)
                //PROEND
                BuildingEnded();
        }
#endif
    }

}
