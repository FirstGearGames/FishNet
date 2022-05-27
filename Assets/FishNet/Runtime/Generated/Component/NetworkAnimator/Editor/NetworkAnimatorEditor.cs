#if UNITY_EDITOR
using FishNet.Editing;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FishNet.Component.Animating.Editing
{

    [CustomEditor(typeof(NetworkAnimator), true)]
    [CanEditMultipleObjects]
    public class NetworkAnimatorEditor : Editor
    {
        private SerializedProperty _animator;
        private SerializedProperty _synchronizeInterval;
        private SerializedProperty _smoothFloats;
        private SerializedProperty _clientAuthoritative;
        private SerializedProperty _sendToOwner;
        private RuntimeAnimatorController _lastRuntimeAnimatorController;
        private AnimatorController _lastAnimatorController;

        protected virtual void OnEnable()
        {
            _animator = serializedObject.FindProperty("_animator");

            _synchronizeInterval = serializedObject.FindProperty("_synchronizeInterval");
            _smoothFloats = serializedObject.FindProperty("_smoothFloats");

            _clientAuthoritative = serializedObject.FindProperty("_clientAuthoritative");
            _sendToOwner = serializedObject.FindProperty("_sendToOwner");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NetworkAnimator na = (NetworkAnimator)target;

            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour(na), typeof(NetworkAnimator), false);
            GUI.enabled = true;

            
#pragma warning disable CS0162 // Unreachable code detected
                EditorGUILayout.HelpBox(EditingConstants.PRO_ASSETS_LOCKED_TEXT, MessageType.Warning);
#pragma warning restore CS0162 // Unreachable code detected

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
            EditorGUILayout.LabelField("* Synchronized Parameters", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("This feature can only be configured while out of play mode.", MessageType.Info);
                return;
            }
            if (na == null)
                return;
            Animator animator = na.Animator;
            if (animator == null)
                return;

            RuntimeAnimatorController runtimeController = (animator.runtimeAnimatorController is AnimatorOverrideController aoc) ? aoc.runtimeAnimatorController : animator.runtimeAnimatorController;
            if (runtimeController == null)
            {
                na.IgnoredParameters.Clear();
                return;
            }

            /* If runtime controller changed 
             * or editor controller is null
             * then get new editor controller. */
            if (runtimeController != _lastRuntimeAnimatorController || _lastAnimatorController == null)
                _lastAnimatorController = (AnimatorController)AssetDatabase.LoadAssetAtPath(AssetDatabase.GetAssetPath(runtimeController), typeof(AnimatorController));
            _lastRuntimeAnimatorController = runtimeController;

            Color defaultColor = GUI.backgroundColor;
            float width = Screen.width;
            float spacePerEntry = 125f;
            //Buttons seem to be longer than spacePerEntry. Why, because who knows...
            float extraSpaceJustBecause = 60;
            float spacer = 20f;
            width -= spacer;
            int entriesPerWidth = Mathf.Max(1, Mathf.FloorToInt(width / (spacePerEntry + extraSpaceJustBecause)));

            List<AnimatorControllerParameter> aps = new List<AnimatorControllerParameter>();
            //Create a parameter detail for each parameter that can be synchronized.
            int count = 0;
            foreach (AnimatorControllerParameter item in _lastAnimatorController.parameters)
            {
                count++;
                //Over 240 parameters; who would do this!?
                if (count >= 240)
                    continue;

                aps.Add(item);
            }

            int apsCount = aps.Count;
            for (int i = 0; i < apsCount; i++)
            {
                using (GUILayout.HorizontalScope hs = new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(spacer);
                    int z = 0;
                    while (z < entriesPerWidth && (z + i < apsCount))
                    {
                        //If this z+i would exceed entries then break.
                        if (z + i >= apsCount)
                            break;

                        AnimatorControllerParameter item = aps[i + z];
                        string parameterName = item.name;
                        bool ignored = na.IgnoredParameters.Contains(parameterName);

                        Color c = (ignored) ? Color.gray : Color.green;
                        GUI.backgroundColor = c;
                        if (GUILayout.Button(item.name, GUILayout.Width(spacePerEntry)))
                        {
                            if (Application.isPlaying)
                            {
                                Debug.Log("Synchronized parameters may not be changed while playing.");
                            }
                            else
                            {
                                if (ignored)
                                    na.IgnoredParameters.Remove(parameterName);
                                else
                                    na.IgnoredParameters.Add(parameterName);
                            }
                        }

                        z++;
                    }

                    i += (z - 1);
                }

                GUI.backgroundColor = defaultColor;
            }
        }



    }

}


#endif