

using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Statistic
{
    [System.Serializable]
    public class NetworkTraficStatistics
    {
        #region Public.
        /// <summary>
        /// Called when NetworkTraffic is updated for the client.
        /// </summary>
        public event Action<NetworkTrafficArgs> OnClientNetworkTraffic;
        /// <summary>
        /// Called when NetworkTraffic is updated for the server.
        /// </summary>
        public event Action<NetworkTrafficArgs> OnServerNetworkTraffic;
        #endregion

        #region Serialized.
        /// <summary>
        /// How often to update traffic statistics.
        /// </summary>
        [Tooltip("How often to update traffic statistics.")]
        [SerializeField]
        [Range(0f, 10f)]
        private float _updateInteval = 1f;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to update client statistics.")]
        [SerializeField]
        private bool _updateClient;
        /// <summary>
        /// True to update client statistics.
        /// </summary>
        public bool UpdateClient
        {
            get => _updateClient;
            private set => _updateClient = value;
        }
        /// <summary>
        /// Sets UpdateClient value.
        /// </summary>
        /// <param name="update"></param>
        public void SetUpdateClient(bool update)
        {
            UpdateClient = update;
        }
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to update server statistics.")]
        [SerializeField]
        private bool _updateServer;
        /// <summary>
        /// True to update client statistics.
        /// </summary>
        public bool UpdateServer
        {
            get => _updateServer;
            private set => _updateServer = value;
        }
        /// <summary>
        /// Sets UpdateServer value.
        /// </summary>
        /// <param name="update"></param>
        public void SetUpdateServer(bool update)
        {
            UpdateServer = update;
        }
        #endregion

        #region Private.
        /// <summary>
        /// NetworkManager for this statistics.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Bytes sent to the server from local client.
        /// </summary>
        private ulong _client_toServerBytes;
        /// <summary>
        /// Bytes received on the local client from the server.
        /// </summary>
        private ulong _client_fromServerBytes;
        /// <summary>
        /// Bytes sent to all clients from the local server.
        /// </summary>
        private ulong _server_toClientsBytes;
        /// <summary>
        /// Bytes received on the local server from all clients.
        /// </summary>
        private ulong _server_fromClientsBytes;
        /// <summary>
        /// Next time network traffic updates may invoke.
        /// </summary>
        private float _nextUpdateTime;
        /// <summary>
        /// Size suffixes as text.
        /// </summary>
        private static readonly string[] _sizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        #endregion

        internal void InitializeOnce_Internal(NetworkManager manager)
        {
            _networkManager = manager;
            manager.TimeManager.OnPreTick += TimeManager_OnPreTick;
        }

        /// <summary>
        /// Called before the TimeManager ticks.
        /// </summary>
        private void TimeManager_OnPreTick()
        {
            if (Time.unscaledTime < _nextUpdateTime)
                return;
            _nextUpdateTime = Time.unscaledTime + _updateInteval;

            if (UpdateClient && _networkManager.IsClient)
                OnClientNetworkTraffic?.Invoke(new NetworkTrafficArgs(_client_toServerBytes, _client_fromServerBytes));
            if (UpdateServer && _networkManager.IsServer)
                OnServerNetworkTraffic?.Invoke(new NetworkTrafficArgs(_server_fromClientsBytes, _server_toClientsBytes));

            _client_toServerBytes = 0;
            _client_fromServerBytes = 0;
            _server_toClientsBytes = 0;
            _server_fromClientsBytes = 0;
        }

        /// <summary>
        /// Called when the local client sends data.
        /// </summary>
        internal void LocalClientSentData(ulong dataLength)
        {
            _client_toServerBytes = Math.Min(_client_toServerBytes + dataLength, ulong.MaxValue);
        }
        /// <summary>
        /// Called when the local client receives data.
        /// </summary>
        public void LocalClientReceivedData(ulong dataLength)
        {
            _client_fromServerBytes = Math.Min(_client_fromServerBytes + dataLength, ulong.MaxValue);
        }


        /// <summary>
        /// Called when the local client sends data.
        /// </summary>
        internal void LocalServerSentData(ulong dataLength)
        {
            _server_toClientsBytes = Math.Min(_server_toClientsBytes + dataLength, ulong.MaxValue);
        }
        /// <summary>
        /// Called when the local client receives data.
        /// </summary>
        public void LocalServerReceivedData(ulong dataLength)
        {
            _server_fromClientsBytes = Math.Min(_server_fromClientsBytes + dataLength, ulong.MaxValue);
        }


        //Attribution: https://stackoverflow.com/questions/14488796/does-net-provide-an-easy-way-convert-bytes-to-kb-mb-gb-etc
        /// <summary>
        /// Formats passed in bytes value to the largest possible data type with 2 decimals.
        /// </summary>
        public static string FormatBytesToLargest(ulong bytes)
        {
            int decimalPlaces = 2;
            if (bytes == 0)
            {
                decimalPlaces = 0;
                return string.Format("{0:n" + decimalPlaces + "} bytes", 0);
            }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(bytes, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)bytes / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            //Don't show decimals for bytes.
            if (mag == 0)
                decimalPlaces = 0;

            return string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, _sizeSuffixes[mag]);
        }
    }


}