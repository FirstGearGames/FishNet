#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Managing.Server.Editing
{


    [CustomEditor(typeof(ServerManager), true)]
    [CanEditMultipleObjects]
    public class ServerManagerEditor : Editor
    {
        private SerializedProperty _limitClientMTU;

        protected virtual void OnEnable()
        {
            _limitClientMTU = serializedObject.FindProperty("_limitClientMTU");
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
                //If belongs to limit client mtu.
                else if (p.name == "_maximumClientMTU")
                {
                    if (!_limitClientMTU.boolValue)
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