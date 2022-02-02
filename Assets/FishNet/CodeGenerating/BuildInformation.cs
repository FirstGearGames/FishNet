#if UNITY_EDITOR
using UnityEditor;


public class BuildInformation
    
{
    /// <summary>
    /// True to remove server only logic.
    /// </summary>
    public static bool RemoveServerLogic => (IsBuilding && !IsHeadless && !IsDevelopment);
    /// <summary>
    /// True to remove client only logic.
    /// </summary>
    public static bool RemoveClientLogic => (IsBuilding && IsHeadless);
    /// <summary>
    /// True to include IsClient checks.
    /// </summary>
    public static bool CheckIsClient => (!IsBuilding || (IsBuilding && IsDevelopment));
    /// <summary>
    /// True to include IsServer checks.
    /// </summary>
    public static bool CheckIsServer => (!IsBuilding || (IsBuilding && !IsHeadless));

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


    

}

#endif