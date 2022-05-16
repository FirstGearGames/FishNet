using FishNet.Authenticating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Example.Authenticating
{

    /// <summary>
    /// This is an example of a password authenticator.
    /// Never send passwords without encryption.
    /// </summary>
    public class PasswordAuthenticator : Authenticator
    {
        #region Public.
        /// <summary>
        /// Called when authenticator has concluded a result for a connection. Boolean is true if authentication passed, false if failed.
        /// Server listens for this event automatically.
        /// </summary>
        public override event Action<NetworkConnection, bool> OnAuthenticationResult;
        #endregion

        #region Serialized.
        /// <summary>
        /// Password to authenticate.
        /// </summary>
        [Tooltip("Password to authenticate.")]
        [SerializeField]
        private string _password = "HelloWorld";
        #endregion

        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);

            //Listen for connection state change as client.
            base.NetworkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
            //Listen for broadcast from client. Be sure to set requireAuthentication to false.
            base.NetworkManager.ServerManager.RegisterBroadcast<PasswordBroadcast>(OnPasswordBroadcast, false);
            //Listen to response from server.
            base.NetworkManager.ClientManager.RegisterBroadcast<ResponseBroadcast>(OnResponseBroadcast);
        }

        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
        {
            /* If anything but the started state then exit early.
             * Only try to authenticate on started state. The server
            * doesn't have to send an authentication request before client
            * can authenticate, that is entirely optional and up to you. In this
            * example the client tries to authenticate soon as they connect. */
            if (args.ConnectionState != LocalConnectionStates.Started)
                return;

            PasswordBroadcast pb = new PasswordBroadcast()
            {
                Password = _password
            };

            base.NetworkManager.ClientManager.Broadcast(pb);
        }


        /// <summary>
        /// Received on server when a client sends the password broadcast message.
        /// </summary>
        /// <param name="conn">Connection sending broadcast.</param>
        /// <param name="pb"></param>
        private void OnPasswordBroadcast(NetworkConnection conn, PasswordBroadcast pb)
        {
            /* If client is already authenticated this could be an attack. Connections
             * are removed when a client disconnects so there is no reason they should
             * already be considered authenticated. */
            if (conn.Authenticated)
            {
                conn.Disconnect(true);
                return;
            }

            bool correctPassword = (pb.Password == _password);
            //Invoke result. This is handled internally to complete the connection or kick client.
            OnAuthenticationResult?.Invoke(conn, correctPassword);
            /* Tell client if they authenticated or not. This is
             * entirely optional but does demonstrate that you can send
             * broadcasts to client on pass or fail. */
            ResponseBroadcast rb = new ResponseBroadcast()
            {
                Passed = correctPassword
            };
            base.NetworkManager.ServerManager.Broadcast(conn, rb, false);
        }

        /// <summary>
        /// Received on client after server sends an authentication response.
        /// </summary>
        /// <param name="rb"></param>
        private void OnResponseBroadcast(ResponseBroadcast rb)
        {
            string result = (rb.Passed) ? "Authentication complete." : "Authenitcation failed.";
            if (NetworkManager.CanLog(LoggingType.Common))
                Debug.Log(result);
        }
    }


}
