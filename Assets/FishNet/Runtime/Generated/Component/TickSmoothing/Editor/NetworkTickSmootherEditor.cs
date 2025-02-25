#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Transforming.Beta
{


    [CustomEditor(typeof(NetworkTickSmoother), true)]
    [CanEditMultipleObjects]
    public class NetworkTickSmootherEditor : Editor
    {
        private SerializedProperty _initializationSettings;
        private SerializedProperty _controllerMovementSettings;
        private SerializedProperty _spectatorMovementSettings;
        
        private bool _showOwnerSmoothingSettings;
        private bool _showSpectatorSmoothingSettings;
        
        protected virtual void OnEnable()
        {
            _initializationSettings = serializedObject.FindProperty(nameof(_initializationSettings));
            _controllerMovementSettings = serializedObject.FindProperty(nameof(_controllerMovementSettings));
            _spectatorMovementSettings = serializedObject.FindProperty(nameof(_spectatorMovementSettings));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((NetworkTickSmoother)target), typeof(NetworkTickSmoother), false);
            GUI.enabled = true;

            //EditorGUILayout.LabelField("Initialization Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(_initializationSettings);
            
            _showOwnerSmoothingSettings = EditorGUILayout.Foldout(_showOwnerSmoothingSettings, "Owner Smoothing");
            if (_showOwnerSmoothingSettings)
                EditorGUILayout.PropertyField(_controllerMovementSettings);

            _showSpectatorSmoothingSettings = EditorGUILayout.Foldout(_showSpectatorSmoothingSettings, "Spectator Smoothing");
            if (_showSpectatorSmoothingSettings)
                EditorGUILayout.PropertyField(_spectatorMovementSettings);
            
            
            //EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif