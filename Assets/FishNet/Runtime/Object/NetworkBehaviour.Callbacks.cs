using FishNet.Connection;
using UnityEngine;

namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Called on the server after initializing this object.
        /// SyncTypes modified before or during this method will be sent to clients in the spawn message.
        /// </summary> 
        public virtual void OnStartServer() { }
        /// <summary>
        /// Called on the server before deinitializing this object.
        /// </summary>
        public virtual void OnStopServer() { }
        /// <summary>
        /// Called on the server after ownership has changed.
        /// </summary>
        /// <param name="newOwner">Current owner of this object.</param>
        public virtual void OnOwnershipServer(NetworkConnection newOwner) { }
        /// <summary>
        /// Called on the server after a spawn message for this object has been sent to clients.
        /// Useful for sending remote calls or data to clients.
        /// </summary>
        /// <param name="connection">Connection the object is being spawned for.</param>
        public virtual void OnSpawnServer(NetworkConnection connection) { }
        /// <summary>
        /// Called on the server before a despawn message for this object has been sent to clients.
        /// Useful for sending remote calls or actions to clients.
        /// </summary>
        public virtual void OnDespawnServer(NetworkConnection connection) { }
        /// <summary>
        /// Called on the client after initializing this object.
        /// </summary>
        public virtual void OnStartClient() { }
        /// <summary>
        /// Called on the client before deinitializing this object.
        /// </summary>
        public virtual void OnStopClient() { }
        /// <summary>
        /// Called on the client after gaining or losing ownership.
        /// </summary>
        /// <param name="newOwner">Previous owner of this object.</param>
        public virtual void OnOwnershipClient(NetworkConnection prevOwner) { }

    }


}