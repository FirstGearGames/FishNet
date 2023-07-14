using UnityEngine;

namespace FishNet.Managing.Debugging
{
    /// <summary>
    /// A container for debugging.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/DebugManager")]
    public class DebugManager : MonoBehaviour
    {
        /// <summary>
        /// True to write scene and object name when sending scene object ids to clients.
        /// </summary>
        public bool WriteSceneObjectDetails = false;
        /// <summary>
        /// True to use RpcLinks for Observer RPCs.
        /// </summary>
        public bool ObserverRpcLinks = true;
        /// <summary>
        /// True to use RpcLinks for Target RPCs.
        /// </summary>
        public bool TargetRpcLinks = true;
        /// <summary>
        /// True to use RpcLinks for Replicate RPCs.
        /// </summary>
        public bool ReplicateRpcLinks = true;
        /// <summary>
        /// True to use RpcLinks for Reconcile RPCs.
        /// </summary>
        public bool ReconcileRpcLinks = true;
        /// <summary>
        /// True to use RpcLinks for Server RPCs.
        /// </summary>
        public bool ServerRpcLinks = true;
    }


}
