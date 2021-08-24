#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Managing.Timing.Editing
{


    [CustomEditor(typeof(TimeManager), true)]
    [CanEditMultipleObjects]
    public class TimeManagerEditor : Editor
    {
        private SerializedProperty _automaticPhysics;
        private SerializedProperty _tickRate;

        private SerializedProperty _useClientSidePrediction;
        private SerializedProperty _maximumBufferedInputs;
        private SerializedProperty _simulationSyncInterval;

        protected virtual void OnEnable()
        {
            _automaticPhysics = serializedObject.FindProperty("_automaticPhysics");
            _tickRate = serializedObject.FindProperty("_tickRate");

            _useClientSidePrediction = serializedObject.FindProperty("_useClientSidePrediction");
            _maximumBufferedInputs = serializedObject.FindProperty("_maximumBufferedInputs");
            _simulationSyncInterval = serializedObject.FindProperty("_simulationSyncInterval");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((TimeManager)target), typeof(TimeManager), false);
            GUI.enabled = true;

            //Animator
            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_automaticPhysics, new GUIContent("Automatic Physics", "True to let Unity run physics. False to let TimeManager run physics after each tick."));
            EditorGUILayout.PropertyField(_tickRate, new GUIContent("Tick Rate", "How many times per second the server will simulate"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            //Synchronization Processing.
            EditorGUILayout.LabelField("Client Side Prediction", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_useClientSidePrediction, new GUIContent("Enabled", "True to enable support for client side prediction. Leaving this false when CSP is not needed will save a small amount of bandwidth and CPU."));
            if (_useClientSidePrediction.boolValue == true)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_maximumBufferedInputs, new GUIContent("Maximum Buffered Inputs", "Maximum number of excessive input sent from client before entries are dropped. Client is expected to send roughly one input per server tick."));
                EditorGUILayout.PropertyField(_simulationSyncInterval, new GUIContent("Simulation Sync Interval", "How many ticks to wait between each step change. Lower this value for more precise tick synchronization between client and server at the cost of more bandwidth."));
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
        }
    }

}
#endif