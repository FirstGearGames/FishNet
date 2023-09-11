#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Managing.Predicting.Editing
{


    [CustomEditor(typeof(PredictionManager), true)]
    [CanEditMultipleObjects]
    public class PredictionManagerEditor : Editor
    {
        private SerializedProperty _queuedInputs;
        private SerializedProperty _dropExcessiveReplicates;
        private SerializedProperty _maximumServerReplicates;
        private SerializedProperty _maximumConsumeCount;
        private SerializedProperty _redundancyCount;
        private SerializedProperty _allowPredictedSpawning;
        private SerializedProperty _reservedObjectIds;


        protected virtual void OnEnable()
        {
            _queuedInputs = serializedObject.FindProperty(nameof(_queuedInputs));
            _dropExcessiveReplicates = serializedObject.FindProperty(nameof(_dropExcessiveReplicates));
            _maximumServerReplicates = serializedObject.FindProperty(nameof(_maximumServerReplicates));
            _maximumConsumeCount = serializedObject.FindProperty(nameof(_maximumConsumeCount));
            _redundancyCount = serializedObject.FindProperty(nameof(_redundancyCount));
            _allowPredictedSpawning = serializedObject.FindProperty(nameof(_allowPredictedSpawning));
            _reservedObjectIds = serializedObject.FindProperty(nameof(_reservedObjectIds));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((PredictionManager)target), typeof(PredictionManager), false);
            GUI.enabled = true;

            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_queuedInputs);
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

            EditorGUILayout.PropertyField(_allowPredictedSpawning);
            if (_allowPredictedSpawning.boolValue == true)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_reservedObjectIds);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif