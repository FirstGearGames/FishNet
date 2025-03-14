#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GameKit.Dependencies.Utilities
{
    public enum EditorLayoutEnableType
    {
        Enabled = 0,
        Disabled = 1,
        DisabledWhilePlaying = 2
    }

    public static class Editing
    {
        /// <summary>
        /// Converts a member string text to PascalCase
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string MemberToPascalCase(this string txt)
        {
            if (txt.Length < 2)
            {
                Debug.LogError($"Text '{txt}' is too short.");
                return string.Empty;
            }

            if (txt[0] != '_')
            {
                Debug.LogError($"Text '{txt}' has the incorrect member prefix.");
                return string.Empty;
            }

            string firstLeter = txt[1].ToString().ToUpper();

            string substring = (txt.Length > 2) ? txt.Substring(2) : string.Empty;
            return $"{firstLeter}{substring}";
        }

        /// <summary>
        /// Converts a member string text to PascalCase
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string PasalCaseToMember(this string txt)
        {
            if (txt.Length < 1)
            {
                Debug.LogError($"Text '{txt}' is too short.");
                return string.Empty;
            }

            string firstLeter = txt[0].ToString().ToLower();

            string subString = (txt.Length > 1) ? txt.Substring(1) : string.Empty;
            return $"_{firstLeter}{subString}";
        }

        /// <summary>
        /// Adds a helpbox field.
        /// </summary>
        public static void AddHelpBox(string text, MessageType messageType = MessageType.Info)
        {
            EditorGUILayout.HelpBox(text, messageType);
        }

        /// <summary>
        /// Adds a property field.
        /// </summary>
        public static void AddPropertyField(SerializedProperty sp, string fieldName, string tooltip = "")
        {
            if (tooltip == "")
                tooltip = sp.tooltip;
            
            EditorGUILayout.PropertyField(sp, new GUIContent(fieldName, tooltip));
        }

        /// <summary>
        /// Adds a property field.
        /// </summary>
        public static void AddPropertyField(SerializedProperty sp, GUIContent guiContent)
        {
            EditorGUILayout.PropertyField(sp, guiContent);
        }

        /// <summary>
        /// Adds a property field.
        /// </summary>
        public static void AddPropertyField(SerializedProperty sp, GUIContent guiContent = null, EditorLayoutEnableType enableType = EditorLayoutEnableType.Enabled, params GUILayoutOption[] options)
        {
            bool disable = IsDisableLayoutType(enableType);
            if (disable)
                GUI.enabled = false;

            EditorGUILayout.PropertyField(sp, guiContent, options);

            if (disable)
                GUI.enabled = true;
        }

        /// <summary>
        /// Adds a property field.
        /// </summary>
        /// <param name="enabled">True to have property enabled.</param>
        [Obsolete("Use AddPropertyField(SerializedProperty, GUIContent, EditorLayoutEnableType, GUILayoutOption.")]
        public static void AddPropertyField(SerializedProperty sp, GUIContent guiContent = null, bool enabled = true, params GUILayoutOption[] options)
        {
            EditorLayoutEnableType enableType = (enabled) ? EditorLayoutEnableType.Enabled : EditorLayoutEnableType.Disabled;
            bool disable = IsDisableLayoutType(enableType);
            if (disable)
                GUI.enabled = false;

            EditorGUILayout.PropertyField(sp, guiContent, options);

            if (disable)
                GUI.enabled = true;
        }

        /// <summary>
        /// Adds an object field.
        /// </summary>
        public static void AddObjectField(string label, MonoScript ms, Type type, bool allowSceneObjects, EditorLayoutEnableType enableType = EditorLayoutEnableType.Enabled, params GUILayoutOption[] options)
        {
            bool disable = IsDisableLayoutType(enableType);
            if (disable)
                GUI.enabled = false;

            EditorGUILayout.ObjectField("Script:", ms, type, allowSceneObjects, options);

            if (disable)
                GUI.enabled = true;
        }

        /// <summary>
        /// Disables GUI if playing.
        /// </summary>
        public static void DisableGUIIfPlaying()
        {
            if (Application.isPlaying)
                GUI.enabled = false;
        }

        /// <summary>
        /// Enables GUI if playing.
        /// </summary>
        public static void EnableGUIIfPlaying()
        {
            if (Application.isPlaying)
                GUI.enabled = true;
        }

        /// <summary>
        /// Returns if a layout field should be disabled.
        /// </summary>
        /// <param name="enableType"></param>
        /// <returns></returns>
        private static bool IsDisableLayoutType(EditorLayoutEnableType enableType)
        {
            return (enableType == EditorLayoutEnableType.Disabled || (enableType == EditorLayoutEnableType.DisabledWhilePlaying && Application.isPlaying));
        }
    }
}

#endif