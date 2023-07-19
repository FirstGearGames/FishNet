using UnityEditor;
using UnityEngine;

namespace TriInspector.Utilities
{
    public static class TriEditorGUI
    {
        public static void Foldout(Rect rect, TriProperty property)
        {
            var content = property.DisplayNameContent;
            property.IsExpanded = EditorGUI.Foldout(rect, property.IsExpanded, content, true);
        }

        public static void DrawBox(Rect position, GUIStyle style,
            bool isHover = false, bool isActive = false, bool on = false, bool hasKeyboardFocus = false)
        {
            if (Event.current.type == EventType.Repaint)
            {
                style.Draw(position, GUIContent.none, isHover, isActive, on, hasKeyboardFocus);
            }
        }
    }
}