//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;

//namespace FishNet.Managing.Timing.Editing
//{


//    [CustomEditor(typeof(TimeManager), true)]
//    [CanEditMultipleObjects]
//    public class TimeManagerEditor : Editor
//    {
//        private SerializedProperty _correctTiming;

//        protected virtual void OnEnable()
//        {
//            _correctTiming = serializedObject.FindProperty("_correctTiming");
//        }

//        public override void OnInspectorGUI()
//        {

//            serializedObject.Update();


//            SerializedProperty p = serializedObject.GetIterator();
//            do
//            {
//                if (p.name == "Base")
//                { 
//                    continue;
//                }
//                //Script reference.
//                else if (p.name == "m_Script")
//                {
//                    GUI.enabled = false;
//                    EditorGUILayout.PropertyField(p);
//                    GUI.enabled = true;
//                }
//                //CSP related.
//                else if (p.name == "_maximumBufferedInputs" ||
//                    p.name == "_targetBufferedInputs" ||
//                    p.name == "_aggressiveTiming")
//                {
//                    if (_correctTiming.boolValue)
//                    {
//                        EditorGUI.indentLevel++;
//                        EditorGUILayout.PropertyField(p);
//                        EditorGUI.indentLevel--;
//                    }
//                }
//                else
//                {
//                    EditorGUILayout.PropertyField(p);
//                }
//            }
//            while (p.NextVisible(true));

//                serializedObject.ApplyModifiedProperties();
//        }
//    }

//}
//#endif