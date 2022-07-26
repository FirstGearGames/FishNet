
namespace FishNet.Object
{
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

}

