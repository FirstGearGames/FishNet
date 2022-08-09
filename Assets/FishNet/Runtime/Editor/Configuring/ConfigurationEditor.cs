#if UNITY_EDITOR
using FishNet.Editing.PrefabCollectionGenerator;
using FishNet.Object;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Editing
{
    public class ConfigurationEditor : EditorWindow
    {

        [MenuItem("Fish-Networking/Configuration", false, 0)]
        public static void ShowConfiguration()
        {
            SettingsService.OpenProjectSettings("Project/Fish-Networking/Configuration");
        }

    }

    public class OpenDocumentationMenu : MonoBehaviour
    {
        /// <summary>
        /// Opens the documentation.
        /// </summary>
        [MenuItem("Fish-Networking/Documentation", false, int.MaxValue)]
        public static void OpenDocumentation()
        {
            System.Diagnostics.Process.Start("https://fish-networking.gitbook.io/docs/");
        }


    }


    public class RebuildSceneIdMenu : MonoBehaviour
    {
        /// <summary>
        /// Rebuilds sceneIds for open scenes.
        /// </summary>
        [MenuItem("Fish-Networking/Rebuild SceneIds", false, 20)]
        public static void RebuildSceneIds()
        {
            int generatedCount = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);

                ListCache<NetworkObject> nobs;
                SceneFN.GetSceneNetworkObjects(s, false, out nobs);
                for (int z = 0; z < nobs.Written; z++)
                {
                    NetworkObject nob = nobs.Collection[z];
                    nob.TryCreateSceneID();
                    EditorUtility.SetDirty(nob);
                }
                generatedCount += nobs.Written;

                ListCaches.StoreCache(nobs);
            }

            Debug.Log($"Generated sceneIds for {generatedCount} objects over {SceneManager.sceneCount} scenes. Please save your open scenes.");
        }


    }

    public class RefreshDefaultPrefabsMenu : MonoBehaviour
    {
        /// <summary>
        /// Rebuilds the DefaultPrefabsCollection file.
        /// </summary>
        [MenuItem("Fish-Networking/Refresh Default Prefabs", false, 21)]
        public static void RebuildDefaultPrefabs()
        {
            Debug.Log("Refreshing default prefabs.");
            Generator.GenerateFull(null, true);
        }


    }

}
#endif