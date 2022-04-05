#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Observing
{

    namespace FishNet.Component.Prediction.Editing
    {
        [CustomEditor(typeof(MatchCondition), true)]
        [CanEditMultipleObjects]
        public class MatchConditionEditor : Editor
        {
            public override void OnInspectorGUI()
            {

                EditorGUILayout.HelpBox("This component is experimental. Documentation may not yet be available.", MessageType.Warning);
                base.OnInspectorGUI();
            }
        }
    }

}

#endif