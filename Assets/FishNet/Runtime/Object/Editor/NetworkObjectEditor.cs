#if UNITY_EDITOR
using FishNet.Object.Prediction;
using UnityEditor;
using UnityEngine;

namespace FishNet.Object.Editing
{

    [CustomEditor(typeof(NetworkObject), true)]
    [CanEditMultipleObjects]
    public class NetworkObjectEditor : Editor
    {
        private SerializedProperty _isNetworked;
        private SerializedProperty _isSpawnable;
        private SerializedProperty _isGlobal;
        private SerializedProperty _initializeOrder;
        private SerializedProperty _defaultDespawnType;

        private SerializedProperty _enablePrediction;
        private SerializedProperty _enableStateForwarding;
        private SerializedProperty _networkTransform;
        private SerializedProperty _predictionType;
        private SerializedProperty _graphicalObject;
        private SerializedProperty _detachGraphicalObject;

        private SerializedProperty _ownerSmoothedProperties;
        private SerializedProperty _spectatorSmoothedProperties;
        private SerializedProperty _ownerInterpolation;
        private SerializedProperty _adaptiveInterpolation;
        private SerializedProperty _spectatorInterpolation;
        private SerializedProperty _enableTeleport;
        private SerializedProperty _teleportThreshold;



        protected virtual void OnEnable()
        {
            _isNetworked = serializedObject.FindProperty(nameof(_isNetworked));
            _isSpawnable = serializedObject.FindProperty(nameof(_isSpawnable));
            _isGlobal = serializedObject.FindProperty(nameof(_isGlobal));
            _initializeOrder = serializedObject.FindProperty(nameof(_initializeOrder));
            _defaultDespawnType = serializedObject.FindProperty(nameof(_defaultDespawnType));

            _enablePrediction = serializedObject.FindProperty(nameof(_enablePrediction));
            _enableStateForwarding = serializedObject.FindProperty(nameof(_enableStateForwarding));
            _networkTransform = serializedObject.FindProperty(nameof(_networkTransform));
            _predictionType = serializedObject.FindProperty(nameof(_predictionType));
            _graphicalObject = serializedObject.FindProperty(nameof(_graphicalObject));
            _detachGraphicalObject = serializedObject.FindProperty(nameof(_detachGraphicalObject));

            _ownerSmoothedProperties = serializedObject.FindProperty(nameof(_ownerSmoothedProperties));
            _ownerInterpolation = serializedObject.FindProperty(nameof(_ownerInterpolation));
            _adaptiveInterpolation = serializedObject.FindProperty(nameof(_adaptiveInterpolation));
            _spectatorSmoothedProperties = serializedObject.FindProperty(nameof(_spectatorSmoothedProperties));
            _spectatorInterpolation = serializedObject.FindProperty(nameof(_spectatorInterpolation));
            _enableTeleport = serializedObject.FindProperty(nameof(_enableTeleport));
            _teleportThreshold = serializedObject.FindProperty(nameof(_teleportThreshold));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NetworkObject nob = (NetworkObject)target;

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour(nob), typeof(NetworkObject), false);
            GUI.enabled = true;

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_isNetworked);
            EditorGUILayout.PropertyField(_isSpawnable);
            EditorGUILayout.PropertyField(_isGlobal);
            EditorGUILayout.PropertyField(_initializeOrder);
            EditorGUILayout.PropertyField(_defaultDespawnType);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Prediction", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_enablePrediction);
            if (_enablePrediction.boolValue == true)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_predictionType);
                EditorGUILayout.PropertyField(_enableStateForwarding);
                if (_enableStateForwarding.boolValue == false)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_networkTransform);
                    EditorGUI.indentLevel--;
                }

                bool graphicalSet = (_graphicalObject.objectReferenceValue != null);
                EditorGUILayout.PropertyField(_graphicalObject);
                if (graphicalSet)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_detachGraphicalObject);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.LabelField("Smoothing", EditorStyles.boldLabel);
                if (!graphicalSet)
                {
                    EditorGUILayout.HelpBox($"More smoothing settings will be displayed when a graphicalObject is set.", MessageType.Info);
                }
                else
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_enableTeleport);
                    if (_enableTeleport.boolValue == true)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(_teleportThreshold, new GUIContent("Teleport Threshold"));
                        EditorGUI.indentLevel--;
                    }                    

                    EditorGUILayout.LabelField("Owner", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_ownerInterpolation, new GUIContent("Interpolation"));
                    EditorGUILayout.PropertyField(_ownerSmoothedProperties, new GUIContent("Smoothed Properties"));
                    EditorGUI.indentLevel--;

                    EditorGUILayout.LabelField("Spectator", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_adaptiveInterpolation);
                    if (_adaptiveInterpolation.intValue == (int)AdaptiveInterpolationType.Off)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(_spectatorInterpolation, new GUIContent("Interpolation"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(_spectatorSmoothedProperties, new GUIContent("Smoothed Properties"));
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;


            serializedObject.ApplyModifiedProperties();
        }



    }

}

#endif