#if UNITY_EDITOR

using UnityEditor;

namespace FishNet.Managing.Editing
{
    [CustomEditor(typeof(NetworkManager))]
    internal sealed class NetworkManagerEditor : Editor
    {
        private SerializedProperty _logging;
        private SerializedProperty _runInBackground;
        private SerializedProperty _dontDestroyOnLoad;
        private SerializedProperty _persistence;
        private SerializedProperty _spawnablePrefabs;

        private void OnEnable()
        {
            _logging = serializedObject.FindProperty("_logging");
            _runInBackground = serializedObject.FindProperty("_runInBackground");
            _dontDestroyOnLoad = serializedObject.FindProperty("_dontDestroyOnLoad");
            _persistence = serializedObject.FindProperty("_persistence");
            _spawnablePrefabs = serializedObject.FindProperty("_spawnablePrefabs");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_runInBackground);
            EditorGUILayout.PropertyField(_dontDestroyOnLoad);
            EditorGUILayout.PropertyField(_persistence);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_logging);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_spawnablePrefabs);
            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif