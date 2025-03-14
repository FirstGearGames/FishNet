
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using File = System.IO.File;

#if UNITY_EDITOR
namespace FishNet.Editing.NewNetworkBehaviourScript
{
    internal sealed class CreateNewNetworkBehaviour : MonoBehaviour
    {

        private const string DEFAULT_TEMPLATE_NAME = "1-Scripting__MonoBehaviour Script-NewMonoBehaviourScript.cs.txt";

        [MenuItem("Assets/Create/NetworkBehaviour Script", false, -220)]
        private static void CreateNewAsset()
        {
            string templatePath;
            if (Directory.Exists(Application.dataPath + "/FishNet"))
            {
                templatePath = Application.dataPath + "/FishNet/Assets/FishNet/Runtime/Editor/NewNetworkBehaviour/template.txt";
            }
            else
            {
                templatePath = "Packages/com.firstgeargames.fishnet/Runtime/Editor/NewNetworkBehaviour/template.txt";
            }
            
            if (!File.Exists(templatePath))
            {

                CopyExistingTemplate(templatePath);
               
            }
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewNetworkBehaviourScript.cs");
       
            


        }

        public static void CopyExistingTemplate(string templatePath)
        {
            File.Copy(EditorApplication.applicationContentsPath + "/Resources/ScriptTemplates/" + DEFAULT_TEMPLATE_NAME, templatePath);
            string fileContent = File.ReadAllText(templatePath);
            fileContent = fileContent.ReplaceFirstOccurence("MonoBehaviour", "NetworkBehaviour");
            fileContent = fileContent.Replace("using UnityEngine;", "using UnityEngine;\nusing FishNet.Object;");
            File.WriteAllText(templatePath, fileContent);
        }

        

    }
    internal  static  class NetworkBehaviourStringExtension
    {
        public static string ReplaceFirstOccurence(this string str, string oldValue, string newValue)
        {
            int pos = str.IndexOf(oldValue, StringComparison.Ordinal);
            if (pos < 0) return str;
            return str.Substring(0, pos) + newValue + str.Substring(pos + oldValue.Length);

        }

    }

}
#endif