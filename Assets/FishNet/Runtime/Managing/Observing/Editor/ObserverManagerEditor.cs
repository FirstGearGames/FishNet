#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Managing.Observing.Editing
{
    [CustomEditor(typeof(ObserverManager), true)]
    [CanEditMultipleObjects]
    public class ObserverManagerEditor : Editor
    {
        private SerializedProperty _updateHostVisibility;
        private SerializedProperty _maximumTimedObserversDuration;
        private SerializedProperty _defaultConditions;

        protected virtual void OnEnable()
        {
            _updateHostVisibility = serializedObject.FindProperty(nameof(_updateHostVisibility));
            _maximumTimedObserversDuration = serializedObject.FindProperty(nameof(_maximumTimedObserversDuration));
            _defaultConditions = serializedObject.FindProperty(nameof(_defaultConditions));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((ObserverManager)target), typeof(ObserverManager), false);
            GUI.enabled = true;

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(_updateHostVisibility);
            if (_maximumTimedObserversDuration.floatValue < 1d)
                EditorGUILayout.HelpBox("Using low values may reduce server performance while under load.", MessageType.Warning);
            EditorGUILayout.PropertyField(_maximumTimedObserversDuration);
            EditorGUILayout.PropertyField(_defaultConditions);

            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif