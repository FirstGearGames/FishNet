#if UNITY_EDITOR
using UnityEditor;
using ConfigurationEditor = FishNet.Configuring.Editing.ConfigurationEditor;


public class BuildInformation
    
{
    /// <summary>
    /// True to remove server only logic.
    /// </summary>
    public static bool IsGuiReleaseBuild => (IsBuilding && !IsHeadless && !IsDevelopment);
    /// <summary>
    /// True to remove client only logic.
    /// </summary>
    public static bool IsServerOnlyBuild => (IsBuilding && IsHeadless);
    /// <summary>
    /// True to include IsClient checks.
    /// </summary>
    public static bool CheckIsClient => (!IsBuilding || !StripBuild || (IsBuilding && IsDevelopment));
    /// <summary>
    /// True to include IsServer checks.
    /// </summary>
    public static bool CheckIsServer => (!IsBuilding || !StripBuild || (IsBuilding && !IsHeadless));

    /// <summary>
    /// True if building.
    /// </summary>
    public static bool IsBuilding { get; private set; }
    /// <summary>
    /// True if a headless build.
    /// </summary>
    public static bool IsHeadless { get; private set; }
    /// <summary>
    /// True if a development build.
    /// </summary>
    public static bool IsDevelopment { get; private set; }
    /// <summary>
    /// True to strip release builds.
    /// </summary>
    public static bool StripBuild
    {
        get
        {
            
            /* This is to protect non pro users from enabling this
             * without the extra logic code.  */
#pragma warning disable CS0162 // Unreachable code detected
            return false;
#pragma warning restore CS0162 // Unreachable code detected
        }
    }

    

}

#endif