using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        /// <summary>
        /// Returns true if only one server is started.
        /// </summary>
        /// <returns></returns>
        internal bool OneServerStarted()
        {
            int startedCount = 0;
            TransportManager tm = NetworkManager.TransportManager;
            //If using multipass check all transports.
            if (tm.Transport is Multipass mp)
            {
                
                foreach (Transport t in mp.Transports)
                {
                    //Another transport is started, no need to load start scenes again.
                    if (t.GetConnectionState(true) == LocalConnectionStates.Started)
                        startedCount++;
                }
            }
            //Not using multipass.
            else
            {
                if (tm.Transport.GetConnectionState(true) == LocalConnectionStates.Started)
                    startedCount = 1;
            }

            return (startedCount == 1);
        }

        /// <summary>
        /// Returns true if any server socket is in the started state.
        /// </summary>
        /// <param name="excludedIndex">When set the transport on this index will be ignored. This value is only used with Multipass.</param>
        /// <returns></returns>
        internal bool AnyServerStarted(int? excludedIndex = null)
        {
            TransportManager tm = NetworkManager.TransportManager;
            //If using multipass check all transports.
            if (tm.Transport is Multipass mp)
            {
                //Get transport which had state changed.
                Transport excludedTransport = (excludedIndex == null) ? null : mp.GetTransport(excludedIndex.Value);

                foreach (Transport t in mp.Transports)
                {
                    /* Skip t if is the transport that had it's state changed.
                     * We are looking for other transports already in started. */
                    if (t == excludedTransport)
                        continue;
                    //Another transport is started, no need to load start scenes again.
                    if (t.GetConnectionState(true) == LocalConnectionStates.Started)
                        return true;
                }
            }
            //Not using multipass.
            else
            {
                return (tm.Transport.GetConnectionState(true) == LocalConnectionStates.Started);
            }

            //Fall through, none started.
            return false;
        }

        /// <summary>
        /// Spawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="go">GameObject instance to spawn.</param>
        /// <param name="ownerConnection">Connection to give ownership to.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Spawn(GameObject go, NetworkConnection ownerConnection = null)
        {
            if (!CanSpawnOrDespawn(true))
                return;
            if (go == null)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"GameObject cannot be spawned because it is null.");
                return;
            }

            NetworkObject nob = go.GetComponent<NetworkObject>();
            Spawn(nob, ownerConnection);
        }


        /// <summary>
        /// Spawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="nob">MetworkObject instance to spawn.</param>
        /// <param name="ownerConnection">Connection to give ownership to.</param>
        /// <param name="synchronizeParent">True to synchronize the parent object in the spawn message. The parent must have a NetworkObject or NetworkBehaviour component for this to work.</param>
        public void Spawn(NetworkObject nob, NetworkConnection ownerConnection = null, bool synchronizeParent = true)
        {
            if (!CanSpawnOrDespawn(true))
                return;
            if (nob == null)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"NetworkObject cannot be spawned because it is null.");
                return;
            }

            Objects.Spawn(nob, ownerConnection, synchronizeParent);
        }


        /// <summary>
        /// Returns if Spawn can be called.
        /// </summary>
        /// <param name="warn">True to warn if not able to execute spawn or despawn.</param>
        /// <returns></returns>
        private bool CanSpawnOrDespawn(bool warn)
        {
            bool canLog = (warn && NetworkManager.CanLog(LoggingType.Warning));
            if (!Started)
            {
                if (canLog)
                    Debug.Log($"The server must be active to spawn or despawn networked objects.");
                return false;
            }

            return true;
        }


        /// <summary>
        /// Despawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="go">GameObject instance to despawn.</param>
        public void Despawn(GameObject go)
        {
            if (!CanSpawnOrDespawn(true))
                return;
            if (go == null)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"GameObject cannot be despawned because it is null.");
                return;
            }

            NetworkObject nob = go.GetComponent<NetworkObject>();
            Despawn(nob);
        }

        /// <summary>
        /// Despawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="networkObject">NetworkObject instance to despawn.</param>
        public void Despawn(NetworkObject networkObject)
        {
            if (!CanSpawnOrDespawn(true))
                return;
            if (networkObject == null)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"NetworkObject cannot be despawned because it is null.");
                return;
            }
            Objects.Despawn(networkObject, true);
        }
    }


}
