#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class NetworkObjectDrawer
{
    public static void ShowNetworkObjectDescription(bool showInEnglish)
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Network Object Settings", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (showInEnglish)
        {
            DisplayEnglishNetworkObjectDescription();
        }
        else
        {
            DisplayChineseNetworkObjectDescription();
        }

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

    private static void DisplayChineseNetworkObjectDescription()
    {
        EditorGUILayout.HelpBox("当您将继承自 NetworkBehaviour 的脚本添加到对象时，该组件将自动附加。", MessageType.Info);
        EditorGUILayout.LabelField("Is Networked", "指示对象是否始终被视为网络对象。如果为 false，则对象不会初始化为网络对象。如果您有对象有时只在本地运行，有时通过网络生成，这可能会很有用。使用 ServerManager.Spawn 生成对象时，Is Networked 将自动设置为 true。");
        EditorGUILayout.LabelField("Is Spawnable", "标记对象是否可以在运行时生成。对于不需要实例化的场景预制件，通常为 false。如果为 true，则该预制件将添加到 DefaultPrefabObjects 中。");
        EditorGUILayout.LabelField("Global", "会使 NetworkObject 始终为所有客户端所知，并将其添加到“Dont Destroy On Load”场景中。此设置对场景对象没有影响，但可以在预制件中设置，或在运行时实例化对象后立即更改。");
        EditorGUILayout.LabelField("Initialization Order", "决定了网络对象在同一时间生成时运行其初始化回调的顺序。值越低，优先级越高，执行得越早。默认值为 0，允许使用负值。");
        EditorGUILayout.LabelField("Default Despawn Type", "对象消失时的默认行为。物体通常会被销毁，但可以设置为其他值（例如 Pool），以提高性能。");
        EditorGUILayout.LabelField("Prediction Enabled", "启用预测。提供附加设置。");
        EditorGUILayout.LabelField("Prediction Type", "决定是否对预测对象使用 Rigidbody。如果使用 CharacterController 更新变换，则设置为 Other。如果使用 Rigidbody，则选择 Rigidbody 或 Rigidbody2D。");
        EditorGUILayout.LabelField("Enable State Forwarding", "用于将状态同步到所有客户端。适用于所有客户端和服务器使用相同输入的游戏。禁用此设置将导致仅对所有者使用预测。");
        EditorGUILayout.LabelField("Graphical Object", "保存预测对象图形的对象。必须是预测对象的子对象。");
        EditorGUILayout.LabelField("Detach Graphical Object", "如果为真，则图形对象将在运行时初始化/取消初始化对象时分离并重新连接，以解决抖动问题。");
        EditorGUILayout.LabelField("Interpolation", "客户端上图形对象的插值次数。设置低至 1 通常足以平滑刻数之间的帧。");
        EditorGUILayout.LabelField("Teleport", "允许图形对象在位置变化剧烈时传送到其实际位置。如果需要，可以使用此设置。");
        EditorGUILayout.LabelField("Teleport Threshold", "图形对象位置距离实际位置的阈值，超过此距离将传送到实际位置。");
    }
}
#endif