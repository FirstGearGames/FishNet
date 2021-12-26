#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Prediction.Editing
{
    [CustomEditor(typeof(PredictedRigidbody), true)]
    [CanEditMultipleObjects]
    public class PredictedRigidbodyEditor : Editor
    {
        public override void OnInspectorGUI()
        {

            serializedObject.Update();

            EditorGUILayout.HelpBox("This component is still in development and may not behave properly.", MessageType.Warning);

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