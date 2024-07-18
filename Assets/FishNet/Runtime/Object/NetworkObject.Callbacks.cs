using FishNet.Connection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// True if OnStartServer was called.
        /// </summary>
        private bool _onStartServerCalled;
        /// <summary>
        /// True if OnStartClient was called.
        /// </summary>
        private bool _onStartClientCalled;
        #endregion

        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvokeStartCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
        {
            /* Note: When invoking OnOwnership here previous owner will
             * always be an empty connection, since the object is just
             * now initializing. */

            //Invoke OnStartNetwork.
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeOnNetwork(true);

            //As server.
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartServer_Internal();
                _onStartServerCalled = true;
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipServer_Internal(FishNet.Managing.NetworkManager.EmptyConnection);
            }
            //As client.
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartClient_Internal();
                _onStartClientCalled = true;
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipClient_Internal(FishNet.Managing.NetworkManager.EmptyConnection);
            }

            if (invokeSyncTypeCallbacks)
                InvokeOnStartSyncTypeCallbacks(true);

            InvokeStartCallbacks_Prediction(asServer);
        }


        /// <summary>
        /// Invokes OnStartXXXX for synctypes, letting them know the NetworkBehaviour start cycle has been completed.
        /// </summary>
        internal void InvokeOnStartSyncTypeCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStartCallbacks(asServer);
        }

        /// <summary>
        /// Invokes OnStopXXXX for synctypes, letting them know the NetworkBehaviour stop cycle is about to start.
        /// </summary>
        internal void InvokeOnStopSyncTypeCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStopCallbacks(asServer);
        }

        /// <summary>
        /// Invokes events to be called after OnServerStart.
        /// This is made one method to save instruction calls.
        /// </summary>
        /// <param name=""></param>
        internal void OnSpawnServer(NetworkConnection conn)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].SendBufferedRpcs(conn);

            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].OnSpawnServer(conn);
        }

        /// <summary>
        /// Called on the server before it sends a despawn message to a client.
        /// </summary>
        /// <param name="conn">Connection spawn was sent to.</param>
        internal void InvokeOnServerDespawn(NetworkConnection conn)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].OnDespawnServer(conn);
        }

        /// <summary>
        /// Invokes OnStop callbacks.
        /// </summary>
        /// <param name="asServer"></param>
        internal void InvokeStopCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
        {
            InvokeStopCallbacks_Prediction(asServer);

            if (invokeSyncTypeCallbacks)
                InvokeOnStopSyncTypeCallbacks(asServer);

            bool invokeOnNetwork = (!asServer || (asServer && !_onStartClientCalled));
            if (asServer && _onStartServerCalled)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopServer_Internal();
                _onStartServerCalled = false;
            }
            else if (!asServer && _onStartClientCalled)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopClient_Internal();
                _onStartClientCalled = false;
            }

            if (invokeOnNetwork)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].InvokeOnNetwork(false);
            }


        }

        /// <summary>
        /// Invokes OnOwnership callbacks when ownership changes.
        /// This is not to be called when assigning ownership during a spawn message.
        /// </summary>
        private void InvokeOwnershipChange(NetworkConnection prevOwner, bool asServer)
        {            
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipServer_Internal(prevOwner);
                //Also write owner syncTypes if there is an owner.
                if (Owner.IsValid)
                {
                    for (int i = 0; i < NetworkBehaviours.Length; i++)
                        NetworkBehaviours[i].WriteDirtySyncTypes(true, true, true);
                }
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
                bool blockInvoke = ((IsOwner && !IsServerStarted) && (prevOwner == Owner));
                if (!blockInvoke)
                {
                    for (int i = 0; i < NetworkBehaviours.Length; i++)
                        NetworkBehaviours[i].OnOwnershipClient_Internal(prevOwner);
                }
            }
        }
    }

}

