#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class OwnershipDrawer
{
    public static void ShowOwnershipDescription()
    {

        ShowOwnershipDescriptionEnglish();
        
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
    
}
#endif