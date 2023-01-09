using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Ownership
{
    /// <summary>
    /// Adding this component allows any client to use predictive spawning on this prefab.
    /// </summary>
    public class PredictedSpawn : NetworkBehaviour
    {
        #region Types.
        /// <summary>
        /// What to do if the server does not respond to this predicted spawning.
        /// </summary>
        private enum NoResponseHandlingType
        {
            /// <summary>
            /// Destroy this object.
            /// </summary>
            Destroy = 0,
            /// <summary>
            /// Keep this object.
            /// </summary>
            Keep = 1,
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// Type of predicted spawning to allow.
        /// </summary>
        [Tooltip("Type of predicted spawning to allow.")]
        [SerializeField] private PredictedSpawningType _spawningType = (PredictedSpawningType.Spawn | PredictedSpawningType.Despawn);
        /// <summary>
        /// How to manage this object on client when the server does not respond to the predicted spawning.
        /// </summary>
        [Tooltip("How to manage this object on client when the server does not respond to the predicted spawning.")]
        [SerializeField]
        private NoResponseHandlingType _noResponseHandling = NoResponseHandlingType.Destroy;
        [Tooltip("Amount of time to wait for a response before NoResponseHandling is used.")]
        [SerializeField]
        private float _responseWait = 1f;
        #endregion        

        public override void OnStartClient()
        {
            base.OnStartClient();
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        /// <summary>
        /// Called after OnTick but before data sends.
        /// </summary>
        private void TimeManager_OnPostTick()
        {
            
        }

        /// <summary>
        /// Takes ownership of this object to the local client and allows immediate control.
        /// </summary>
        [Client]
        public virtual void Spawn(NetworkConnection owner = null)
        {
            if (_spawningType == PredictedSpawningType.Disabled)
                return;

            //If not server go through the server.
            if (!base.IsServer)
            {
                //base.NetworkObject.SetLocalOwnership(c);
                //ServerTakeOwnership();
            }
            //Otherwise take directly without rpcs.
            else
            {
                //OnTakeOwnership(c);
            }
        }


        ///// <summary>
        ///// Takes ownership of this object.
        ///// </summary>
        //[ServerRpc(RequireOwnership = false)]
        //private void ServerTakeOwnership(NetworkConnection caller = null)
        //{
        //    OnTakeOwnership(caller);
        //}

        ///// <summary>
        ///// Called on the server when a client tries to take ownership of this object.
        ///// </summary>
        ///// <param name="caller">Connection trying to take ownership.</param>
        //[Server]
        //protected virtual void OnTakeOwnership(NetworkConnection caller)
        //{
        //    //Client somehow disconnected between here and there.
        //    if (!caller.IsActive)
        //        return;
        //    //Feature is not enabled.
        //    if (!_allowTakeOwnership)
        //        return;
        //    //Already owner.
        //    if (caller == base.Owner)
        //        return;

        //    base.GiveOwnership(caller);
        //    /* No need to send a response back because an ownershipchange will handle changes.
        //     * Although if you were to override with this your own behavior
        //     * you could send responses for approved/denied. */
        //}

    }

}