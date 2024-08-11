#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class NetworkObserverDrawer
{
    public static void ShowNetworkObserverDescription(bool showInEnglish)
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Network Observer Settings", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (showInEnglish)
        {
            DisplayEnglishNetworkObserverDescription();
        }
        else
        {
            DisplayChineseNetworkObserverDescription();
        }

        EditorGUILayout.EndVertical();
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

    private static void DisplayChineseNetworkObserverDescription()
    {
        EditorGUILayout.HelpBox("NetworkObserver 使用条件来确定客户端是否有资格成为对象的观察者；可以使用多个条件。NetworkObserver 组件可用于覆盖 ObserverManager，或向添加 NetworkObserver 的对象添加其他条件。", MessageType.Info);
        EditorGUILayout.LabelField("条件包括：");
        EditorGUILayout.LabelField("1. Distance Condition：如果客户端在指定距离内，则为真。");
        EditorGUILayout.LabelField("2. Grid Condition：与 Distance Condition 类似，但不够准确且性能更高。要求在 NetworkManager 对象上或下方放置 HashGrid。");
        EditorGUILayout.LabelField("3. Scene Condition：如果客户端与对象共享任何场景，则为真。");
        EditorGUILayout.LabelField("4. Match Condition：如果玩家或对象在同一比赛中，则为真。可以将拥有和非拥有的对象添加到比赛中。未添加到比赛中的对象或玩家的数据将与所有人同步，除非受到其他条件的阻止。");
        EditorGUILayout.LabelField("5. Owner Condition：如果玩家拥有该物品，则为真。使物品仅对拥有者可见。如果没有拥有者，则任何客户端都看不到该物品。");
        EditorGUILayout.LabelField("6. Host Condition：如果玩家是客户端主机，则为真。任何非客户端主机的连接都将不符合此条件。");
        EditorGUILayout.LabelField("组件设置：");
        EditorGUILayout.LabelField("Override Type：用于更改 NetworkObserver 组件如何使用 ObserverManager 设置。添加缺失值将添加来自 ObserverManager 的任何 NetworkObserver 上尚不存在的条件。使用管理器将用管理器中的条件替换条件。忽略管理器将保留 NetworkObserver 条件，完全忽略 ObserverManager。");
        EditorGUILayout.LabelField("Updating Host Visibility：更改客户端主机渲染器的可见性，当服务器对象对客户端不可见时。如果您希望在可见性更改期间启用和禁用其他方面，请考虑使用 NetworkObject.OnHostVisibilityUpdated 事件。");
        EditorGUILayout.LabelField("Observer Conditions：指要使用的条件。");
    }
}
#endif