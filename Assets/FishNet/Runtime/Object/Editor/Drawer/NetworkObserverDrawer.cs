#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class NetworkObserverDrawer
{
    public static void ShowNetworkObserverDescription()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Network Observer Settings", EditorStyles.boldLabel);
        GUILayout.Space(10);

        DisplayEnglishNetworkObserverDescription();
    }

    private static void DisplayEnglishNetworkObserverDescription()
    {
        EditorGUILayout.HelpBox("NetworkObserver uses conditions to determine if a client is eligible to be an observer of the object; multiple conditions can be used together.", MessageType.Info);
        EditorGUILayout.LabelField("Conditions include:");
        EditorGUILayout.LabelField("1. Distance Condition: True if the client is within a specified distance from the object.");
        EditorGUILayout.LabelField("2. Grid Condition: Similar to Distance Condition, but less accurate and more performance-efficient. Requires HashGrid placed on or below the NetworkManager object.");
        EditorGUILayout.LabelField("3. Scene Condition: True if the client shares any scene with the object.");
        EditorGUILayout.LabelField("4. Match Condition: True if the player or object is in the same match. Both owned and non-owned objects can be added to the match. Data of objects or players not added to the match is synchronized with everyone unless blocked by other conditions.");
        EditorGUILayout.LabelField("5. Owner Condition: True if the player owns the item. Makes the item visible only to the owner. If there is no owner, no client sees the item.");
        EditorGUILayout.LabelField("6. Host Condition: True if the player is the host of the client. Any connection not being the host client will not meet this condition.");
        EditorGUILayout.LabelField("Component Settings:");
        EditorGUILayout.LabelField("Override Type: Changes how NetworkObserver component uses the ObserverManager settings. Adding missing values will add any conditions not present on NetworkObserver from the ObserverManager. Using the manager will replace conditions on the NetworkObserver with those in the manager. Ignoring the manager keeps the NetworkObserver conditions and fully ignores the ObserverManager.");
        EditorGUILayout.LabelField("Updating Host Visibility: Changes the visibility of the client host renderer when the server object is not visible to the client. Consider using NetworkObject.OnHostVisibilityUpdated event if you want to enable and disable other aspects during visibility changes.");
        EditorGUILayout.LabelField("Observer Conditions: Refers to the conditions to use.");
    }


}
#endif