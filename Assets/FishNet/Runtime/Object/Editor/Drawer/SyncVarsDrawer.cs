#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class SyncVarsDrawer
{
    public static void ShowSyncVarsDescription(bool showInEnglish)
    {
        if (showInEnglish)
        {
            ShowSyncVarsDescriptionEnglish();
        }
        else
        {
            ShowSyncVarsDescriptionChinese();
        }
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

    private static void ShowSyncVarsDescriptionChinese()
    {
        EditorGUILayout.LabelField("SyncVars 概述", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("1. 定义 SyncVar", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "要使用 SyncVar，请在类中定义一个类型为 SyncVar<T> 的变量。类型 T 可以是任何支持的类型。",
            EditorStyles.wordWrappedLabel
        );
        EditorGUILayout.TextArea(
            @"public class YourClass : NetworkBehaviour
{
    public SyncVar<int> yourSyncVar = new SyncVar<int>();
}", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("2. 在服务器上修改值", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "要修改 SyncVar 值，请使用 ServerRpc 在服务器上设置值。新的值将自动同步到所有客户端。",
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

        EditorGUILayout.LabelField("3. 客户端上订阅变化", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "在客户端，您可以订阅 SyncVar 的 OnChange 事件。这允许您响应变化并更新 UI 或执行其他操作。",
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
        // 处理变化
    }
}", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("4. SyncTypeSettings 配置", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "SyncTypeSettings 允许您配置 SyncVar 的行为。您可以设置权限和同步频率。",
            EditorStyles.wordWrappedLabel
        );

        EditorGUILayout.LabelField("A) 仅服务器写入", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            @"public SyncVar<int> yourSyncVar = new SyncVar<int>(new SyncTypeSettings { WritePermission = WritePermission.ServerOnly });", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("B) 客户端不同步", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            @"public SyncVar<int> yourSyncVar = new SyncVar<int>(new SyncTypeSettings { WritePermission = WritePermission.ClientUnsynchronized });", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("C) 观察者权限", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            @"public SyncVar<int> yourSyncVar = new SyncVar<int>(new SyncTypeSettings { ReadPermission = ReadPermission.Observers, WritePermission = WritePermission.ServerOnly });", 
            EditorStyles.textArea
        );

        EditorGUILayout.LabelField("D) 同步频率", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
            @"public SyncVar<int> yourSyncVar = new SyncVar<int>(new SyncTypeSettings { SendRate = 0.1f });", 
            EditorStyles.textArea
        );
    }
}
#endif