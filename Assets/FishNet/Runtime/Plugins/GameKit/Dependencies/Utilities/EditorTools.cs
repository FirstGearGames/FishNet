#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameKit.Dependencies.Utilities
{
    public static class PropertyDrawerToolExtensions
    {
        /// <summary>
        /// Returns GetPropertyHeight value based on drawerTool properties.
        /// </summary>
        public static float GetPropertyHeight(this PropertyDrawerTool drawerTool)
        {
            if (drawerTool == null)
                return EditorGUIUtility.singleLineHeight;

            return (EditorGUIUtility.singleLineHeight * drawerTool.LineSpacingMultiplier * drawerTool.PropertiesDrawn);
        }
    }

    /// <summary>
    /// Various utility classes relating to floats.
    /// </summary>
    public class PropertyDrawerTool
    {
        public PropertyDrawerTool()
        {
            Debug.LogError($"This initializer is not supported. Use the initializer with arguments.");
        }

        public PropertyDrawerTool(Rect position, float lineSpacingMultiplier = 1f)
        {
            Position = position;
            LineSpacingMultiplier = lineSpacingMultiplier;
            Position = position;
            _startingIndent = EditorGUI.indentLevel;
        }

        /// <summary>
        /// Starting position as indicated by the OnGUI method. 
        /// </summary>
        /// <remarks>This value may be modified by user code.</remarks>
        public Rect Position = default;
        /// <summary>
        /// Preferred spacing between each draw.
        /// </summary>
        public float LineSpacingMultiplier;
        /// <summary>
        /// Number of entries drawn by this object.
        /// </summary>
        public int PropertiesDrawn = 0;

        /// <summary>
        /// Additional position Y of next draw.
        /// </summary>
        private float _additionalPositionY = 0;
        /// <summary>
        /// Indent level during initialization.
        /// </summary>
        private readonly int _startingIndent;

        /// <summary>
        /// Sets EditorGUI.Indent to the level it were when initializing this class.
        /// </summary>
        public void SetIndentToStarting() => EditorGUI.indentLevel = _startingIndent;

        /// <summary>
        /// Draws a label.
        /// </summary>
        public void DrawLabel(GUIContent lLabel) => DrawLabel(lLabel, EditorStyles.label.fontStyle, indent: 0);

        /// <summary>
        /// Draws a label.
        /// </summary>
        public void DrawLabel(GUIContent lLabel, FontStyle styleOverride) => DrawLabel(lLabel, styleOverride, indent: 0);

        /// <summary>
        /// Draws a label.
        /// </summary>
        public void DrawLabel(GUIContent lLabel, FontStyle styleOverride, int indent)
        {
            PropertiesDrawn++;

            if (indent != 0)
                EditorGUI.indentLevel += indent;

            //Set style.
            FontStyle startingStyle = EditorStyles.label.fontStyle;
            EditorStyles.label.fontStyle = styleOverride;

            EditorGUI.PrefixLabel(GetRect(), GUIUtility.GetControlID(FocusType.Passive), lLabel);

            EditorStyles.label.fontStyle = startingStyle;

            if (indent != 0)
                EditorGUI.indentLevel -= indent;
        }

        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop) => DrawProperty(prop, lLabel: "", indent: 0);

        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop, string label) => DrawProperty(prop, new GUIContent(label), EditorStyles.label.fontStyle, indent: 0);

        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop, GUIContent content) => DrawProperty(prop, content, EditorStyles.label.fontStyle, indent: 0);
        
        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop, int indent) => DrawProperty(prop, lLabel: "", indent);

        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop, string lLabel, int indent) => DrawProperty(prop, lLabel, EditorStyles.label.fontStyle, indent);

        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop, GUIContent content, int indent) => DrawProperty(prop, content, EditorStyles.label.fontStyle, indent);
        
        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop, GUIContent content, FontStyle labelStyle) => DrawProperty(prop, content, labelStyle, indent: 0);
        
        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop, string lLabel, FontStyle labelStyle, int indent)
        {
            GUIContent content = (lLabel == "") ? default : new GUIContent(lLabel);

            DrawProperty(prop, content, labelStyle, indent);
        }

        /// <summary>
        /// Draws a property.
        /// </summary>
        public void DrawProperty(SerializedProperty prop, GUIContent content, FontStyle labelStyle, int indent)
        {
            PropertiesDrawn++;

            EditorGUI.indentLevel += indent;

            FontStyle startingStyle = EditorStyles.label.fontStyle;
            EditorStyles.label.fontStyle = labelStyle;

            EditorGUI.PropertyField(GetRect(), prop, content);

            EditorStyles.label.fontStyle = startingStyle;

            EditorGUI.indentLevel -= indent;
        }

        /// <summary>
        /// Gets the next Rect to draw at.
        /// </summary>
        /// <returns></returns>
        public Rect GetRect(float? lineSpacingMultiplierOverride = null)
        {
            float multiplier = lineSpacingMultiplierOverride ?? LineSpacingMultiplier;
            
            Rect result = new(Position.x, Position.y + _additionalPositionY, Position.width, EditorGUIUtility.singleLineHeight * multiplier);
            
            _additionalPositionY += EditorGUIUtility.singleLineHeight * multiplier;
            
            return result;
        }
    }
}
#endif