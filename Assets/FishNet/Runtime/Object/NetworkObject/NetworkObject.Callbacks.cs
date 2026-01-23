using FishNet.Connection;
using System.Runtime.CompilerServices;
using System;
using FishNet.Serializing;
using UnityEngine;
using Unity.Profiling;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        #region Types

        public delegate void NetworkObjectCallback(NetworkObject nb);
        
        public delegate void NetworkObjectInvokeCallback(NetworkObject nb, bool asServer, bool invokeSyncTypeCallbacks);

        #endregion
        
        #region Private.
        /// <summary>
        /// True if OnStartServer was called.
        /// </summary>
        private bool _onStartServerCalled;
        /// <summary>
        /// True if OnStartClient was called.
        /// </summary>
        private bool _onStartClientCalled;
        /// <summary>
        /// True if OnStartSyncTypeCallbacks was called.
        /// </summary>
        private bool _onStartSyncTypeCallbacksCalled;

        /// <summary>
        /// True if OnStartServer was called.
        /// </summary>
        public bool OnStartServerCalled
        {
            get => _onStartServerCalled;
            private set
            {
                if (_onStartServerCalled != value)
                {
                    _onStartServerCalled = value;
                    if (value)
                    {
                        using (_pm_OnStartServerEvent.Auto())
                            OnStartServerEvent?.Invoke(this);
                    }
                    else
                    {
                        using (_pm_OnStopServerEvent.Auto())
                            OnStopServerEvent?.Invoke(this);
                    }
                }
            }
        }
        
        /// <summary>
        /// True if OnStartClient was called.
        /// </summary>
        public bool OnStartClientCalled
        {
            get => _onStartClientCalled;
            private set
            {
                if (_onStartClientCalled != value)
                {
                    _onStartClientCalled = value;
                    if (value)
                    {
                        using (_pm_OnStartClientEvent.Auto())
                            OnStartClientEvent?.Invoke(this);
                    }
                    else
                    {
                        using (_pm_OnStopClientEvent.Auto())
                            OnStopClientEvent?.Invoke(this);
                    }
                }
            }
        }
        
        /// <summary>
        /// True if OnStartSyncTypeCallbacks was called.
        /// </summary>
        public bool OnStartSyncTypeCallbacksCalled
        {
            get => _onStartSyncTypeCallbacksCalled;
            private set
            {
                if (_onStartSyncTypeCallbacksCalled != value)
                {
                    _onStartSyncTypeCallbacksCalled = value;
                    if (value)
                    {
                        using (_pm_OnStartSyncTypeCallbacksEvent.Auto())
                            OnStartSyncTypeCallbacks?.Invoke(this);
                    }
                    else
                    {
                        using (_pm_OnStopSyncTypeCallbacksEvent.Auto())
                            OnStopSyncTypeCallbacks?.Invoke(this);
                    }
                }
            }
        }

        public event NetworkObjectCallback OnStartServerEvent;
        public event NetworkObjectCallback OnStopServerEvent;
        public event NetworkObjectCallback OnStartClientEvent;
        public event NetworkObjectCallback OnStopClientEvent;
        public event NetworkObjectCallback OnStartSyncTypeCallbacks;
        public event NetworkObjectCallback OnStopSyncTypeCallbacks;
        public event NetworkObjectCallback OnServerInitializedEvent;
        public event NetworkObjectCallback OnClientInitializedEvent;
        public event NetworkObjectCallback OnServerDeinitializedEvent;
        public event NetworkObjectCallback OnClientDeinitializedEvent;
        public event NetworkObjectInvokeCallback PreInvokeStartCallbacks;
        public event NetworkObjectInvokeCallback PostInvokeStartCallbacks;
        public event NetworkObjectInvokeCallback PreInvokeStopCallbacks;
        public event NetworkObjectInvokeCallback PostInvokeStopCallbacks;

        #region Profiling.
        private static readonly ProfilerMarker _pm_OnStartServerEvent =
            new("NetworkObject.OnStartServerEvent");
        private static readonly ProfilerMarker _pm_OnStopServerEvent =
            new("NetworkObject.OnStopServerEvent");
        private static readonly ProfilerMarker _pm_OnStartClientEvent =
            new("NetworkObject.OnStartClientEvent");
        private static readonly ProfilerMarker _pm_OnStopClientEvent =
            new("NetworkObject.OnStopClientEvent");
        private static readonly ProfilerMarker _pm_OnStartSyncTypeCallbacksEvent =
            new("NetworkObject.OnStartSyncTypeCallbacks");
        private static readonly ProfilerMarker _pm_OnStopSyncTypeCallbacksEvent =
            new("NetworkObject.OnStopSyncTypeCallbacks");
        private static readonly ProfilerMarker _pm_OnServerInitializedEvent =
            new("NetworkObject.OnServerInitializedEvent");
        private static readonly ProfilerMarker _pm_OnClientInitializedEvent =
            new("NetworkObject.OnClientInitializedEvent");
        private static readonly ProfilerMarker _pm_OnServerDeinitializedEvent =
            new("NetworkObject.OnServerDeinitializedEvent");
        private static readonly ProfilerMarker _pm_OnClientDeinitializedEvent =
            new("NetworkObject.OnClientDeinitializedEvent");
        private static readonly ProfilerMarker _pm_PreInvokeStartCallbacksEvent =
            new("NetworkObject.PreInvokeStartCallbacks");
        private static readonly ProfilerMarker _pm_PostInvokeStartCallbacksEvent =
            new("NetworkObject.PostInvokeStartCallbacks");
        private static readonly ProfilerMarker _pm_PreInvokeStopCallbacksEvent =
            new("NetworkObject.PreInvokeStopCallbacks");
        private static readonly ProfilerMarker _pm_PostInvokeStopCallbacksEvent =
            new("NetworkObject.PostInvokeStopCallbacks");

        #endregion
        
        #endregion

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        private void InvokeStartCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
        {
            using (_pm_PreInvokeStartCallbacksEvent.Auto())
                PreInvokeStartCallbacks?.Invoke(this, asServer, invokeSyncTypeCallbacks);
            
            /* Note: When invoking OnOwnership here previous owner will
             * always be an empty connection, since the object is just
             * now initializing. */

            // Invoke OnStartNetwork.
            bool invokeOnNetwork = asServer || IsServerOnlyStarted || IsClientOnlyInitialized;
            if (invokeOnNetwork)
            {
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].InvokeOnNetwork_Internal(start: true);
            }

            //As server.
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].OnStartServer_Internal();
                OnStartServerCalled = true;
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].OnOwnershipServer_Internal(Managing.NetworkManager.EmptyConnection);
            }
            //As client.
            else
            {
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].OnStartClient_Internal();
                OnStartClientCalled = true;
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].OnOwnershipClient_Internal(Managing.NetworkManager.EmptyConnection);
            }

            if (invokeSyncTypeCallbacks)
            {
                InvokeOnStartSyncTypeCallbacks(true);
                OnStartSyncTypeCallbacksCalled = true;
            }

            InvokeStartCallbacks_Prediction(asServer);
            
            using (_pm_PostInvokeStartCallbacksEvent.Auto())
                PostInvokeStartCallbacks?.Invoke(this, asServer, invokeSyncTypeCallbacks);
        }

        /// <summary>
        /// Invokes OnStartXXXX for synctypes, letting them know the NetworkBehaviour start cycle has been completed.
        /// </summary>
        internal void InvokeOnStartSyncTypeCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Count; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStartCallbacks(asServer);
        }

        /// <summary>
        /// Invokes OnStopXXXX for synctypes, letting them know the NetworkBehaviour stop cycle is about to start.
        /// </summary>
        internal void InvokeOnStopSyncTypeCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Count; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStopCallbacks(asServer);
        }

        /// <summary>
        /// Invokes events to be called after OnServerStart.
        /// This is made one method to save instruction calls.
        /// </summary>
        /// <param name = ""></param>
        internal void OnSpawnServer(NetworkConnection conn)
        {
            for (int i = 0; i < NetworkBehaviours.Count; i++)
                NetworkBehaviours[i].SendBufferedRpcs(conn);

            for (int i = 0; i < NetworkBehaviours.Count; i++)
                NetworkBehaviours[i].OnSpawnServer(conn);
        }

        /// <summary>
        /// Called on the server before it sends a despawn message to a client.
        /// </summary>
        /// <param name = "conn">Connection spawn was sent to.</param>
        internal void InvokeOnServerDespawn(NetworkConnection conn)
        {
            for (int i = 0; i < NetworkBehaviours.Count; i++)
                NetworkBehaviours[i].OnDespawnServer(conn);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Invokes OnStop callbacks.
        /// </summary>
        internal void InvokeStopCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
        {
            using (_pm_PreInvokeStopCallbacksEvent.Auto())
                PreInvokeStopCallbacks?.Invoke(this, asServer, invokeSyncTypeCallbacks);
            
            InvokeStopCallbacks_Prediction(asServer);

            if (invokeSyncTypeCallbacks)
            {
                InvokeOnStopSyncTypeCallbacks(asServer);
                OnStartSyncTypeCallbacksCalled = false;
            }

            if (asServer && OnStartServerCalled)
            {
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].OnStopServer_Internal();

                if (!OnStartClientCalled)
                    InvokeOnNetwork();

                OnStartServerCalled = false;
            }
            else if (!asServer && OnStartClientCalled)
            {
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].OnStopClient_Internal();

                /* Only invoke OnNetwork if server start isn't called, otherwise
                 * that means this is still intialized on the server. This would
                 * happen if the object despawned for the clientHost but not on the
                 * server. */
                if (!OnStartServerCalled)
                    InvokeOnNetwork();

                OnStartClientCalled = false;
            }
            
            using (_pm_PostInvokeStopCallbacksEvent.Auto())
                PostInvokeStopCallbacks?.Invoke(this, asServer, invokeSyncTypeCallbacks);

            void InvokeOnNetwork()
            {
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].InvokeOnNetwork_Internal(start: false);
            }
        }

        /// <summary>
        /// Invokes OnOwnership callbacks when ownership changes.
        /// This is not to be called when assigning ownership during a spawn message.
        /// </summary>
        private void InvokeManualOwnershipChange(NetworkConnection prevOwner, bool asServer)
        {
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Count; i++)
                    NetworkBehaviours[i].OnOwnershipServer_Internal(prevOwner);

                WriteSyncTypesForManualOwnershipChange(prevOwner);
            }
            else
            {
                /* If local client is owner and not server then only
                 * invoke if the prevOwner is different. This prevents
                 * the owner change callback from happening twice when
                 * using TakeOwnership.
                 *
                 * Further explained, the TakeOwnership sets local client
                 * as owner client-side, which invokes the OnOwnership method.
                 * Then when the server approves the owner change it would invoke
                 * again, which is not needed. */
                bool blockInvoke = IsOwner && !IsServerStarted && prevOwner == Owner;
                if (!blockInvoke)
                {
                    for (int i = 0; i < NetworkBehaviours.Count; i++)
                        NetworkBehaviours[i].OnOwnershipClient_Internal(prevOwner);
                }
            }
        }
    }
}
