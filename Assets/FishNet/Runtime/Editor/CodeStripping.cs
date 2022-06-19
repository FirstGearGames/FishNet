
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

namespace FishNet.Configuring
{


    public class CodeStripping
    
    {

        /// <summary>
        /// True if making a release build for client.
        /// </summary>
        public static bool ReleasingForClient => (Configuration.ConfigurationData.IsBuilding && !Configuration.ConfigurationData.IsHeadless && !Configuration.ConfigurationData.IsDevelopment);
        /// <summary>
        /// True if making a release build for server.
        /// </summary>
        public static bool ReleasingForServer => (Configuration.ConfigurationData.IsBuilding && Configuration.ConfigurationData.IsHeadless && !Configuration.ConfigurationData.IsDevelopment);
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

        
    }

}
