#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class DescriptionWindow : EditorWindow
{
    private static bool _showInEnglish = true;
    private int _selectedTab = 0;
    private readonly string[] _tabNames = { "Network Object", "Network Observer", "SyncVars", "Ownership"};

    private GUIStyle _tabStyle;
    private GUIStyle _tabSelectedStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _boxStyle;
    
    private Vector2 _scrollPosition; // For scrolling
    
    public static void ShowWindow()
    {
        GetWindow<DescriptionWindow>("NetworkObject Description");
    }

    private void OnEnable()
    {
        // Initialize custom styles
        _tabStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { background = MakeTex(1, 1, new Color(0.1f, 0.3f, 0.5f)) }, // Darker blue
            active = { background = MakeTex(1, 1, new Color(0.2f, 0.4f, 0.6f)) }, // Lighter blue
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            stretchWidth = true,
            stretchHeight = true,
            border = new RectOffset(1, 1, 1, 1)
        };

        _tabSelectedStyle = new GUIStyle(_tabStyle)
        {
            normal = { background = MakeTex(1, 1, new Color(0.2f, 0.4f, 0.6f)) }, // Selected tab color
            active = { background = MakeTex(1, 1, new Color(0.3f, 0.5f, 0.7f)) } // Lighter selected color
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            normal = { background = MakeTex(100, 1, new Color(0.1f, 0.3f, 0.5f)) }, // Darker blue
            active = { background = MakeTex(100, 1, new Color(0.2f, 0.4f, 0.6f)) }, // Lighter blue
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(1, 1, new Color(0.0745f, 0.3647f, 0.6275f)) } // Light blue
        };
    }

    private void OnGUI()
    {
        // Draw background color
        Rect r = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(r, new Color(0.0745f, 0.3647f, 0.6275f, 1f)); // Light blue

        GUILayout.Space(10);

        // Tabs
        GUILayout.BeginHorizontal();
        for (int i = 0; i < _tabNames.Length; i++)
        {
            GUIStyle currentTabStyle = (_selectedTab == i) ? _tabSelectedStyle : _tabStyle;
            if (GUILayout.Button(_tabNames[i], currentTabStyle, GUILayout.Height(30)))
            {
                _selectedTab = i;
                Repaint();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Begin scroll view
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        // Content display
        switch (_selectedTab)
        {
            case 0:
                NetworkObjectDrawer.ShowNetworkObjectDescription(_showInEnglish);
                break;
            case 1:
                NetworkObserverDrawer.ShowNetworkObserverDescription(_showInEnglish);
                break;
            case 2:
                SyncVarsDrawer.ShowSyncVarsDescription(_showInEnglish);
                break;
            case 3:
                OwnershipDrawer.ShowOwnershipDescription(_showInEnglish);
                break;
        }

        // End scroll view
        EditorGUILayout.EndScrollView();

        // Language switch
        GUILayout.Space(20);
        GUILayout.Label("Language:", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("English", _buttonStyle, GUILayout.Width(100)))
        {
            _showInEnglish = true;
            Repaint();
        }
        if (GUILayout.Button("中文", _buttonStyle, GUILayout.Width(100)))
        {
            _showInEnglish = false;
            Repaint();
        }
        GUILayout.EndHorizontal();
    }

    // Helper function to create a texture
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Texture2D tex = new Texture2D(width, height);
        Color[] pix = tex.GetPixels();
        for (int i = 0; i < pix.Length; ++i)
            pix[i] = col;
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}
#endif