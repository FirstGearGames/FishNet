using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called when a client is removed from the server using Kick. This is invoked before the client is disconnected.
        /// NetworkConnection when available, clientId, and KickReason are provided.
        /// </summary>
        public event Action<NetworkConnection, int, KickReason> OnClientKick;
        #endregion

        /// <summary>
        /// Returns true if only one server is started.
        /// </summary>
        /// <returns></returns>
        public bool OneServerStarted()
        {
            int startedCount = 0;
            TransportManager tm = NetworkManager.TransportManager;
            //If using multipass check all transports.
            if (tm.Transport is Multipass mp)
            {

                foreach (Transport t in mp.Transports)
                {
                    //Another transport is started, no need to load start scenes again.
                    if (t.GetConnectionState(true) == LocalConnectionState.Started)
                        startedCount++;
                }
            }
            //Not using multipass.
            else
            {
                if (tm.Transport.GetConnectionState(true) == LocalConnectionState.Started)
                    startedCount = 1;
            }

            return (startedCount == 1);
        }

        /// <summary>
        /// Returns true if any server socket is in the started state.
        /// </summary>
        /// <param name="excludedIndex">When set the transport on this index will be ignored. This value is only used with Multipass.</param>
        /// <returns></returns>
        public bool AnyServerStarted(int? excludedIndex = null)
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
                    if (t.GetConnectionState(true) == LocalConnectionState.Started)
                        return true;
                }
            }
            //Not using multipass.
            else
            {
                return (tm.Transport.GetConnectionState(true) == LocalConnectionState.Started);
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
        public void Spawn(GameObject go, NetworkConnection ownerConnection = null, UnityEngine.SceneManagement.Scene scene = default)
        {
            if (go == null)
            {
                NetworkManager.LogWarning($"GameObject cannot be spawned because it is null.");
                return;
            }

            NetworkObject nob = go.GetComponent<NetworkObject>();
            Spawn(nob, ownerConnection, scene);
        }


        /// <summary>
        /// Spawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="nob">MetworkObject instance to spawn.</param>
        /// <param name="ownerConnection">Connection to give ownership to.</param>
        public void Spawn(NetworkObject nob, NetworkConnection ownerConnection = null, UnityEngine.SceneManagement.Scene scene = default)
        {
            Objects.Spawn(nob, ownerConnection, scene);
        }

        /// <summary>
        /// Despawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="go">GameObject instance to despawn.</param>
        /// <param name="cacheOnDespawnOverride">Overrides the default DisableOnDespawn value for this single despawn. Scene objects will never be destroyed.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Despawn(GameObject go, DespawnType? despawnType = null)
        {
            if (go == null)
            {
                NetworkManager.LogWarning($"GameObject cannot be despawned because it is null.");
                return;
            }

            NetworkObject nob = go.GetComponent<NetworkObject>();
            Despawn(nob, despawnType);
        }

        /// <summary>
        /// Despawns an object over the network. Can only be called on the server.
        /// </summary>
        /// <param name="networkObject">NetworkObject instance to despawn.</param>
        /// <param name="despawnType">Despawn override type.</param>
        public void Despawn(NetworkObject networkObject, DespawnType? despawnType = null)
        {
            DespawnType resolvedDespawnType = (despawnType == null)
                ? networkObject.GetDefaultDespawnType()
                : despawnType.Value;
            Objects.Despawn(networkObject, resolvedDespawnType, true);
        }

        /// <summary>
        /// Kicks a connection immediately while invoking OnClientKick.
        /// </summary>
        /// <param name="conn">Client to kick.</param>
        /// <param name="kickReason">Reason client is being kicked.</param>
        /// <param name="loggingType">How to print logging as.</param>
        /// <param name="log">Optional message to be debug logged.</param>
        public void Kick(NetworkConnection conn, KickReason kickReason, LoggingType loggingType = LoggingType.Common, string log = "")
        {
            if (!conn.IsValid)
                return;

            OnClientKick?.Invoke(conn, conn.ClientId, kickReason);
            if (conn.IsActive)
                conn.Disconnect(true);

            if (!string.IsNullOrEmpty(log))
                NetworkManager.Log(loggingType, log);
        }

        /// <summary>
        /// Kicks a connection immediately while invoking OnClientKick.
        /// </summary>
        /// <param name="clientId">ClientId to kick.</param>
        /// <param name="kickReason">Reason client is being kicked.</param>
        /// <param name="loggingType">How to print logging as.</param>
        /// <param name="log">Optional message to be debug logged.</param>
        public void Kick(int clientId, KickReason kickReason, LoggingType loggingType = LoggingType.Common, string log = "")
        {
            OnClientKick?.Invoke(null, clientId, kickReason);
            NetworkManager.TransportManager.Transport.StopConnection(clientId, true);
            if (!string.IsNullOrEmpty(log))
                NetworkManager.Log(loggingType, log);
        }

        /// <summary>
        /// Kicks a connection immediately while invoking OnClientKick.
        /// </summary>
        /// <param name="conn">Client to kick.</param>
        /// <param name="reader">Reader to clear before kicking.</param>
        /// <param name="kickReason">Reason client is being kicked.</param>
        /// <param name="loggingType">How to print logging as.</param>
        /// <param name="log">Optional message to be debug logged.</param>
        public void Kick(NetworkConnection conn, Reader reader, KickReason kickReason, LoggingType loggingType = LoggingType.Common, string log = "")
        {
            reader.Clear();
            Kick(conn, kickReason, loggingType, log);
        }
    }


}
