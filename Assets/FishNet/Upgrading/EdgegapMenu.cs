#if !EDGEGAP_PLUGIN_SERVERS
#if UNITY_EDITOR
using FishNet.Documenting;
using UnityEditor;
using UnityEngine;

namespace FishNet.Plugin
{
    [APIExclude]
    public class EdgegapMenu : MonoBehaviour
    {

        /// <summary>
        /// Replaces all components.
        /// </summary>
        [MenuItem("Tools/Get Edgegap Hosting", false, 0)]
        private static void GetEdgegapHosting()
        {
            Application.OpenURL("https://firstgeargames.com/FishNet/Edgegap/");
        }


    }
}
#endif
#endif