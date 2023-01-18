#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Managing.Predicting.Editing
{


    [CustomEditor(typeof(PredictionManager), true)]
    [CanEditMultipleObjects]
    public class PredictionManagerEditor : Editor
    {
        private SerializedProperty _dropExcessiveReplicates;
        private SerializedProperty _maximumServerReplicates;
        private SerializedProperty _maximumConsumeCount;

        private SerializedProperty _redundancyCount;

        protected virtual void OnEnable()
        {
            _dropExcessiveReplicates = serializedObject.FindProperty("_dropExcessiveReplicates");
            _maximumServerReplicates = serializedObject.FindProperty("_maximumServerReplicates");
            _maximumConsumeCount = serializedObject.FindProperty("_maximumConsumeCount");
            _redundancyCount = serializedObject.FindProperty("_redundancyCount");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((PredictionManager)target), typeof(PredictionManager), false);
            GUI.enabled = true;

            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_dropExcessiveReplicates);
            EditorGUI.indentLevel++;
            if (_dropExcessiveReplicates.boolValue == true)
            {
                EditorGUILayout.PropertyField(_maximumServerReplicates);
            }
            else
            {
                EditorGUILayout.PropertyField(_maximumConsumeCount);
            }
            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Client", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_redundancyCount);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif