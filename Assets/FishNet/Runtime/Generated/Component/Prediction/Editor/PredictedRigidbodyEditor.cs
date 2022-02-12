#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Prediction.Editing
{
    [CustomEditor(typeof(PredictedRigidbodyBase), true)]
    [CanEditMultipleObjects]
    public class PredictedRigidbodyBaseEditor : Editor
    {
        public override void OnInspectorGUI()
        {

            serializedObject.Update();

            EditorGUILayout.HelpBox("Place this component on your objects which hold visuals. Visuals should be a child of your rigidbody object.", MessageType.Info);

            SerializedProperty p = serializedObject.GetIterator();
            do
            {
                if (p.name == "Base")
                {
                    continue;
                }
                //Script reference.
                else if (p.name == "m_Script")
                {
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(p);
                    GUI.enabled = true;
                }
                else
                {
                    EditorGUILayout.PropertyField(p);
                }
            }
            while (p.NextVisible(true));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif