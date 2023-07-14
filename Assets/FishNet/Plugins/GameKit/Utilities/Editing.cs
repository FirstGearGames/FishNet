
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GameKit.Utilities
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
        /// Adds a property field.
        /// </summary>
        public static void AddPropertyField(SerializedProperty sp, GUIContent guiContent, EditorLayoutEnableType enableType = EditorLayoutEnableType.Enabled, params GUILayoutOption[] options)
        {
            bool disable = DisableLayout(enableType);
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
            bool disable = DisableLayout(enableType);
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
        private static bool DisableLayout(EditorLayoutEnableType enableType)
        {
            return (enableType == EditorLayoutEnableType.Disabled || (enableType == EditorLayoutEnableType.DisabledWhilePlaying && Application.isPlaying));
        }
    }


}

#endif