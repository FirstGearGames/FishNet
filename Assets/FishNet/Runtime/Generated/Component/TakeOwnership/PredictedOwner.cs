using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace FishNet.Component.Ownership
{
    /// <summary>
    /// Adding this component allows any client to take ownership of the object and begin modifying it immediately.
    /// </summary>
    public class PredictedOwner : NetworkBehaviour
    {
        #region Public.
        /// <summary>
        /// True if the local client used TakeOwnership and is awaiting an ownership change.
        /// </summary>
        public bool TakingOwnership { get; private set; }
        /// <summary>
        /// Owner on client prior to taking ownership. This can be used to reverse a failed ownership attempt.
        /// </summary>
        public NetworkConnection PreviousOwner { get; private set; } = NetworkManager.EmptyConnection;
        #endregion

        #region Serialized.
        /// <summary>
        /// True if to enable this component.
        /// </summary>
        [Tooltip("True if to enable this component.")]
        [SyncVar(SendRate = 0f)]
        [SerializeField]
        private bool _allowTakeOwnership = true;
        /// <summary>
        /// Sets the next value for AllowTakeOwnership and synchronizes it.
        /// Only the server may use this method.
        /// </summary>
        /// <param name="value">Next value to use.</param>
        [Server]
        public void SetAllowTakeOwnership(bool value) => _allowTakeOwnership = value;
        #endregion

        /// <summary>
        /// Called on the client after gaining or losing ownership.
        /// </summary>
        /// <param name="prevOwner">Previous owner of this object.</param>
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            /* Unset taken ownership either way.
            * If the new owner it won't be used,
            * if no longer owner then another client
            * took it. */
            TakingOwnership = false;
            PreviousOwner = base.Owner;
        }

        /// <summary>
        /// Takes ownership of this object to the local client and allows immediate control.
        /// </summary>
        [Client]
        public virtual void TakeOwnership()
        {
            if (!_allowTakeOwnership)
                return;
            //Already owner.
            if (base.IsOwner)
                return;

            NetworkConnection c = base.ClientManager.Connection;
            TakingOwnership = true;
            //If not server go through the server.
            if (!base.IsServer)
            {
                base.NetworkObject.SetLocalOwnership(c);
                ServerTakeOwnership();
            }
            //Otherwise take directly without rpcs.
            else
            {
                OnTakeOwnership(c);
            }
        }


        /// <summary>
        /// Takes ownership of this object.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerTakeOwnership(NetworkConnection caller = null)
        {
            OnTakeOwnership(caller);
        }

        /// <summary>
        /// Called on the server when a client tries to take ownership of this object.
        /// </summary>
        /// <param name="caller">Connection trying to take ownership.</param>
        [Server]
        protected virtual void OnTakeOwnership(NetworkConnection caller)
        {
            //Client somehow disconnected between here and there.
            if (!caller.IsActive)
                return;
            //Feature is not enabled.
            if (!_allowTakeOwnership)
                return;
            //Already owner.
            if (caller == base.Owner)
                return;

            base.GiveOwnership(caller);
            /* No need to send a response back because an ownershipchange will handle changes.
             * Although if you were to override with this your own behavior
             * you could send responses for approved/denied. */
        }

    }

}