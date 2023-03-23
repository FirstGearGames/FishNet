using FishNet.Connection;
using FishNet.Example.Authenticating;
using FishNet.Managing;
using FishNet.Transporting;
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FishNet.Authenticating
{

    /// <summary>
    /// This authenticator is an example of how to let host bypass the authentication process.
    /// When checking to authenticate on the client side call AuthenticateAsHost, and if returned true skip normal authentication.
    /// </summary>
    public abstract class HostAuthenticator : Authenticator
    {
        #region Serialized.
        /// <summary>
        /// True to enable use of AuthenticateAsHost.
        /// </summary>
        [Tooltip("True to enable use of AuthenticateAsHost.")]
        [SerializeField]
        private bool _allowHostAuthentication;
        /// <summary>
        /// Sets if to allow host authentication.
        /// </summary>
        /// <param name="value"></param>
        public void SetAllowHostAuthentication(bool value) => _allowHostAuthentication = value;
        /// <summary>
        /// Returns if AllowHostAuthentication is enabled.
        /// </summary>
        /// <returns></returns>
        public bool GetAllowHostAuthentication() => _allowHostAuthentication;
        #endregion

        #region Private.
        /// <summary>
        /// A random hash which only exist if the server is started.
        /// </summary>
        private static string _hostHash = string.Empty;
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="networkManager"></param>
        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            //Listen for connection state of local server to set hash.
            base.NetworkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            //Listen for broadcast from client. Be sure to set requireAuthentication to false.
            base.NetworkManager.ServerManager.RegisterBroadcast<HostPasswordBroadcast>(OnHostPasswordBroadcast, false);
        }

        /// <summary>
        /// Called after the local server connection state changes.
        /// </summary>
        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            int length = (obj.ConnectionState == LocalConnectionState.Started) ? 25 : 0;
            SetHostHash(length);
        }

        /// <summary>
        /// Received on server when a client sends the password broadcast message.
        /// </summary>
        /// <param name="conn">Connection sending broadcast.</param>
        /// <param name="hpb"></param>
        private void OnHostPasswordBroadcast(NetworkConnection conn, HostPasswordBroadcast hpb)
        {
            //Not accepting host authentications. This could be an attack.
            if (!_allowHostAuthentication)
            {
                conn.Disconnect(true);
                return;
            }
            /* If client is already authenticated this could be an attack. Connections
             * are removed when a client disconnects so there is no reason they should
             * already be considered authenticated. */
            if (conn.Authenticated)
            {
                conn.Disconnect(true);
                return;
            }

            bool correctPassword = (hpb.Password == _hostHash);
            OnHostAuthenticationResult(conn, correctPassword);
        }

        /// <summary>
        /// Called after handling a host authentication result.
        /// </summary>
        /// <param name="conn">Connection authenticating.</param>
        /// <param name="authenticated">True if authentication passed.</param>
        protected abstract void OnHostAuthenticationResult(NetworkConnection conn, bool authenticated);    

        /// <summary>
        /// Sets a host hash of length.
        /// </summary>
        /// https://stackoverflow.com/questions/32932679/using-rngcryptoserviceprovider-to-generate-random-string
        private void SetHostHash(int length)
        {            
            if (length <= 0)
            {
                _hostHash = string.Empty;
            }
            else
            {
                const string charPool = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()";
                StringBuilder result = new StringBuilder();
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    byte[] uintBuffer = new byte[sizeof(uint)];
                    while (length-- > 0)
                    {
                        rng.GetBytes(uintBuffer);
                        uint num = BitConverter.ToUInt32(uintBuffer, 0);
                        result.Append(charPool[(int)(num % (uint)charPool.Length)]);
                    }
                }

                _hostHash = result.ToString();
            }
        }

        /// <summary>
        /// Returns true if authentication was sent as host.
        /// </summary>
        /// <returns></returns>
        protected bool AuthenticateAsHost()
        {
            if (!_allowHostAuthentication)
                return false;
            if (_hostHash == string.Empty)
                return false;

            HostPasswordBroadcast hpb = new HostPasswordBroadcast()
            {
                Password = _hostHash,
            };

            base.NetworkManager.ClientManager.Broadcast(hpb);
            return true;
        }

    }


}