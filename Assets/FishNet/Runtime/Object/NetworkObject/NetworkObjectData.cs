
namespace FishNet.Object
{
    /// <summary>
    /// Action to take when despawning a NetworkObject.
    /// </summary>
    public enum DespawnType : byte
    {
        Destroy = 0,
        Pool = 1,
    }


    /// <summary>
    /// Current state of the NetworkObject.
    /// </summary>
    internal enum NetworkObjectState : byte
    {
        /// <summary>
        /// State has not been set. This occurs when the object has never been spawned or despawned.
        /// </summary>
        Unset = 0,
        /// <summary>
        /// Object is currently spawned.
        /// </summary>
        Spawned = 1,
        /// <summary>
        /// Object is currently despawned.
        /// </summary>
        Despawned = 2,
    }

    /// <summary>
    /// Options on retrieving nested NetworkObjects.
    /// </summary>
    [System.Flags]
    internal enum GetNetworkObjectOption : int
    {
        /// <summary>
        /// Include NetworkObject which nested are being returned for. 
        /// </summary>
        IncludeSelf = (1 << 0),
        /// <summary>
        /// Include initialize nested.
        /// </summary>
        Initialized = (1 << 1),
        /// <summary>
        /// Include runtime nested.
        /// </summary>
        Runtime = (1 << 2),
        /// <summary>
        /// Recursively iterate nested includes.
        /// </summary>
        /// <remarks>This only functions if Initialized or Runtime is flagged.</remarks>
        Recursive = (1 << 3),
        
        /// <summary>
        /// Uses Initialized and Runtime flags.
        /// </summary>
        InitializedRuntime = (Initialized | Runtime),
        /// <summary>
        /// Uses Initialized, Runtime, and Recursive flags.
        /// </summary>
        InitializedRuntimeRecursive = (Initialized | Runtime | Recursive),
        /// <summary>
        /// Sets all flags.
        /// </summary>
        All = ~0,
    }


    internal static class GetNetworkObjectOptionExtensions 
    {
        /// <summary>
        /// True if whole contains part.
        /// </summary>
        public static bool FastContains(this GetNetworkObjectOption whole, GetNetworkObjectOption part) => (whole & part) == part;
    }
}

