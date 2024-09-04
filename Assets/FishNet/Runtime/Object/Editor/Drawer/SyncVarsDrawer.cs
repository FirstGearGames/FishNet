#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class SyncVarsDrawer
{
    public static void ShowSyncVarsDescription()
    {

        ShowSyncVarsDescriptionEnglish();
    }

    private static void ShowSyncVarsDescriptionEnglish()
    {
        EditorGUILayout.LabelField("SyncVars Overview", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("1. Define SyncVar", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "To use SyncVar, define a variable of type SyncVar<T> in your class. " +
            "The type T can be any type supported by SyncVar.",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"public class YourClass : NetworkBehaviour
{
    public SyncVar<int> yourSyncVar = new SyncVar<int>();
}", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("2. Modify Value on Server", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "To modify the SyncVar value, use a ServerRpc to set the value on the server. " +
            "The new value will be automatically synchronized to all clients.",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"[ServerRpc]
public void ModifySyncVar(int newValue)
{
    yourSyncVar.Value = newValue;
}", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("3. Subscribe to Changes on Client", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "On the client side, you can subscribe to the OnChange event of the SyncVar. " +
            "This allows you to react to changes and update the UI or perform other actions.",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"public class YourClass : NetworkBehaviour
{
    public SyncVar<int> yourSyncVar = new SyncVar<int>();

    private void OnEnable()
    {
        yourSyncVar.OnChange += OnSyncVarChanged;
    }

    private void OnDisable()
    {
        yourSyncVar.OnChange -= OnSyncVarChanged;
    }

    private void OnSyncVarChanged(int previousValue, int newValue, bool asServer)
    {
        // Handle the change
    }
}", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("4. SyncTypeSettings Configuration", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "SyncTypeSettings allows you to configure how SyncVar behaves. " +
            "You can set permissions and synchronization frequency.",
            EditorStyles.wordWrappedLabel
        );

        EditorGUILayout.LabelField("A) Server Only Write", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            @"public SyncVar<int> yourSyncVar = new SyncVar<int>(new SyncTypeSettings { WritePermission = WritePermission.ServerOnly });", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("B) Client Unsynchronized", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            @"public SyncVar<int> yourSyncVar = new SyncVar<int>(new SyncTypeSettings { WritePermission = WritePermission.ClientUnsynchronized });", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("C) Observer Permissions", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            @"public SyncVar<int> yourSyncVar = new SyncVar<int>(new SyncTypeSettings { ReadPermission = ReadPermission.Observers, WritePermission = WritePermission.ServerOnly });", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("D) Synchronization Frequency", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            @"public SyncVar<int> yourSyncVar = new SyncVar<int>(new SyncTypeSettings { SendRate = 0.1f });", 
            EditorStyles.textArea
        );
    }
}
#endif