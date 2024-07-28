#if UNITY_EDITOR
using FishNet.Object.Prediction;
using UnityEditor;
using UnityEngine;

namespace FishNet.Object.Editing
{
    [CustomEditor(typeof(NetworkObject), true)]
    [CanEditMultipleObjects]
    public class NetworkObjectEditor : Editor
    {
        // Serialized properties for network settings
        private SerializedProperty _isNetworked;
        private SerializedProperty _isSpawnable;
        private SerializedProperty _isGlobal;
        private SerializedProperty _initializeOrder;
        private SerializedProperty _defaultDespawnType;

        // Serialized properties for prediction settings
        private SerializedProperty _enablePrediction;
        private SerializedProperty _enableStateForwarding;
        private SerializedProperty _networkTransform;
        private SerializedProperty _predictionType;
        private SerializedProperty _graphicalObject;
        private SerializedProperty _detachGraphicalObject;

        // Serialized properties for smoothing settings
        private SerializedProperty _ownerSmoothedProperties;
        private SerializedProperty _spectatorSmoothedProperties;
        private SerializedProperty _ownerInterpolation;
        private SerializedProperty _adaptiveInterpolation;
        private SerializedProperty _spectatorInterpolation;
        private SerializedProperty _enableTeleport;
        private SerializedProperty _teleportThreshold;

        // Editor tab selection
        private int _selectedTab = 0;
        private string[] _tabNames = { "Settings", "Prediction" };

        // FishNet logo
        private Texture2D fishNetLogo;

        protected virtual void OnEnable()
        {
            // Initialize serialized properties
            _isNetworked = serializedObject.FindProperty(nameof(_isNetworked));
            _isSpawnable = serializedObject.FindProperty(nameof(_isSpawnable));
            _isGlobal = serializedObject.FindProperty(nameof(_isGlobal));
            _initializeOrder = serializedObject.FindProperty(nameof(_initializeOrder));
            _defaultDespawnType = serializedObject.FindProperty(nameof(_defaultDespawnType));

            _enablePrediction = serializedObject.FindProperty(nameof(_enablePrediction));
            _enableStateForwarding = serializedObject.FindProperty(nameof(_enableStateForwarding));
            _networkTransform = serializedObject.FindProperty(nameof(_networkTransform));
            _predictionType = serializedObject.FindProperty(nameof(_predictionType));
            _graphicalObject = serializedObject.FindProperty(nameof(_graphicalObject));
            _detachGraphicalObject = serializedObject.FindProperty(nameof(_detachGraphicalObject));

            _ownerSmoothedProperties = serializedObject.FindProperty(nameof(_ownerSmoothedProperties));
            _ownerInterpolation = serializedObject.FindProperty(nameof(_ownerInterpolation));
            _adaptiveInterpolation = serializedObject.FindProperty(nameof(_adaptiveInterpolation));
            _spectatorSmoothedProperties = serializedObject.FindProperty(nameof(_spectatorSmoothedProperties));
            _spectatorInterpolation = serializedObject.FindProperty(nameof(_spectatorInterpolation));
            _enableTeleport = serializedObject.FindProperty(nameof(_enableTeleport));
            _teleportThreshold = serializedObject.FindProperty(nameof(_teleportThreshold));

            // Find FishNet logo dynamically
            string[] guids = AssetDatabase.FindAssets("fishnet_light");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                fishNetLogo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        public override void OnInspectorGUI()
        {
            // Update the serialized object
            serializedObject.Update();
            NetworkObject nob = (NetworkObject)target;

            GUILayout.BeginHorizontal();
            // Display the script field
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", MonoScript.FromMonoBehaviour(nob), typeof(NetworkObject), false);
            GUI.enabled = true;
            // Add a button next to the script field
            if (GUILayout.Button(UnityEditor.EditorGUIUtility.IconContent("_Help"), GUILayout.Width(25)))
            {
                Application.OpenURL("https://fish-networking.gitbook.io/docs/manual/components/network-object");
            }
            GUILayout.EndHorizontal();

            // Tab selection for settings and prediction
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);

            // Increase indentation for tab content
            EditorGUI.indentLevel++;

            // Draw the selected tab
            if (_selectedTab == 0)
            {
                DrawSettingsTab();
            }
            else if (_selectedTab == 1)
            {
                DrawPredictionTab();
            }

            // Restore indentation level
            EditorGUI.indentLevel--;

            // Apply modified properties
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSettingsTab()
        {
            // Draw settings tab content
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_isNetworked);
            EditorGUILayout.PropertyField(_isSpawnable);
            EditorGUILayout.PropertyField(_isGlobal);
            EditorGUILayout.PropertyField(_initializeOrder);
            EditorGUILayout.PropertyField(_defaultDespawnType);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); // Add horizontal line
        }

        public override bool HasPreviewGUI()
        {
            // Check if the target object has a NetworkObject component
            MonoBehaviour monoBehaviour = target as MonoBehaviour;
            return monoBehaviour != null && monoBehaviour.GetComponent<NetworkObject>() != null;
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Network Object Preview");
        }

        public override void OnPreviewSettings()
        {
            GUILayout.Label("Network Object Settings", "preLabel");
            GUILayout.Button("Refresh", "preButton");
        }

        private void DrawPredictionTab()
        {
            // Draw prediction tab content
            EditorGUILayout.LabelField("Prediction", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enablePrediction);

            if (_enablePrediction.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_predictionType);
                EditorGUILayout.PropertyField(_enableStateForwarding);

                if (!_enableStateForwarding.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_networkTransform);
                    EditorGUI.indentLevel--;
                }

                bool graphicalSet = (_graphicalObject.objectReferenceValue != null);
                EditorGUILayout.PropertyField(_graphicalObject);

                if (graphicalSet)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_detachGraphicalObject);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.LabelField("Smoothing", EditorStyles.boldLabel);

                if (!graphicalSet)
                {
                    EditorGUILayout.HelpBox("More smoothing settings will be displayed when a graphicalObject is set.", MessageType.Info);
                }
                else
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_enableTeleport);

                    if (_enableTeleport.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(_teleportThreshold, new GUIContent("Teleport Threshold"));
                        EditorGUI.indentLevel--;
                    }

                    DrawOwnerSettings();
                    DrawSpectatorSettings();

                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); // Add horizontal line
        }

        private void DrawOwnerSettings()
        {
            // Draw owner settings
            EditorGUILayout.LabelField("Owner", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_ownerInterpolation, new GUIContent("Interpolation"));
            EditorGUILayout.PropertyField(_ownerSmoothedProperties, new GUIContent("Smoothed Properties"));
            EditorGUI.indentLevel--;
        }

        private void DrawSpectatorSettings()
        {
            // Draw spectator settings
            EditorGUILayout.LabelField("Spectator", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_adaptiveInterpolation);

            if (_adaptiveInterpolation.intValue == (int)AdaptiveInterpolationType.Off)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_spectatorInterpolation, new GUIContent("Interpolation"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(_spectatorSmoothedProperties, new GUIContent("Smoothed Properties"));
            EditorGUI.indentLevel--;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            // Get the NetworkObject component from the target
            MonoBehaviour monoBehaviour = target as MonoBehaviour;
            NetworkObject networkObject = monoBehaviour.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                EditorGUI.LabelField(r, "No NetworkObject component found");
                return;
            }

            // Draw light blue background rectangle
            EditorGUI.DrawRect(r, new Color(0.0745f, 0.3647f, 0.6275f, 1f)); // 使用十六进制颜色#135da0
            // Draw the FishNet logo if loaded successfully
            if (fishNetLogo != null)
            {
                float logoSize = 64f;
                GUI.DrawTexture(new Rect(r.x + r.width - logoSize - 10, r.y + 10, logoSize, logoSize), fishNetLogo, ScaleMode.ScaleToFit);
            }
            else
            {
                Debug.LogWarning("FishNet logo could not be loaded.");
            }

            // Set text color
            Color originalColor = GUI.contentColor;
            GUI.contentColor = Color.white; // White text for better contrast

            // Draw network information title
            EditorGUI.LabelField(new Rect(r.x + 5, r.y + 5, r.width, EditorGUIUtility.singleLineHeight), "Network Information", EditorStyles.boldLabel);

            // Draw network information
            float yOffset = EditorGUIUtility.singleLineHeight * 1.5f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float xOffset = r.x + 10f;
            float widthOffset = r.width - 20f;

            // Change the order and add additional labels for clarity
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Object ID: {networkObject.ObjectId}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Owner ID: {networkObject.OwnerId}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Owner(ClientId) ID: {networkObject.Owner.ClientId}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Owned: {networkObject.IsOwner}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Owner Or Server: {networkObject.IsOwnerOrServer}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Client: {networkObject.IsClient}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Server: {networkObject.IsServer}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Global: {networkObject.IsGlobal}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Scene Object: {networkObject.IsSceneObject}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Nested: {networkObject.IsNested}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Spawned: {networkObject.IsSpawned}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Spawnable: {networkObject.IsSpawnable}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Is Offline: {networkObject.IsOffline}");
            yOffset += lineHeight;
            EditorGUI.LabelField(new Rect(xOffset, r.y + yOffset, widthOffset, lineHeight), $"Observers: {networkObject.Observers}");

            // Modify the object's name to display network ID and ownership status
            if (networkObject.IsSpawned && !monoBehaviour.gameObject.name.Contains("("))
            {
                string newName = $"{monoBehaviour.gameObject.name} ({networkObject.ObjectId}, Owned: {networkObject.IsOwner})";
                monoBehaviour.gameObject.name = newName;
            }

            // Restore original text color
            GUI.contentColor = originalColor;
        }
    }
}
#endif
