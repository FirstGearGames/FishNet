#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using static FishNet.Component.ColliderRollback.ColliderRollback;

namespace FishNet.Component.ColliderRollback
{

    [CustomEditor(typeof(ColliderRollback), true)]
    [CanEditMultipleObjects]
    public class ColliderRollbackEditor : Editor
    {
        private SerializedProperty _boundingBox;
        private SerializedProperty _physicsType;
        private SerializedProperty _boundingBoxSize;
        private SerializedProperty _colliderParents;


        protected virtual void OnEnable()
        {
            _boundingBox = serializedObject.FindProperty(nameof(_boundingBox));
            _physicsType = serializedObject.FindProperty(nameof(_physicsType));
            _boundingBoxSize = serializedObject.FindProperty(nameof(_boundingBoxSize));
            _colliderParents = serializedObject.FindProperty(nameof(_colliderParents));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ColliderRollback nob = (ColliderRollback)target;

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour(nob), typeof(ColliderRollback), false);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(_boundingBox, new GUIContent("Bounding Box (experimental)"));
            if ((BoundingBoxType)_boundingBox.intValue != BoundingBoxType.Disabled)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_physicsType);
                EditorGUILayout.PropertyField(_boundingBoxSize);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_colliderParents);

            serializedObject.ApplyModifiedProperties();
        }

    }

}


#endif