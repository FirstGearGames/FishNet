#if UNITY_EDITOR


using UnityEditor;
using UnityEngine;
using UnitySettingsProviderAttribute = UnityEditor.SettingsProviderAttribute;
using UnitySettingsProvider = UnityEditor.SettingsProvider;
using FishNet.Configuring;
using System.IO;
using System.Xml.Linq;


namespace FishNet.Editing.NewNetworkBehaviourScript
{
    internal static class SettingsProvider
    {


        private static PrefabGeneratorConfigurations _settings;
        static string  templatePath;
        [UnitySettingsProvider]
        private static UnitySettingsProvider Create()
        {
            return new("Project/Fish-Networking/New NetworkBehaviour Template", SettingsScope.Project)
            {
                label = "New NetworkBehaviour Template",

                guiHandler = OnGUI,

                keywords = new string[]
                {
                    "Fish",
                    "Networking",
                    "CreateNewNetworkBehaviour",
                    "Template"
                },
            };
        }

        private static void OnGUI(string searchContext)
        {
            if(templatePath == null) templatePath = Application.dataPath + "/FishnetNBTemplate.txt";

            if (GUILayout.Button("Edit template"))
            {
                if (!File.Exists(templatePath))
                {
                    CreateNewNetworkBehaviour.CopyExistingTemplate(templatePath);
                }
                System.Diagnostics.Process.Start(templatePath);
            }
           
            EditorGUILayout.LabelField($"Template path:  {templatePath}");
            
        }

       

      
    }
}

#endif