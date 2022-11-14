#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Prediction
{


    [CustomEditor(typeof(PredictedObject), true)]
    [CanEditMultipleObjects]
    public class PredictedObjectEditor : Editor
    {
        private SerializedProperty _graphicalObject;
        private SerializedProperty _ownerInterpolation;
        private SerializedProperty _enableTeleport;
        private SerializedProperty _teleportThreshold;
        private SerializedProperty _predictionType;

        private SerializedProperty _rigidbody;
        private SerializedProperty _rigidbody2d;
        private SerializedProperty _spectatorSmoothPosition;
        private SerializedProperty _spectatorSmoothRotation;
        private SerializedProperty _spectatorSmoothingDuration;
        private SerializedProperty _spectatorInterpolation;
        private SerializedProperty _overflowMultiplier;
        private SerializedProperty _maintainedVelocity;
        private SerializedProperty _resendType;
        private SerializedProperty _resendInterval;

        private SerializedProperty _networkTransform;

        protected virtual void OnEnable()
        {
            _graphicalObject = serializedObject.FindProperty("_graphicalObject");
            _ownerInterpolation = serializedObject.FindProperty("_ownerInterpolation");
            _enableTeleport = serializedObject.FindProperty("_enableTeleport");
            _teleportThreshold = serializedObject.FindProperty("_teleportThreshold");
            _predictionType = serializedObject.FindProperty("_predictionType");

            _rigidbody = serializedObject.FindProperty("_rigidbody");
            _rigidbody2d = serializedObject.FindProperty("_rigidbody2d");
            _spectatorSmoothPosition = serializedObject.FindProperty("_spectatorSmoothPosition");
            _spectatorSmoothRotation = serializedObject.FindProperty("_spectatorSmoothRotation");
            _spectatorSmoothingDuration = serializedObject.FindProperty("_spectatorSmoothingDuration");
            _spectatorInterpolation = serializedObject.FindProperty("_spectatorInterpolation");
            _overflowMultiplier = serializedObject.FindProperty("_overflowMultiplier");
            _maintainedVelocity = serializedObject.FindProperty("_maintainedVelocity");
            _resendType = serializedObject.FindProperty("_resendType");
            _resendInterval = serializedObject.FindProperty("_resendInterval");

            _networkTransform = serializedObject.FindProperty("_networkTransform");

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((PredictedObject)target), typeof(PredictedObject), false);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(_graphicalObject);
            EditorGUILayout.PropertyField(_enableTeleport);
            if (_enableTeleport.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_teleportThreshold);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.LabelField("Owner Settings");
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_ownerInterpolation, new GUIContent("Interpolation"));
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(_predictionType);
            PredictedObject.PredictionType movementType = (PredictedObject.PredictionType)_predictionType.intValue;
            if (movementType != PredictedObject.PredictionType.Other)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("When using physics prediction do not include a NetworkTransform; this component will synchronize instead.", MessageType.Info);
                if (movementType == PredictedObject.PredictionType.Rigidbody)
                    EditorGUILayout.PropertyField(_rigidbody);
                else
                    EditorGUILayout.PropertyField(_rigidbody2d, new GUIContent("Rigidbody2D", "Rigidbody2D to predict."));

                EditorGUILayout.LabelField("Spectator Settings");
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Smoothing");
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_spectatorSmoothPosition, new GUIContent("Smooth Position"));
                EditorGUILayout.PropertyField(_spectatorSmoothRotation, new GUIContent("Smooth Rotation"));
                EditorGUILayout.PropertyField(_spectatorSmoothingDuration, new GUIContent("Smoothing Duration"));
                EditorGUILayout.PropertyField(_spectatorInterpolation, new GUIContent("Interpolation"));
                EditorGUILayout.PropertyField(_overflowMultiplier);
                EditorGUI.indentLevel--;
                EditorGUILayout.PropertyField(_maintainedVelocity);

                EditorGUILayout.PropertyField(_resendType);
                PredictedObject.ResendType resendType = (PredictedObject.ResendType)_resendType.intValue;
                if (resendType == PredictedObject.ResendType.Interval)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_resendInterval, new GUIContent("Interval"));
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;

                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("When other is selected another component, such as NetworkTransform, must be used to synchronize.", MessageType.Info);
                EditorGUILayout.PropertyField(_networkTransform);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif

