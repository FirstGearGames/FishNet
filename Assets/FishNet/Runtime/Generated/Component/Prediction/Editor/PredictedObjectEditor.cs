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
        private SerializedProperty _smoothTicks;
        private SerializedProperty _durationType;
        private SerializedProperty _smoothingDuration;
        private SerializedProperty _enableTeleport;
        private SerializedProperty _teleportThreshold;
        private SerializedProperty _predictionType;
        private SerializedProperty _rigidbody;
        private SerializedProperty _rigidbody2d;
        private SerializedProperty _networkTransform;
        private SerializedProperty _predictionRatio;

        protected virtual void OnEnable()
        {
            _graphicalObject = serializedObject.FindProperty("_graphicalObject");
            _smoothTicks = serializedObject.FindProperty("_smoothTicks");
            _durationType = serializedObject.FindProperty("_durationType");
            _smoothingDuration = serializedObject.FindProperty("_smoothingDuration");
            _enableTeleport = serializedObject.FindProperty("_enableTeleport");
            _teleportThreshold = serializedObject.FindProperty("_teleportThreshold");
            _predictionType = serializedObject.FindProperty("_predictionType");
            _rigidbody = serializedObject.FindProperty("_rigidbody");
            _rigidbody2d = serializedObject.FindProperty("_rigidbody2d");
            _networkTransform = serializedObject.FindProperty("_networkTransform");
            _predictionRatio = serializedObject.FindProperty("_predictionRatio");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((PredictedObject)target), typeof(PredictedObject), false);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(_graphicalObject);
            EditorGUILayout.PropertyField(_smoothTicks);
            EditorGUILayout.PropertyField(_durationType);
            if ((PredictedObject.SmoothingDurationType)_durationType.intValue == PredictedObject.SmoothingDurationType.Time)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_smoothingDuration);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_enableTeleport);
            if (_enableTeleport.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_teleportThreshold);
                EditorGUI.indentLevel--;
            }

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
                EditorGUILayout.PropertyField(_predictionRatio);
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

