using FishNet.Connection;
using System;
using UnityEngine;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        /// <summary>
        /// Called on the server after initializing this object.
        /// SyncTypes modified before or during this method will be sent to clients in the spawn message.
        /// </summary>
        internal event Action OnStartServer;
        /// <summary>
        /// Called on the server before deinitializing this object.
        /// Useful for tidying object of network related task, including the ability to send RPCs before the object is despawned on clients.
        /// </summary>
        internal event Action OnStopServer;
        /// <summary>
        /// Called on the server after ownership has changed.
        /// </summary>
        /// <param name="owner">Current owner of this object.</param>
        internal event Action<NetworkConnection> OnOwnershipServer;
        /// <summary>
        /// Called on the server after a spawn message for this object has been sent to clients.
        /// Useful for sending remote calls or actions to clients.
        /// </summary>
        internal event Action<NetworkConnection> OnSpawnServer;
        /// <summary>
        /// Called on the server before a despawn message for this object has been sent to clients.
        /// Useful for sending remote calls or actions to clients.
        /// </summary>
        internal event Action<NetworkConnection> OnDespawnServer;
        /// <summary>
        /// Called on the client after initializing this object.
        /// </summary>
        /// <param name="isOwner">True if the owner of this object.</param>
        internal event Action<bool> OnStartClient;
        /// <summary>
        /// Called on the client before deinitializing this object.
        /// </summary>
        /// <param name="isOwner">True if the owner of this object.</param>
        internal event Action<bool> OnStopClient;
        /// <summary>
        /// Called on the client after gaining or losing ownership.
        /// </summary>
        /// <param name="newOwner">Current owner of this object.</param>
        internal event Action<NetworkConnection> OnOwnershipClient;

        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        private void InitializeCallbacks(bool asServer)
        {
            //As server.
            if (asServer)
            {
                OnStartServer?.Invoke();
                if (OwnerIsValid)
                    OnOwnershipServer?.Invoke(Owner);
            }
            //As client.
            else
            {
                OnStartClient?.Invoke(IsOwner);
                if (IsOwner)
                    OnOwnershipClient?.Invoke(Owner);
            }
        }


        /// <summary>
        /// Called on the server after it sends a spawn message to a client.
        /// </summary>
        /// <param name="conn">Connection spawn was sent to.</param>
        internal void InvokeOnServerSpawn(NetworkConnection conn)
        {
            OnSpawnServer?.Invoke(conn);
        }
        /// <summary>
        /// Called on the server before it sends a despawn message to a client.
        /// </summary>
        /// <param name="conn">Connection spawn was sent to.</param>
        internal void InvokeOnServerDespawn(NetworkConnection conn)
        {
            OnDespawnServer?.Invoke(conn);
        }

        /// <summary>
        /// Invokes OnStop callbacks.
        /// </summary>
        /// <param name="asServer"></param>
        private void InvokeStopCallbacks(bool asServer)
        {
            if (asServer)
                OnStopServer?.Invoke();
            else
                OnStopClient?.Invoke(IsOwner);
        }

        /// <summary>
        /// Invokes OnOwnership callbacks.
        /// </summary>
        /// <param name="newOwner"></param>
        private void InvokeOwnership(NetworkConnection newOwner, bool asServer)
        {
            if (asServer)
                OnOwnershipServer?.Invoke(newOwner);
            else
                OnOwnershipClient?.Invoke(newOwner);
        }
    }

}

