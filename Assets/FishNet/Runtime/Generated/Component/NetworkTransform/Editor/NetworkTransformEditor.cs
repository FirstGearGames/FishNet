#if UNITY_EDITOR
using FishNet.Editing;
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Transforming.Editing
{


    [CustomEditor(typeof(NetworkTransform), true)]
    [CanEditMultipleObjects]
    public class NetworkTransformEditor : Editor
    {
        private SerializedProperty _compress;
        private SerializedProperty _synchronizeParent;
        private SerializedProperty _interpolation;
        private SerializedProperty _extrapolation;
        private SerializedProperty _enableTeleport;
        private SerializedProperty _teleportThreshold;
        private SerializedProperty _clientAuthoritative;
        private SerializedProperty _sendToOwner;
        private SerializedProperty _positionSnapping;
        private SerializedProperty _rotationSnapping;
        private SerializedProperty _scaleSnapping;


        protected virtual void OnEnable()
        {
            _compress = serializedObject.FindProperty("_compress");
            _synchronizeParent = serializedObject.FindProperty("_synchronizeParent");
            _interpolation = serializedObject.FindProperty("_interpolation");
            _extrapolation = serializedObject.FindProperty("_extrapolation");
            _enableTeleport = serializedObject.FindProperty("_enableTeleport");
            _teleportThreshold = serializedObject.FindProperty("_teleportThreshold");
            _clientAuthoritative = serializedObject.FindProperty("_clientAuthoritative");
            _sendToOwner = serializedObject.FindProperty("_sendToOwner");
            _positionSnapping = serializedObject.FindProperty("_positionSnapping");
            _rotationSnapping = serializedObject.FindProperty("_rotationSnapping");
            _scaleSnapping = serializedObject.FindProperty("_scaleSnapping");
        }

        public override void OnInspectorGUI()
        {

            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour((NetworkTransform)target), typeof(NetworkTransform), false);
            GUI.enabled = true;

            EditorGUILayout.HelpBox(EditingConstants.PRO_FEATURE_MESSAGE, MessageType.Info);
            
            //Misc.
            EditorGUILayout.LabelField("Misc", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_compress);
            EditorGUILayout.PropertyField(_synchronizeParent, new GUIContent("* Synchronize Parent"));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            //Smoothing.
            EditorGUILayout.LabelField("Smoothing", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_interpolation);
            EditorGUILayout.PropertyField(_extrapolation, new GUIContent("* Extrapolation"));
            EditorGUILayout.PropertyField(_enableTeleport);
            if (_enableTeleport.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_teleportThreshold);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            //Authority.
            EditorGUILayout.LabelField("Authority", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_clientAuthoritative);
            if (!_clientAuthoritative.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_sendToOwner);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            //Snapping.
            EditorGUILayout.LabelField("Snapping.", EditorStyles.boldLabel);
            EditorGUI.indentLevel += 2;
            EditorGUILayout.PropertyField(_positionSnapping);
            EditorGUILayout.PropertyField(_rotationSnapping);
            EditorGUILayout.PropertyField(_scaleSnapping);
            EditorGUI.indentLevel -= 2;

            serializedObject.ApplyModifiedProperties();
        }
    }

}
#endif