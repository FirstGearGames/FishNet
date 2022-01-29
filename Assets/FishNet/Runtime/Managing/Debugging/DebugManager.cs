using UnityEngine;

namespace FishNet.Managing.Debugging
{
    /// <summary>
    /// A container for debugging.
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugManager : MonoBehaviour
    {
        public bool ObserverRpcLinks = true;
        public bool TargetRpcLinks = true;
        public bool ReplicateRpcLinks = true;
        public bool ReconcileRpcLinks = true;
        public bool ServerRpcLinks = true;
    }


}
