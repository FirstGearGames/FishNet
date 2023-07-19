using UnityEditor;
using UnityEngine;

namespace TriInspector.Editor.Samples
{
    internal static class SampleWindowStyles
    {
        public static readonly GUIStyle Padding;
        public static readonly GUIStyle BoxWithPadding;
        public static readonly GUIStyle HeaderDisplayNameLabel;

        static SampleWindowStyles()
        {
            Padding = new GUIStyle(GUI.skin.label)
            {
                padding = new RectOffset(5, 5, 5, 5),
            };

            BoxWithPadding = new GUIStyle(TriEditorStyles.Box)
            {
                padding = new RectOffset(5, 5, 5, 5),
            };

            HeaderDisplayNameLabel = new GUIStyle(EditorStyles.largeLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 17,
                margin = new RectOffset(5, 5, 5, 0),
            };
        }
    }
}