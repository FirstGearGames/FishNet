using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Windows;
using File = System.IO.File;

#if DEBUG
namespace FishNet.Editing.NewNetworkBehaviourScript
{
    internal sealed class CreateNewNetworkBehaviour : MonoBehaviour
    {

        private bool firstTimeLoaded = false;
        private string templatePath;
        const string defaultTemplateName = "1-Scripting__MonoBehaviour Script-NewMonoBehaviourScript.cs.txt";

        [MenuItem("Assets/Create/NetworkBehaviour Script", false, -220)]
        private static void CreateNewAsset()
        {
            string templatePath = Application.dataPath + "/FishNet/Assets/FishNet/Runtime/Editor/NewNetworkBehaviour/template.txt";
            if (!File.Exists(templatePath))
            {
                File.Copy(EditorApplication.applicationContentsPath + "/Resources/ScriptTemplates/" + defaultTemplateName, templatePath);
                string fileContent = File.ReadAllText(templatePath);
                Debug.Log(fileContent);
                fileContent = fileContent.ReplaceFirstOccurence("MonoBehaviour", "NetworkBehaviour");
                fileContent = fileContent.Replace("using UnityEngine;", "using UnityEngine;\nusing FishNet.Object;");
                File.WriteAllText(templatePath,fileContent);

               
            }
            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(templatePath, "NewNetworkBehaviourScript.cs");
       
            


        }


        

    }
    internal  static  class NetworkBehaviourStringExtension
    {
        public static string ReplaceFirstOccurence(this string str, string oldValue, string newValue)
        {
            int pos = str.IndexOf(oldValue);
            if (pos < 0) return str;
            return str.Substring(0, pos) + newValue + str.Substring(pos + oldValue.Length);

        }

    }

}
#endif