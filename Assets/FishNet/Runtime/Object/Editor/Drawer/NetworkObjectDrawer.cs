#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class NetworkObjectDrawer
{
    public static void ShowNetworkObjectDescription()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Network Object Settings", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        DisplayEnglishNetworkObjectDescription();

        EditorGUILayout.EndVertical();
    }

    private static void DisplayEnglishNetworkObjectDescription()
    {
        EditorGUILayout.HelpBox("When you add a script that inherits from NetworkBehaviour to your object, this component is automatically attached.", MessageType.Info);
        EditorGUILayout.LabelField("Is Networked", "Indicates whether the object should always be considered networked. If false, the object will not initialize as a networked object. This is useful for objects that sometimes run locally and other times are networked. Set to true automatically when spawned using ServerManager.Spawn.");
        EditorGUILayout.LabelField("Is Spawnable", "Marks whether the object can be spawned at runtime. False for scene prefabs that don’t need instantiation. If true, the prefab will be added to DefaultPrefabObjects.");
        EditorGUILayout.LabelField("Global", "Makes the NetworkObject always known to all clients and adds it to the 'Dont Destroy On Load' scene. This does not affect scene objects but can be set in the prefab or changed after runtime instantiation.");
        EditorGUILayout.LabelField("Initialization Order", "Determines the order of initialization callbacks for network objects spawned at the same time. Lower values have higher priority. Default is 0, allowing negative values.");
        EditorGUILayout.LabelField("Default Despawn Type", "The default behavior when an object despawns. Typically destroyed, but can be set to other values (e.g., Pool) for performance optimization.");
        EditorGUILayout.LabelField("Prediction Enabled", "Enables prediction for the object. Provides additional settings.");
        EditorGUILayout.LabelField("Prediction Type", "Determines if you use Rigidbody for prediction. Set to Other if using CharacterController, otherwise use Rigidbody or Rigidbody2D.");
        EditorGUILayout.LabelField("Enable State Forwarding", "Synchronizes state across all clients. Ideal for games where all clients and the server use the same input. Disable this setting to use prediction only for the owner.");
        EditorGUILayout.LabelField("Graphical Object", "The object holding the graphics of the predicted object. Must be a child of the predicted object.");
        EditorGUILayout.LabelField("Detach Graphical Object", "If true, graphical objects will be detached and reattached during runtime initialization/deinitialization to resolve jitter issues.");
        EditorGUILayout.LabelField("Interpolation", "Refers to the number of ticks for graphical interpolation on the client. A setting as low as 1 is usually sufficient.");
        EditorGUILayout.LabelField("Teleport", "Allows graphical objects to teleport to their actual position if changes are drastic. This is an optional setting.");
        EditorGUILayout.LabelField("Teleport Threshold", "Distance at which the graphical object will teleport to its actual position.");
    }
    
}
#endif