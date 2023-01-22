#if UNITY_EDITOR
using FishNet.Editing.PrefabCollectionGenerator;
using FishNet.Object;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System.Collections.Generic;
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
        [MenuItem("Fish-Networking/Refresh Default Prefabs", false, 22)]
        public static void RebuildDefaultPrefabs()
        {
            Debug.Log("Refreshing default prefabs.");
            Generator.GenerateFull(null, true);
        }

    }


    public class RemoveDuplicateNetworkObjectsMenu : MonoBehaviour
    {
        /// <summary>
        /// Iterates all network object prefabs in the project and open scenes, removing NetworkObject components which exist multiple times on a single object.
        /// </summary>
        [MenuItem("Fish-Networking/Remove Duplicate NetworkObjects", false, 21)]

        public static void RemoveDuplicateNetworkObjects()
        {
            List<NetworkObject> foundNobs = new List<NetworkObject>();

            foreach (string path in Generator.GetPrefabFiles("Assets", new HashSet<string>(), true))
            {
                NetworkObject nob = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
                if (nob != null)
                    foundNobs.Add(nob);
            }

            //Now add scene objects.
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
                for (int z = 0; z < nobs.Written; z++)
                    foundNobs.Add(nobs.Collection[i]);

                ListCaches.StoreCache(nobs);
            }

            //Remove duplicates.
            int removed = 0;
            foreach (NetworkObject nob in foundNobs)
            {
                int count = nob.RemoveDuplicateNetworkObjects();
                if (count > 0)
                    removed += count;
            }

            Debug.Log($"Removed {removed} duplicate NetworkObjects. Please save your open scenes and project.");
        }

    }





    /// <summary>
    /// Contributed by YarnCat! Thank you!
    /// </summary>
    public class FishNetGettingStartedMenu : EditorWindow
    {

        [MenuItem("Fish-Networking/Getting Started")]
        public static void GettingStartedMenu()
        {
            FishNetGettingStartedMenu window = (FishNetGettingStartedMenu)EditorWindow.GetWindow(typeof(FishNetGettingStartedMenu));
            window.position = new Rect(0, 0, 320, 355);
            Rect mainPos;
#if UNITY_2020_1_OR_NEWER
            mainPos = EditorGUIUtility.GetMainWindowPosition();
#else
            mainPos = new Rect(Vector2.zero, Vector2.zero);
#endif
            var pos = window.position;  
            float w = (mainPos.width - pos.width) * 0.5f;
            float h = (mainPos.height - pos.height) * 0.5f;
            pos.x = mainPos.x + w;
            pos.y = mainPos.y + h;
            window.position = pos;

            window._fishnetLogo = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/FishNet/Runtime/Editor/Textures/UI/Logo_With_Text.png", typeof(Texture));
            window._labelStyle = new GUIStyle("label");
            window._labelStyle.fontSize = 24;
            window._labelStyle.wordWrap = true;   
            //window.labelStyle.alignment = TextAnchor.MiddleCenter;
            window._labelStyle.normal.textColor = new Color32(74, 195, 255, 255);

            window._reviewButtonBg = MakeBackgroundTexture(1, 1, new Color32(52, 111, 255, 255));
            window._reviewButtonBgHover = MakeBackgroundTexture(1, 1, new Color32(99, 153, 255, 255));
            window._reviewButtonStyle = new GUIStyle("button");
            window._reviewButtonStyle.fontSize = 18;
            window._reviewButtonStyle.fontStyle = FontStyle.Bold;
            window._reviewButtonStyle.normal.background = window._reviewButtonBg;
            window._reviewButtonStyle.active.background = window._reviewButtonBgHover;
            window._reviewButtonStyle.focused.background = window._reviewButtonBgHover;
            window._reviewButtonStyle.onFocused.background = window._reviewButtonBgHover;
            window._reviewButtonStyle.hover.background = window._reviewButtonBgHover;
            window._reviewButtonStyle.onHover.background = window._reviewButtonBgHover;
            window._reviewButtonStyle.alignment = TextAnchor.MiddleCenter;
            window._reviewButtonStyle.normal.textColor = new Color(1, 1, 1, 1);

        }


        private static bool _subscribed;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            SubscribeToUpdate();
        }

        private static void SubscribeToUpdate()
        {
            if (!_subscribed && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _subscribed = true;
                EditorApplication.update += ShowGettingStarted;
            }
        }

        private static void ShowGettingStarted()
        {
            EditorApplication.update -= ShowGettingStarted;

            string showedSplash = "ShowedFishNetGettingStarted";
            bool shown = EditorPrefs.GetBool(showedSplash, false);

            if (!shown)
            {
                EditorPrefs.SetBool(showedSplash, true);
                GettingStartedMenu();
            }
        }

        private Texture2D _fishnetLogo, _reviewButtonBg, _reviewButtonBgHover;
        private GUIStyle _labelStyle, _reviewButtonStyle;

        void OnGUI()
        {


            GUILayout.Box(_fishnetLogo, GUILayout.Width(this.position.width), GUILayout.Height(128));
            GUILayout.Space(20);

            GUILayout.Label("Have you considered leaving us a review?", _labelStyle, GUILayout.Width(280));

            GUILayout.Space(10);

            if (GUILayout.Button("Leave us a review!", _reviewButtonStyle))
            {
                Application.OpenURL("https://assetstore.unity.com/packages/tools/network/fish-net-networking-evolved-207815");
            }

            GUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Documentation", GUILayout.Width(this.position.width * 0.485f)))
            {
                Application.OpenURL("https://fish-networking.gitbook.io/docs/");
            }

            if (GUILayout.Button("Discord", GUILayout.Width(this.position.width * 0.485f)))
            {
                Application.OpenURL("https://discord.gg/Ta9HgDh4Hj");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("FishNet Pro", GUILayout.Width(this.position.width * 0.485f)))
            {
                Application.OpenURL("https://fish-networking.gitbook.io/docs/master/pro");
            }

            if (GUILayout.Button("Github", GUILayout.Width(this.position.width * 0.485f)))
            {
                Application.OpenURL("https://github.com/FirstGearGames/FishNet");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Pro Downloads", GUILayout.Width(this.position.width * 0.485f)))
            {
                Application.OpenURL("https://www.firstgeargames.com/");
            }

            if (GUILayout.Button("Examples", GUILayout.Width(this.position.width * 0.485f)))
            {
                Application.OpenURL("https://fish-networking.gitbook.io/docs/manual/tutorials/example-projects");
            }
            EditorGUILayout.EndHorizontal();

            //GUILayout.Space(20);
            //_showOnStartupSelected = EditorGUILayout.Popup("Show on Startup", _showOnStartupSelected, showOnStartupOptions);
        }
        //private string[] showOnStartupOptions = new string[] { "Always", "On new version", "Never", };
        //private int _showOnStartupSelected = 1;

        private static Texture2D MakeBackgroundTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            Texture2D backgroundTexture = new Texture2D(width, height);
            backgroundTexture.SetPixels(pixels);
            backgroundTexture.Apply();
            return backgroundTexture;
        }
    }

}
#endif