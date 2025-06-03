#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

namespace FishNet.Editing
{
    /* When creating dedicated server builds this will place an empty file within
     * the build folder.
     *
     * The file contains absolutely no information, and is used by our partners to identify how many of their customers are using
     * Fish-Networking.
     *
     * The created file is not required -- you may delete the file and/or this code, but please
     * consider retaining the file as it helps keep FishNet free. */

    public class BuildIdentifier
    {
        [PostProcessBuild(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            //Not a server build.
#if !SERVER_BUILD && !UNITY_SERVER
            return;
#endif

            string buildPath = Path.GetDirectoryName(pathToBuiltProject);
            if (buildPath == null)
                return;

            //Validate that we are in the right folder.
            string crashHandler = Path.Combine(buildPath, "UnityCrashHandler64.exe");
            if (File.Exists(crashHandler))
            {
                //Try to create the empty file.
                try
                {
                    string filePath = Path.Combine(buildPath, "BuildIdentifier.json");
                    File.WriteAllText(filePath, string.Empty);
                }
                finally { }
            }
        }
    }
}

#endif