using FishNet.Connection;
using UnityEngine;

namespace FishNet.Object
{
    public sealed partial class NetworkObject : MonoBehaviour
    {
        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        private void InitializeCallbacks(bool asServer)
        {
            /* Note: When invoking OnOwnership here previous owner will
             * always be an empty connection, since the object is just
             * now initializing. */

            if (!asServer)
                ClientInitialized = true;

            //Set that client or server is active before callbacks.
            SetActiveStatus(true, asServer);

            //Invoke OnStartNetwork.
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeOnNetwork(true);            

            //As server.
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartServer();
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].InvokeSyncTypeCallbacks(true);

                if (Owner.IsValid)
                {
                    for (int i = 0; i < NetworkBehaviours.Length; i++)
                        NetworkBehaviours[i].OnOwnershipServer(FishNet.Managing.NetworkManager.EmptyConnection);
                }
            }
            //As client.
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartClient();
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].InvokeSyncTypeCallbacks(false);

                if (IsOwner)
                {
                    for (int i = 0; i < NetworkBehaviours.Length; i++)
                        NetworkBehaviours[i].OnOwnershipClient(FishNet.Managing.NetworkManager.EmptyConnection);
                }
            }
        }

        /// <summary>
        /// Invokes events to be called after OnServerStart.
        /// This is made one method to save instruction calls.
        /// </summary>
        /// <param name=""></param>
        internal void InvokePostOnServerStart(NetworkConnection conn)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].OnSendBufferedRpcs(conn);

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
        private void InvokeStopCallbacks(bool asServer)
        {
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopServer();
            }
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopClient();
            }

            /* Invoke OnStopNetwork if server is calling
            * or if client and not as server. */
            if (asServer || (!asServer && !IsServer))
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].InvokeOnNetwork(false);
            }

            if (asServer)
                IsServer = false;
            else
                IsClient = false;
        }

        /// <summary>
        /// Invokes OnOwnership callbacks.
        /// </summary>
        /// <param name="prevOwner"></param>
        private void InvokeOwnership(NetworkConnection prevOwner, bool asServer)
        {
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipServer(prevOwner);
            }
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipClient(prevOwner);
            }
        }
    }

}

