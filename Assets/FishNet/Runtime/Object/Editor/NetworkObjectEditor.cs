#if UNITY_EDITOR
#if PREDICTION_V2
using FishNet.Editing;
using FishNet.Object;
using FishNet.Object.Prediction;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
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
        private SerializedProperty _predictionType;
        private SerializedProperty _graphicalObject;
        private SerializedProperty _ownerInterpolation;
        private SerializedProperty _enableTeleport;
        private SerializedProperty _teleportThreshold;
        private SerializedProperty _enableStateForwarding;      
        

        protected virtual void OnEnable()
        {
            _isNetworked = serializedObject.FindProperty(nameof(_isNetworked));
            _isSpawnable = serializedObject.FindProperty(nameof(_isSpawnable));
            _isGlobal = serializedObject.FindProperty(nameof(_isGlobal));
            _initializeOrder = serializedObject.FindProperty(nameof(_initializeOrder));
            _defaultDespawnType = serializedObject.FindProperty(nameof(_defaultDespawnType));

            _enablePrediction = serializedObject.FindProperty(nameof(_enablePrediction));
            _predictionType = serializedObject.FindProperty(nameof(_predictionType));
            _graphicalObject = serializedObject.FindProperty(nameof(_graphicalObject));
            _enableTeleport = serializedObject.FindProperty(nameof(_enableTeleport));
            _teleportThreshold = serializedObject.FindProperty(nameof(_teleportThreshold));
            _enableStateForwarding = serializedObject.FindProperty(nameof(_enableStateForwarding));

            _ownerInterpolation = serializedObject.FindProperty(nameof(_ownerInterpolation));
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
                GUI.enabled = false;
                EditorGUILayout.PropertyField(_enableStateForwarding);
                GUI.enabled = true;
                //EditorGUILayout.LabelField("Owner", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_graphicalObject);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_ownerInterpolation, new GUIContent("Interpolation"));
                EditorGUILayout.PropertyField(_enableTeleport);
                if (_enableTeleport.boolValue == true)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_teleportThreshold, new GUIContent("Teleport Threshold"));
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;


            serializedObject.ApplyModifiedProperties();
        }



    }

}


#endif
#endif