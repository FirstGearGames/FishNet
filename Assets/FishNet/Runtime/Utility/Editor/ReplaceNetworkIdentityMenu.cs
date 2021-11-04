#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#if MIRROR
using Mirror;
#endif

namespace FishNet.Upgrading.Mirror.Editing
{


    public class ReplaceNetworkIdentityMenu : MonoBehaviour
    {


        /// <summary>
        /// Rebuilds sceneIds for open scenes.
        /// </summary>
        [MenuItem("Fish-Networking/Upgrading/Replace Network Identities")]
        static void ReplaceNetworkIdentities()
        {
#if !MIRROR
            Debug.LogError("Mirror must be imported to perform this function.");
#else
            PopulatePrefabs();
#endif
        }


        /// <summary>
        /// Finds all NetworkObjects in project and adds them to defaultPrefabs.
        /// </summary>
        /// <returns>True if was populated from assets.</returns>
        private static void PopulatePrefabs()
        {
#if MIRROR
            int removed = 0;
            int added = 0;
            string[] guids;
            string[] objectPaths;

            guids = AssetDatabase.FindAssets("t:GameObject", null);
            objectPaths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                objectPaths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

            /* Find all network managers which use Single prefab linking
             * as well all network object prefabs. */
            foreach (string item in objectPaths)
            {
                GameObject go = (GameObject)AssetDatabase.LoadAssetAtPath(item, typeof(GameObject));
                /* IMPORTANT IMPORTANT IMPORTANT IMPORTANT 
                 * If you receive an error on this line then
                 * Mirror is not imported. Mirror must be imported to
                 * run this function. 
                 * IMPORTANT IMPORTANT IMPORTANT IMPORTANT */
                if (go.TryGetComponent<NetworkIdentity>(out NetworkIdentity ni))
                {
                    removed++;
                    DestroyImmediate(ni);
                    if (!go.TryGetComponent<NetworkObject>(out _))
                    {
                        go.AddComponent<NetworkObject>();
                        added++;
                    }
                    EditorUtility.SetDirty(go);
                }
            }

            Debug.Log($"Removed {removed} NetworkIdentity components and added {added} NetworkObject components.");
            Debug.LogWarning("You must File -> Save for changes to complete.");
#endif
        }


    }

}
#endif