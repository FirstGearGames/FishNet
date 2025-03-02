#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnitySettingsProviderAttribute = UnityEditor.SettingsProviderAttribute;
using UnitySettingsProvider = UnityEditor.SettingsProvider;
using FishNet.Configuring;
using System.Linq;

namespace FishNet.Editing.NewNetworkBehaviourScript
{
    internal static class SettingsProvider
    {


        private static PrefabGeneratorConfigurations _settings;


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
                    "Prefab",
                    "Objects",
                    "Generator",
                },
            };
        }

        private static void OnGUI(string searchContext)
        {
          
        }

       

      
    }
}

#endif