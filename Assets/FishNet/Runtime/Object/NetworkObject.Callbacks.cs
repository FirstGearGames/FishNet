using FishNet.Connection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
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
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipServer_Internal(FishNet.Managing.NetworkManager.EmptyConnection);
            }
            //As client.
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartClient_Internal();
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipClient_Internal(FishNet.Managing.NetworkManager.EmptyConnection);
            }

            if (invokeSyncTypeCallbacks)
                InvokeOnStartSyncTypeCallbacks(true);
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
        internal void InvokeStopCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStopCallbacks(asServer);

            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopServer_Internal();
            }
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopClient_Internal();
            }

            /* Several conditions determine if OnStopNetwork can
             * be called.
             * 
             * - If asServer and pending destroy from clientHost.
             * - If !asServer and not ServerInitialized. */
            bool callStopNetwork;
            if (asServer)
            {
                if (!IsClientStarted)
                    callStopNetwork = true;
                else
                    callStopNetwork = (ServerManager.Objects.GetFromPending(ObjectId) == null);
            }
            else
            {
                /* When not as server only perform OnStopNetwork if
                 * not initialized for the server. The object could be
                 * server initialized if it were spawned, despawned, then spawned again
                 * before client ran this method. */
                callStopNetwork = !IsServerInitialized;
            }
            if (callStopNetwork)
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
#if PREDICTION_V2
                ResetReplicateTick();
#endif
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

