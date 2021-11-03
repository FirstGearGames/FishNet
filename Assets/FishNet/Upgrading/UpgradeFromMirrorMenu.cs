#if UNITY_EDITOR
using FishNet.Documenting;
using UnityEditor;
using UnityEngine;

namespace FishNet.Upgrading.Mirror.Editing
{

    /* IMPORTANT IMPORTANT IMPORTANT IMPORTANT 
    * If you receive errors about missing Mirror components,
    * such as NetworkIdentity, then remove MIRROR and any other
    * MIRROR defines.
    * Project Settings -> Player -> Other -> Scripting Define Symbols.
    * 
    * If you are also using my assets add FGG_ASSETS to the defines, and
    * then remove it after running this script. */
    [APIExclude]
    public class UpgradeFromMirrorMenu : MonoBehaviour
    {

        /// <summary>
        /// Replaces all components.
        /// </summary>
        [MenuItem("Fish-Networking/Upgrading/Mirror/Replace Components")]
        private static void ReplaceComponents()
        {
#if MIRROR
            MirrorUpgrade result = GameObject.FindObjectOfType<MirrorUpgrade>();
            if (result != null)
            {
                Debug.LogError("MirrorUpgrade already exist in the scene. This suggests an operation is currently running.");
                return;
            }

            GameObject iteratorGo = new GameObject();
            iteratorGo.name = "MirrorUpgrade";
            iteratorGo.AddComponent<MirrorUpgrade>();
#else
            Debug.LogError("Mirror must be imported to perform this function.");
#endif
        }


    }
}
#endif
