#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Managing.Timing.Editing
{


    [CustomEditor(typeof(TimeManager), true)]
    [CanEditMultipleObjects]
    public class TimeManagerEditor : Editor
    {
        private SerializedProperty _tickRate;
        private SerializedProperty _pingInterval;
        private SerializedProperty _timingInterval;
        private SerializedProperty _physicsMode;
        private SerializedProperty _maximumBufferedInputs;

        protected virtual void OnEnable()
        {
            _tickRate = serializedObject.FindProperty("_tickRate");
            _pingInterval = serializedObject.FindProperty("_pingInterval");
            _timingInterval = serializedObject.FindProperty("_timingInterval");
            _physicsMode = serializedObject.FindProperty("_physicsMode");
            _maximumBufferedInputs = serializedObject.FindProperty("_maximumBufferedInputs");
        }

        public override void OnInspectorGUI()
        {

            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((TimeManager)target), typeof(TimeManager), false);
            GUI.enabled = true;

            //Timing.
            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_tickRate);
            EditorGUILayout.PropertyField(_pingInterval);
            EditorGUILayout.PropertyField(_timingInterval);            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            //Physics.
            EditorGUILayout.LabelField("Physics", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_physicsMode);
            if (_physicsMode.intValue != (int)FishNet.Managing.Timing.PhysicsMode.TimeManager)
                EditorGUILayout.HelpBox("If you are using physics interactions be sure to change the PhysicsMode to TimeManager and implement physics within the TimeManager tick events.", MessageType.None);
            EditorGUI.indentLevel--;

            //Prediction.
            EditorGUILayout.LabelField("Prediction", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_maximumBufferedInputs);
            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }
    }

}
#endif