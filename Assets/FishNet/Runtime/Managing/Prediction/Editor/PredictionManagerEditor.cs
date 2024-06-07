#if !PREDICTION_1
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Managing.Predicting.Editing
{


    [CustomEditor(typeof(PredictionManager), true)]
    [CanEditMultipleObjects]
    public class PredictionManagerEditor : Editor
    {
       // private SerializedProperty _queuedInputs;
        private SerializedProperty _dropExcessiveReplicates;
        private SerializedProperty _maximumServerReplicates;
        private SerializedProperty _maximumConsumeCount;
        private SerializedProperty _stateInterpolation;
       // private SerializedProperty _serverInterpolation;

        protected virtual void OnEnable()
        {
            _dropExcessiveReplicates = serializedObject.FindProperty(nameof(_dropExcessiveReplicates));
            _maximumServerReplicates = serializedObject.FindProperty(nameof(_maximumServerReplicates));
            _maximumConsumeCount = serializedObject.FindProperty(nameof(_maximumConsumeCount));
            _stateInterpolation = serializedObject.FindProperty(nameof(_stateInterpolation));
           // _serverInterpolation = serializedObject.FindProperty(nameof(_serverInterpolation));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((PredictionManager)target), typeof(PredictionManager), false);
            GUI.enabled = true;


            EditorGUILayout.LabelField("Client", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (_stateInterpolation.intValue == 0)
                EditorGUILayout.HelpBox($"With interpolation set at 0 states will run as they are received, rather than create an interpolation buffer. Using 0 interpolation drastically increases the chance of Created states arriving out of order.", MessageType.Warning);
            EditorGUILayout.PropertyField(_stateInterpolation);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
           // EditorGUILayout.PropertyField(_serverInterpolation);
            EditorGUILayout.PropertyField(_dropExcessiveReplicates);
            EditorGUI.indentLevel++;
            if (_dropExcessiveReplicates.boolValue == true)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_maximumServerReplicates);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;


            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif


#else



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
        private SerializedProperty _redundancyCount;

        protected virtual void OnEnable()
        {
            _queuedInputs = serializedObject.FindProperty(nameof(_queuedInputs));
            _dropExcessiveReplicates = serializedObject.FindProperty(nameof(_dropExcessiveReplicates));
            _maximumServerReplicates = serializedObject.FindProperty(nameof(_maximumServerReplicates));
            _redundancyCount = serializedObject.FindProperty(nameof(_redundancyCount));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((PredictionManager)target), typeof(PredictionManager), false);
            GUI.enabled = true;


            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_redundancyCount);
            EditorGUILayout.PropertyField(_queuedInputs);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_dropExcessiveReplicates);
            EditorGUI.indentLevel++;
            if (_dropExcessiveReplicates.boolValue == true)
                EditorGUILayout.PropertyField(_maximumServerReplicates);
            EditorGUI.indentLevel--;


            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif


#endif