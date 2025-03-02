using UnityEditor;
using UnityEngine;
using UnityEngine.Windows;

public class CreateNewNetworkBehaviour : MonoBehaviour
{
    [MenuItem("Assets/Create/NetworkBehaviour Script", false, -220)]
    private static void CreateNewAsset()
    {

        ProjectWindowUtil.CreateScriptAssetFromTemplateFile("Assets/Scripts/template.txt", "NewNetworkBehaviourScript.cs");



    }

}
