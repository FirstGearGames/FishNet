#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Transforming.Editing
{


    [CustomEditor(typeof(NetworkTransform), true)]
    [CanEditMultipleObjects]
    public class NetworkTransformEditor : Editor
    {
        private SerializedProperty _enableTeleport;
        private SerializedProperty _clientAuthoritative;

        protected virtual void OnEnable()
        {
            _enableTeleport = serializedObject.FindProperty("_enableTeleport");
            _clientAuthoritative = serializedObject.FindProperty("_clientAuthoritative");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

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
                //Teleporting.
                else if (p.name == "_teleportThreshold")
                {
                    if (_enableTeleport.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(p);
                        EditorGUI.indentLevel--;
                    }
                }
                //Client authoritative
                else if (p.name == "_sendToOwner")
                {
                    if (!_clientAuthoritative.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(p);
                        EditorGUI.indentLevel--;
                    }
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