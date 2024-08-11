#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class OwnershipDrawer
{
    public static void ShowOwnershipDescription(bool showInEnglish)
    {
        if (showInEnglish)
        {
            ShowOwnershipDescriptionEnglish();
        }
        else
        {
            ShowOwnershipDescriptionChinese();
        }
    }

    private static void ShowOwnershipDescriptionEnglish()
    {
        EditorGUILayout.LabelField("Ownership Overview", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Ownership Concept", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Ownership is a term you'll frequently encounter while developing with Fish-Net. " +
            "Only one owner can exist at a time, and ownership determines which client can control an object. " +
            "It's important to note that objects do not always have an owner, and ownership changes must be performed by the server.",
            EditorStyles.wordWrappedLabel
        );

        EditorGUILayout.LabelField("Assigning Ownership", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "There are several ways to assign ownership to a client. The first method is to spawn an object with a specific connection or client as the owner.",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"GameObject go = Instantiate(_yourPrefab);
InstanceFinder.ServerManager.Spawn(go, ownerConnection);",
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("Changing or Adding Ownership", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "If an object has already been spawned, you can grant or take away ownership at any time. The previous owner will be replaced by the new owner.",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"networkObject.GiveOwnership(newOwnerConnection);",
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("Removing Ownership", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "You can also remove ownership from any object at any time.",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"networkObject.RemoveOwnership();",
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("Checking Ownership", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "You can check ownership status in various ways. These checks can be performed on NetworkObject or NetworkBehaviour.",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"// Is true if the local client owns the object.
base.IsOwner;

// Returns the current owner NetworkConnection.
// This can be accessible on clients even if they do not own the object
// so long as ServerManager.ShareIds is enabled. Sharing Ids has absolutely no
// security risk.
base.Owner;

// True if the local client owns the object, or if
// is the server and there is no owner.
base.HasAuthority",
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("Example Usage", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Here's an example of moving an object only if the client owns it:",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"private void Update()
{
    if (base.IsOwner)
    {
        float hor = Input.GetAxisRaw(""Horizontal"");
        float ver = Input.GetAxisRaw(""Vertical"");
        transform.position += new Vector3(hor, 0f, ver);
    }
}",
            EditorStyles.textArea
        );
    }

    private static void ShowOwnershipDescriptionChinese()
    {
        EditorGUILayout.LabelField("所有权概述", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("所有权概念", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "所有权是您在使用 Fish-Net 进行开发过程中经常看到和使用的一个术语。只能有一个所有者，所有权决定哪个客户端可以控制某个对象。重要的是要知道，对象并不总是有所有者，所有权更改必须由服务器完成。",
            EditorStyles.wordWrappedLabel
        );

        EditorGUILayout.LabelField("分配所有权", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "有几种方法可以将所有权授予客户端。第一种方法是生成一个具有特定连接或客户端的对象作为所有者。",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"GameObject go = Instantiate(_yourPrefab);
InstanceFinder.ServerManager.Spawn(go, ownerConnection);",
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("更改或添加所有权", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "如果对象已经生成，您可以随时授予或获取对象的所有权。以前的所有者将被新所有者取代。",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"networkObject.GiveOwnership(newOwnerConnection);",
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("移除所有权", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "您还可以随时删除客户端对任何对象的所有权。",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"networkObject.RemoveOwnership();",
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("检查所有权", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "可以通过多种方式检查所有权状态。这些检查可以在 NetworkObject 或 NetworkBehaviour 上进行。",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"// 如果本地客户端拥有对象，则为 true。
base.IsOwner;

// 返回当前所有者的 NetworkConnection。
// 即使客户端不拥有对象，只要 ServerManager.ShareIds 启用，这也可以被访问。
// 共享 Ids 完全没有安全风险。
base.Owner;

// 如果本地客户端拥有对象，或者如果是服务器且没有所有者，则为 true。
base.HasAuthority",
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("示例用法", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "以下是仅当客户端拥有对象时才移动对象的示例：",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"private void Update()
{
    if (base.IsOwner)
    {
        float hor = Input.GetAxisRaw(""Horizontal"");
        float ver = Input.GetAxisRaw(""Vertical"");
        transform.position += new Vector3(hor, 0f, ver);
    }
}",
            EditorStyles.textArea
        );
    }
}
#endif