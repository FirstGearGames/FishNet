using System;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using System.Runtime.CompilerServices;
using FishNet.Managing;
using Unity.Profiling;
using UnityEngine;

namespace FishNet.Object
{
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// True if OnStartServer has been called.
        /// </summary>
        [APIExclude]
        public bool OnStartServerCalled { get; private set; }
        /// <summary>
        /// True if OnStartClient has been called.
        /// </summary>
        [APIExclude]
        public bool OnStartClientCalled { get; private set; }
        #endregion

        #region Private.
        
        #region Private Profiler Markers
        private static readonly ProfilerMarker _pm_InvokeSyncTypeOnStartCallbacks = new("NetworkBehaviour.InvokeSyncTypeOnStartCallbacks(bool)");
        private static readonly ProfilerMarker _pm_InvokeSyncTypeOnStopCallbacks = new("NetworkBehaviour.InvokeSyncTypeOnStopCallbacks(bool)");
        
        private static readonly ProfilerMarker _pm_InvokeOnNetwork_Internal = new("NetworkBehaviour.InvokeOnNetwork_Internal(bool)");
        private static readonly ProfilerMarker _pm_OnStartNetwork_Internal = new("NetworkBehaviour.OnStartNetwork_Internal(bool)");
        private static readonly ProfilerMarker _pm_OnStopNetwork_Internal = new("NetworkBehaviour.OnStopNetwork_Internal(bool)");
        
        private static readonly ProfilerMarker _pm_OnStartServer_Internal = new("NetworkBehaviour.OnStartServer_Internal(bool)");
        private static readonly ProfilerMarker _pm_OnStopServer_Internal = new("NetworkBehaviour.OnStopServer_Internal(bool)");
        private static readonly ProfilerMarker _pm_OnOwnershipServer_Internal = new("NetworkBehaviour.OnOwnershipServer_Internal(NetworkConnection)");
        
        private static readonly ProfilerMarker _pm_OnStartClient_Internal = new("NetworkBehaviour.OnStartClient_Internal(bool)");
        private static readonly ProfilerMarker _pm_OnStopClient_Internal = new("NetworkBehaviour.OnStopClient_Internal(bool)");
        private static readonly ProfilerMarker _pm_OnOwnershipClient_Internal = new("NetworkBehaviour.OnOwnershipClient_Internal(NetworkConnection)");
        #endregion
        
        /// <summary>
        /// True if OnStartNetwork has been called.
        /// </summary>
        private bool _onStartNetworkCalled;
        /// <summary>
        /// True if OnStopNetwork has been called.
        /// </summary>
        private bool _onStopNetworkCalled;
        #endregion

        /* Payloads are written and read immediatley after the header containing the target NetworkObject/Behaviour. */
        /// <summary>
        /// Called when writing a spawn. This may be used to deliver information for predicted spawning, or simply have values set before initialization without depending on SyncTypes.
        /// </summary>
        /// <param name = "connection">Connection receiving the payload. When sending to the server connection.IsValid will return false.</param>
        public virtual void WritePayload(NetworkConnection connection, Writer writer) { }

        /// <summary>
        /// Called before network start callbacks, but after the object is initialized with network values. This may be used to read information from predicted spawning, or simply have values set before initialization without depending on SyncTypes.
        /// </summary>
        /// <param name = "connection">Connection sending the payload. When coming from the server connection.IsValid will return false.</param>
        public virtual void ReadPayload(NetworkConnection connection, Reader reader) { }

        /// <summary>
        /// Invokes OnStartXXXX for synctypes, letting them know the NetworkBehaviour start cycle has been completed.
        /// </summary>
        internal void InvokeSyncTypeOnStartCallbacks(bool asServer)
        {
            using (_pm_InvokeSyncTypeOnStartCallbacks.Auto())
            {
                foreach (SyncBase item in _syncTypes.Values)
                {
                    try
                    {
                        item.OnStartCallback(asServer);
                    }
                    catch (Exception e)
                    {
                        NetworkManager.LogError(e.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Invokes OnStopXXXX for synctypes, letting them know the NetworkBehaviour stop cycle is about to start.
        /// </summary>
        internal void InvokeSyncTypeOnStopCallbacks(bool asServer)
        {
            using (_pm_InvokeSyncTypeOnStopCallbacks.Auto())
            {
                // if (_syncTypes == null)
                //     return;
                foreach (SyncBase item in _syncTypes.Values)
                {
                    try
                    {
                        item.OnStopCallback(asServer);
                    }
                    catch (Exception e)
                    {
                        NetworkManager.LogError(e.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Invokes the OnStart/StopNetwork.
        /// </summary>
        internal void InvokeOnNetwork_Internal(bool start)
        {
            using (_pm_InvokeOnNetwork_Internal.Auto())
            {
                if (start)
                {
                    if (_onStartNetworkCalled)
                        return;

                    if (!gameObject.activeInHierarchy)
                    {
                        NetworkInitialize___Early();
                        NetworkInitialize___Late();
                    }

                    OnStartNetwork_Internal();
                }
                else
                {
                    if (_onStopNetworkCalled)
                        return;
                    OnStopNetwork_Internal();
                }
            }
        }

        internal virtual void OnStartNetwork_Internal()
        {
            using (_pm_OnStartNetwork_Internal.Auto())
            {
                _onStartNetworkCalled = true;
                _onStopNetworkCalled = false;
                try
                {
                    OnStartNetwork();
                }
                catch (Exception e)
                {
                    NetworkManager.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Called when the network has initialized this object. May be called for server or client but will only be called once.
        /// When as host or server this method will run before OnStartServer.
        /// When as client only the method will run before OnStartClient.
        /// </summary>
        public virtual void OnStartNetwork() { }

        internal virtual void OnStopNetwork_Internal()
        {
            using (_pm_OnStopNetwork_Internal.Auto())
            {
                _onStopNetworkCalled = true;
                _onStartNetworkCalled = false;

                try
                {
                    OnStopNetwork();
                }
                catch (Exception e)
                {
                    NetworkManager.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Called when the network is deinitializing this object. May be called for server or client but will only be called once.
        /// When as host or server this method will run after OnStopServer.
        /// When as client only this method will run after OnStopClient.
        /// </summary>
        public virtual void OnStopNetwork() { }

        internal void OnStartServer_Internal()
        {
            using (_pm_OnStartServer_Internal.Auto())
            {
                OnStartServerCalled = true;
                try
                {
                    OnStartServer();
                }
                catch (Exception e)
                {
                    NetworkManager.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Called on the server after initializing this object.
        /// SyncTypes modified before or during this method will be sent to clients in the spawn message.
        /// </summary>
        public virtual void OnStartServer() { }

        internal void OnStopServer_Internal()
        {
            using (_pm_OnStopServer_Internal.Auto())
            {
                OnStartServerCalled = false;
                ReturnRpcLinks();
                try
                {
                    OnStopServer();
                }
                catch (Exception e)
                {
                    NetworkManager.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Called on the server before deinitializing this object.
        /// </summary>
        public virtual void OnStopServer() { }

        internal void OnOwnershipServer_Internal(NetworkConnection prevOwner)
        {
            using (_pm_OnOwnershipServer_Internal.Auto())
            {
                ResetState_Prediction(true);
                try
                {
                    OnOwnershipServer(prevOwner);
                }
                catch (Exception e)
                {
                    NetworkManager.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Called on the server after ownership has changed.
        /// </summary>
        /// <param name = "prevOwner">Previous owner of this object.</param>
        public virtual void OnOwnershipServer(NetworkConnection prevOwner) { }

        /// <summary>
        /// Called on the server after a spawn message for this object has been sent to clients.
        /// Useful for sending remote calls or data to clients.
        /// </summary>
        /// <param name = "connection">Connection the object is being spawned for.</param>
        public virtual void OnSpawnServer(NetworkConnection connection) { }

        /// <summary>
        /// Called on the server before a despawn message for this object has been sent to connection.
        /// Useful for sending remote calls or actions to clients.
        /// </summary>
        public virtual void OnDespawnServer(NetworkConnection connection) { }

        internal void OnStartClient_Internal()
        {
            using (_pm_OnStartClient_Internal.Auto())
            {
                OnStartClientCalled = true;
                try
                {
                    OnStartClient();
                }
                catch (Exception e)
                {
                    NetworkManager.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Called on the client after initializing this object.
        /// </summary>
        public virtual void OnStartClient() { }

        internal void OnStopClient_Internal()
        {
            using (_pm_OnStopClient_Internal.Auto())
            {
                OnStartClientCalled = false;
                try
                {
                    OnStopClient();
                }
                catch (Exception e)
                {
                    NetworkManager.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Called on the client before deinitializing this object.
        /// </summary>
        public virtual void OnStopClient() { }

        internal void OnOwnershipClient_Internal(NetworkConnection prevOwner)
        {
            using (_pm_OnOwnershipClient_Internal.Auto())
            {
                // If losing or gaining ownership then clear replicate cache.
                if (IsOwner || prevOwner == LocalConnection)
                {
                    ResetState_Prediction(false);
                }

                try
                {
                    OnOwnershipClient(prevOwner);
                }
                catch (Exception e)
                {
                    NetworkManager.LogError(e.ToString());
                }
            }
        }

        /// <summary>
        /// Called on the client after gaining or losing ownership.
        /// </summary>
        /// <param name = "prevOwner">Previous owner of this object.</param>
        public virtual void OnOwnershipClient(NetworkConnection prevOwner) { }
    }
}