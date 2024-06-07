﻿using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using System.Runtime.CompilerServices;
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
        /// <param name="connection">Connection receiving the payload. When sending to the server connection.IsValid will return false.</param>
        public virtual void WritePayload(NetworkConnection connection, Writer writer) { }
        /// <summary>
        /// Called before network start callbacks, but after the object is initialized with network values. This may be used to read information from predicted spawning, or simply have values set before initialization without depending on SyncTypes.
        /// </summary>
        /// <param name="connection">Connection sending the payload. When coming from the server connection.IsValid will return false.</param>
        public virtual void ReadPayload(NetworkConnection connection, Reader reader) { }

        /// <summary>
        /// Invokes OnStartXXXX for synctypes, letting them know the NetworkBehaviour start cycle has been completed.
        /// </summary>
        internal void InvokeSyncTypeOnStartCallbacks(bool asServer)
        {
            foreach (SyncBase item in _syncTypes.Values)
                item.OnStartCallback(asServer);
        }

        /// <summary>
        /// Invokes OnStopXXXX for synctypes, letting them know the NetworkBehaviour stop cycle is about to start.
        /// </summary>
        internal void InvokeSyncTypeOnStopCallbacks(bool asServer)
        {
            foreach (SyncBase item in _syncTypes.Values)
                item.OnStopCallback(asServer);
        }


        /// <summary>
        /// Invokes the OnStart/StopNetwork.
        /// </summary>
        /// <param name="start"></param>
        internal void InvokeOnNetwork(bool start)
        {
            if (start)
            {
                if (_onStartNetworkCalled)
                    return;
                OnStartNetwork_Internal();
            }
            else
            {
                if (_onStopNetworkCalled)
                    return;
                OnStopNetwork_Internal();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal virtual void OnStartNetwork_Internal()
        {
            _onStartNetworkCalled = true;
            _onStopNetworkCalled = false;
            OnStartNetwork();
        }
        /// <summary>
        /// Called when the network has initialized this object. May be called for server or client but will only be called once.
        /// When as host or server this method will run before OnStartServer. 
        /// When as client only the method will run before OnStartClient.
        /// </summary>
        public virtual void OnStartNetwork() { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal virtual void OnStopNetwork_Internal()
        {
            _onStopNetworkCalled = true;
            _onStartNetworkCalled = false;
            OnStopNetwork();
        }
        /// <summary>
        /// Called when the network is deinitializing this object. May be called for server or client but will only be called once.
        /// When as host or server this method will run after OnStopServer.
        /// When as client only this method will run after OnStopClient.
        /// </summary>
        public virtual void OnStopNetwork() { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnStartServer_Internal()
        {
            OnStartServerCalled = true;
            OnStartServer();
        }
        /// <summary>
        /// Called on the server after initializing this object.
        /// SyncTypes modified before or during this method will be sent to clients in the spawn message.
        /// </summary> 
        public virtual void OnStartServer() { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnStopServer_Internal()
        {
            OnStartServerCalled = false;
            ReturnRpcLinks();
            OnStopServer();
        }
        /// <summary>
        /// Called on the server before deinitializing this object.
        /// </summary>
        public virtual void OnStopServer() { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnOwnershipServer_Internal(NetworkConnection prevOwner)
        {
#if !PREDICTION_1
            ResetPredictionTicks();
#endif
            CallClearReplicateCache(true);
            OnOwnershipServer(prevOwner);
        }
        /// <summary>
        /// Called on the server after ownership has changed.
        /// </summary>
        /// <param name="prevOwner">Previous owner of this object.</param>
        public virtual void OnOwnershipServer(NetworkConnection prevOwner) { }

        /// <summary>
        /// Called on the server after a spawn message for this object has been sent to clients.
        /// Useful for sending remote calls or data to clients.
        /// </summary>
        /// <param name="connection">Connection the object is being spawned for.</param>
        public virtual void OnSpawnServer(NetworkConnection connection) { }
        /// <summary>
        /// Called on the server before a despawn message for this object has been sent to connection.
        /// Useful for sending remote calls or actions to clients.
        /// </summary>
        public virtual void OnDespawnServer(NetworkConnection connection) { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnStartClient_Internal()
        {
            OnStartClientCalled = true;
            OnStartClient();
        }
        /// <summary>
        /// Called on the client after initializing this object.
        /// </summary>
        public virtual void OnStartClient() { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnStopClient_Internal()
        {
            OnStartClientCalled = false;
            OnStopClient();
        }
        /// <summary>
        /// Called on the client before deinitializing this object.
        /// </summary>
        public virtual void OnStopClient() { }

#if !PREDICTION_1

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnOwnershipClient_Internal(NetworkConnection prevOwner)
        {
            //If losing or gaining ownership then clear replicate cache.
            if (IsOwner || prevOwner == LocalConnection)
                CallClearReplicateCache(false);

            _lastReadReconcileTick = 0;
            OnOwnershipClient(prevOwner);
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnOwnershipClient_Internal(NetworkConnection prevOwner)
        {
            //If losing or gaining ownership then clear replicate cache.
            if (IsOwner || prevOwner == LocalConnection)
                CallClearReplicateCache(false);

            OnOwnershipClient(prevOwner);
        }
#endif
        /// <summary>
        /// Called on the client after gaining or losing ownership.
        /// </summary>
        /// <param name="prevOwner">Previous owner of this object.</param>
        public virtual void OnOwnershipClient(NetworkConnection prevOwner) { }

        /// <summary>
        /// Calls ClearReplicateCache for prediction v1 or v2.
        /// </summary>
        private void CallClearReplicateCache(bool asServer)
        {
#if PREDICTION_1
            ClearReplicateCache_Virtual(asServer);
#else
            ClearReplicateCache();
#endif
        }
    }


}