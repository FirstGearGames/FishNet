#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishNet.Component.Animating
{

    [CustomEditor(typeof(NetworkAnimator), true)]
    [CanEditMultipleObjects]
    public class FlexNetworkAnimatorEditor : Editor
    {
        private SerializedProperty _animator;
        private SerializedProperty _synchronizeInterval;
        private SerializedProperty _smoothFloats;
        private SerializedProperty _clientAuthoritative;
        private SerializedProperty _sendToOwner;
        private SerializedProperty _ignoredParameters;

        protected virtual void OnEnable()
        {
            _animator = serializedObject.FindProperty("_animator");

            _synchronizeInterval = serializedObject.FindProperty("_synchronizeInterval");
            _smoothFloats = serializedObject.FindProperty("_smoothFloats");

            _clientAuthoritative = serializedObject.FindProperty("_clientAuthoritative");
            _sendToOwner = serializedObject.FindProperty("_sendToOwner");

            _ignoredParameters = serializedObject.FindProperty("_ignoredParameters");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NetworkAnimator na = (NetworkAnimator)target;

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour(na), typeof(NetworkAnimator), false);
            GUI.enabled = true;

            //Animator
            EditorGUILayout.LabelField("Animator", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_animator, new GUIContent("Animator", "The animator component to synchronize."));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            //Synchronization Processing.
            EditorGUILayout.LabelField("Synchronization Processing", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_synchronizeInterval, new GUIContent("Synchronize Interval", "How often to synchronize this animator."));
            EditorGUILayout.PropertyField(_smoothFloats, new GUIContent("Smooth Floats", "True to smooth floats on spectators rather than snap to their values immediately. Commonly set to true for smooth blend tree animations."));
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            //Authority.
            EditorGUILayout.LabelField("Authority", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_clientAuthoritative, new GUIContent("Client Authoritative", "True if using client authoritative movement."));
            if (_clientAuthoritative.boolValue == false)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_sendToOwner, new GUIContent("Synchronize To Owner", "True to synchronize server results back to owner. Typically used when you are sending inputs to the server and are relying on the server response to move the transform."));
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            DrawParameters(na);

            serializedObject.ApplyModifiedProperties();
        }


        private void DrawParameters(NetworkAnimator na)
        {
            return;
            Animator animator = na.Animator;
            if (animator == null)
                return;
            if (animator.runtimeAnimatorController == null)
                return;

            //Create a parameter detail for each parameter that can be synchronized.
            foreach (AnimatorControllerParameter item in animator.parameters)
            {
                int count = 0;
                if (!animator.IsParameterControlledByCurve(item.name))
                {
                    count++;
                    //Over 250 parameters; who would do this!?
                    if (count >= 240)
                    {
                        Debug.LogError($"Parameter {item.name} exceeds the allowed 240 parameter count and is being ignored.");
                        continue;
                    }

                    string parameterName = item.name;
                }
            }
        }
    }

}
#endif