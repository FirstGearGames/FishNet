#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using GameKitEditing = GameKit.Utilities.Editing;

namespace FishNet.Managing.Observing.Editing
{


    [CustomEditor(typeof(ObserverManager), true)]
    [CanEditMultipleObjects]
    public class ObserverManagerEditor : Editor
    {
        private SerializedProperty _enableNetworkLod;
        private SerializedProperty _levelOfDetailDistances;
        private SerializedProperty _updateHostVisibility;
        private SerializedProperty _defaultConditions;

        protected virtual void OnEnable()
        {
            _enableNetworkLod = serializedObject.FindProperty(nameof(_enableNetworkLod));
            _levelOfDetailDistances = serializedObject.FindProperty(nameof(_levelOfDetailDistances));
            _updateHostVisibility = serializedObject.FindProperty(nameof(_updateHostVisibility));
            _defaultConditions = serializedObject.FindProperty(nameof(_defaultConditions));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((ObserverManager)target), typeof(ObserverManager), false);
            GUI.enabled = true;

            GameKitEditing.DisableGUIIfPlaying();
            EditorGUILayout.PropertyField(_enableNetworkLod);
            if (_enableNetworkLod.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_levelOfDetailDistances);
                EditorGUI.indentLevel--;
            }
            GameKitEditing.EnableGUIIfPlaying();

            EditorGUILayout.PropertyField(_updateHostVisibility);
            EditorGUILayout.PropertyField(_defaultConditions);

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif