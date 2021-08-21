#if UNITY_EDITOR
using UnityEditor;

namespace Fluidity.Editing
{
    /* Thanks to SoftwareGuy aka Coburn for this file. See links below. */
    public class Toolbox
    {
        //[MenuItem("Ignorance/RTFM/Github Repository")]
        //private static void LaunchGithubRepo()
        //{
        //    UnityEngine.Application.OpenURL("https://github.com/SoftwareGuy/Ignorance");
        //}

        //[MenuItem("Ignorance/RTFM/Github Issue Tracker")]
        //private static void LaunchGithubIssueTracker()
        //{
        //    UnityEngine.Application.OpenURL("https://github.com/SoftwareGuy/Ignorance/issues");
        //}

        [MenuItem("Fluidity/RTFM/ENet-CSharp Fork")]
        private static void LaunchENetCSharpForkRepo()
        {
            UnityEngine.Application.OpenURL("https://github.com/SoftwareGuy/ENet-CSharp");
        }

        [MenuItem("Fluidity/Debug/Reveal ENet Native Library Name")]
        public static void RevealEnetLibraryName()
        {
            EditorUtility.DisplayDialog("Enet Library Name", $"Use this for debugging.\nYour platform expects the native Enet library to be called: {ENet.Native.nativeLibraryName}", "Got it");
        }
    }
}
#endif
