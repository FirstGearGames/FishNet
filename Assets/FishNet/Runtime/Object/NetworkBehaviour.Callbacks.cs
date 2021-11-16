using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Object.Synchronizing.Internal;
using UnityEngine;

namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Invokes cached callbacks on SyncTypes which were held until OnStartXXXXX was called.
        /// </summary>
        /// <param name="asServer"></param>
        private void InvokeSyncTypeCallbacks(bool asServer)
        {
            foreach (SyncBase item in _syncVars.Values)
                item.OnStartCallback(asServer);
            foreach (SyncBase item in _syncObjects.Values)
                item.OnStartCallback(asServer);
        }
        /// <summary>
        /// True if OnStartServer has been called.
        /// </summary>
        [APIExclude]
        public bool OnStartServerCalled { get; private set; } = false;
        /// <summary>
        /// True if OnStartClient has been called.
        /// </summary>
        [APIExclude]
        public bool OnStartClientCalled { get; private set; } = false;
        /// <summary>
        /// Called on the server after initializing this object.
        /// SyncTypes modified before or during this method will be sent to clients in the spawn message.
        /// </summary> 
        public virtual void OnStartServer() 
        {
            OnStartServerCalled = true;
            InvokeSyncTypeCallbacks(true);
        }
        /// <summary>
        /// Called on the server before deinitializing this object.
        /// </summary>
        public virtual void OnStopServer()
        {
            OnStartServerCalled = false;
        }
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
        public virtual void OnStartClient()
        {
            OnStartClientCalled = true;
            InvokeSyncTypeCallbacks(false);
        }
        /// <summary>
        /// Called on the client before deinitializing this object.
        /// </summary>
        public virtual void OnStopClient()
        {
            OnStartClientCalled = false;
        }
        /// <summary>
        /// Called on the client after gaining or losing ownership.
        /// </summary>
        /// <param name="newOwner">Previous owner of this object.</param>
        public virtual void OnOwnershipClient(NetworkConnection prevOwner) { }

    }


}