#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Managing.Observing.Editing
{


    [CustomEditor(typeof(ObserverManager), true)]
    [CanEditMultipleObjects]
    public class ObserverManagerEditor : Editor
    {
        private SerializedProperty _useNetworkLod;
        private SerializedProperty _levelOfDetailDistances;
        private SerializedProperty _updateHostVisibility;
        private SerializedProperty _defaultConditions;

        protected virtual void OnEnable()
        {
            _useNetworkLod = serializedObject.FindProperty(nameof(_useNetworkLod));
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


            EditorGUILayout.PropertyField(_useNetworkLod);
            if (_useNetworkLod.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_levelOfDetailDistances);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(_updateHostVisibility);
            EditorGUILayout.PropertyField(_defaultConditions);

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif