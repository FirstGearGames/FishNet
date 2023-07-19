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
        private SerializedProperty _isGlobal;
        private SerializedProperty _initializeOrder;
        private SerializedProperty _defaultDespawnType;

        private SerializedProperty _enablePrediction;
        private SerializedProperty _predictionType;
        private SerializedProperty _graphicalObject;
        private SerializedProperty _enableStateForwarding;

        private SerializedProperty _ownerInterpolation;
        private SerializedProperty _enableTeleport;
        private SerializedProperty _ownerTeleportThreshold;

        //private SerializedProperty _futurePredictionTime;
        private SerializedProperty _spectatorAdaptiveInterpolation;
        private SerializedProperty _spectatorInterpolation;
        private SerializedProperty _adaptiveSmoothingType;
        private SerializedProperty _customSmoothingData;
        private SerializedProperty _preconfiguredSmoothingDataPreview;


        protected virtual void OnEnable()
        {
            _isNetworked = serializedObject.FindProperty(nameof(_isNetworked));
            _isGlobal = serializedObject.FindProperty(nameof(_isGlobal));
            _initializeOrder = serializedObject.FindProperty(nameof(_initializeOrder));
            _defaultDespawnType = serializedObject.FindProperty(nameof(_defaultDespawnType));

            _enablePrediction = serializedObject.FindProperty(nameof(_enablePrediction));
            _predictionType = serializedObject.FindProperty(nameof(_predictionType));
            _graphicalObject = serializedObject.FindProperty(nameof(_graphicalObject));
            _enableStateForwarding = serializedObject.FindProperty(nameof(_enableStateForwarding));

            _ownerInterpolation = serializedObject.FindProperty(nameof(_ownerInterpolation));
            _enableTeleport = serializedObject.FindProperty(nameof(_enableTeleport));
            _ownerTeleportThreshold = serializedObject.FindProperty(nameof(_ownerTeleportThreshold));

            //_futurePredictionTime = serializedObject.FindProperty(nameof(_futurePredictionTime));
            _spectatorAdaptiveInterpolation = serializedObject.FindProperty(nameof(_spectatorAdaptiveInterpolation));
            _spectatorInterpolation = serializedObject.FindProperty(nameof(_spectatorInterpolation));
            _adaptiveSmoothingType = serializedObject.FindProperty(nameof(_adaptiveSmoothingType));
            _customSmoothingData = serializedObject.FindProperty(nameof(_customSmoothingData));
            _preconfiguredSmoothingDataPreview = serializedObject.FindProperty(nameof(_preconfiguredSmoothingDataPreview));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NetworkObject nob = (NetworkObject)target;

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour(nob), typeof(NetworkObject), false);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(_isNetworked);
            EditorGUILayout.PropertyField(_isGlobal);
            EditorGUILayout.PropertyField(_initializeOrder);
            EditorGUILayout.PropertyField(_defaultDespawnType);

            EditorGUILayout.PropertyField(_enablePrediction);
            if (_enablePrediction.boolValue == true)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_predictionType);
                EditorGUILayout.PropertyField(_graphicalObject);
                GUI.enabled = false;
                EditorGUILayout.PropertyField(_enableStateForwarding);
                GUI.enabled = true;
                EditorGUILayout.LabelField("Owner", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_ownerInterpolation, new GUIContent("Interpolation"));
                EditorGUILayout.PropertyField(_enableTeleport);
                if (_enableTeleport.boolValue == true)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_ownerTeleportThreshold, new GUIContent("Teleport Threshold"));
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.LabelField("Spectator", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                GUI.enabled = false;
                EditorGUILayout.PropertyField(_spectatorAdaptiveInterpolation, new GUIContent("Adaptive Interpolation"));
                GUI.enabled = true;
                //if (_futurePredictionTime.floatValue <= 0f)
                if (_spectatorAdaptiveInterpolation.boolValue == false)
                {
                    EditorGUILayout.PropertyField(_spectatorInterpolation, new GUIContent("Interpolation"));
                }
                else
                {
                    EditorGUILayout.PropertyField(_adaptiveSmoothingType);
                    EditorGUI.indentLevel++;
                    if (_adaptiveSmoothingType.intValue == (int)AdaptiveSmoothingType.Custom)
                    {
                        EditorGUILayout.PropertyField(_customSmoothingData);
                    }
                    else
                    {
                        GUI.enabled = false;
                        EditorGUILayout.PropertyField(_preconfiguredSmoothingDataPreview, new GUIContent("Preconfigured Smoothing Data"));
                        GUI.enabled = true;
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();


            serializedObject.ApplyModifiedProperties();
        }



    }

}


#endif
#endif