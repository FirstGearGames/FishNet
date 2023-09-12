﻿#if UNITY_2020_3_OR_NEWER && UNITY_EDITOR_WIN
using FishNet.CodeAnalysis.Annotations;
#endif
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Object.Synchronizing.Internal;
using System;
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

        /// <summary>
        /// This <see cref="Action"/> will be invoked after <see cref="OnStartNetwork"/> is called.
        /// </summary>
        public event Action OnStartedOnNetwork;

        /// <summary>
        /// This <see cref="Action"/> will be invoked after <see cref="OnStartClient"/> is called.
        /// </summary>
        public event Action OnStartedOnClient;

        /// <summary>
        /// This <see cref="Action"/> will be invoked after <see cref="OnStartServer"/> is called.
        /// </summary>
        public event Action OnStartedOnServer;
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

        /// <summary>
        /// Invokes OnStartXXXX for synctypes, letting them know the NetworkBehaviour start cycle has been completed.
        /// </summary>
        internal void InvokeSyncTypeOnStartCallbacks(bool asServer)
        {
            foreach (SyncBase item in _syncVars.Values)
                item.OnStartCallback(asServer);
            foreach (SyncBase item in _syncObjects.Values)
                item.OnStartCallback(asServer);
        }

        /// <summary>
        /// Invokes OnStopXXXX for synctypes, letting them know the NetworkBehaviour stop cycle is about to start.
        /// </summary>
        internal void InvokeSyncTypeOnStopCallbacks(bool asServer)
        {
            foreach (SyncBase item in _syncVars.Values)
                item.OnStopCallback(asServer);
            foreach (SyncBase item in _syncObjects.Values)
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
        internal void OnStartNetwork_Internal()
        {
            _onStartNetworkCalled = true;
            _onStopNetworkCalled = false;
            OnStartNetwork();
            OnStartedOnNetwork?.Invoke();
        }
        /// <summary>
        /// Called when the network has initialized this object. May be called for server or client but will only be called once.
        /// When as host or server this method will run before OnStartServer. 
        /// When as client only the method will run before OnStartClient.
        /// </summary>
        public virtual void OnStartNetwork() { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnStopNetwork_Internal()
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
            OnStartedOnServer?.Invoke();
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
            //When switching ownership always clear replicate cache on server.
#if !PREDICTION_V2
            ClearReplicateCache_Virtual(true);
#else
            ClearReplicateCache();
#endif
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
            OnStartedOnClient?.Invoke();
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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnOwnershipClient_Internal(NetworkConnection prevOwner)
        {
            //If losing or gaining ownership then clear replicate cache.
            if (IsOwner || prevOwner == LocalConnection)
            {
#if !PREDICTION_V2
                ClearReplicateCache_Virtual(false);
#else
                ClearReplicateCache();
#endif
            }
            OnOwnershipClient(prevOwner);
        }
        /// <summary>
        /// Called on the client after gaining or losing ownership.
        /// </summary>
        /// <param name="prevOwner">Previous owner of this object.</param>
        public virtual void OnOwnershipClient(NetworkConnection prevOwner) { }

    }


}