using FishNet.Managing.Statistic;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;

namespace FishNet.Component.Utility
{
    /// <summary>
    /// Add to any object to display current ping(round trip time).
    /// </summary>
    [AddComponentMenu("FishNet/Component/BandwidthDisplay")]
    public class BandwidthDisplay : MonoBehaviour
    {
        #region Types.
        private enum Corner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private class InOutAverage
        {
            private RingBuffer<ulong> _in;
            private RingBuffer<ulong> _out;

            public InOutAverage(byte seconds)
            {
                _in = new(seconds);
                _out = new(seconds);
            }

            public void AddIn(ulong value) => _in.Add(value);
            public void AddOut(ulong value) => _out.Add(value);

            public ulong GetAverage(bool inAverage)
            {
                RingBuffer<ulong> buffer = (inAverage) ? _in : _out;

                int count = buffer.Count;

                ulong total = 0;
                foreach (ulong v in buffer)
                    total += v;

                return (total / (uint)count);
            }

            public void ResetState()
            {
                _in.Clear();
                _out.Clear();
            }

            public void InitializeState(int capacity)
            {
                _in.Initialize(capacity);
                _out.Initialize(capacity);
            }
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// Color for text.
        /// </summary>
        [Tooltip("Color for text.")]
        [SerializeField]
        private Color _color = Color.white;
        /// <summary>
        /// Which corner to display network statistics in.
        /// </summary>
        [Tooltip("Which corner to display network statistics in.")]
        [SerializeField]
        private Corner _placement = Corner.TopRight;
        /// <summary>
        /// Number of seconds used to gather data per second. Lower values will show more up to date usage per second while higher values provide a better over-all estimate.
        /// </summary>
        [Tooltip("Number of seconds used to gather data per second. Lower values will show more up to date usage per second while higher values provide a better over-all estimate.")]
        [SerializeField]
        [Range(1, byte.MaxValue)]
        private byte _secondsAveraged = 1;

        /// <summary>
        /// rue to show outgoing data bytes.
        /// </summary>
        [Tooltip("True to show outgoing data bytes.")]
        [SerializeField]
        private bool _showOutgoing = true;

        /// <summary>
        /// Sets ShowOutgoing value.
        /// </summary>
        /// <param name="value"></param>
        public void SetShowOutgoing(bool value) => _showOutgoing = value;

        /// <summary>
        /// True to show incoming data bytes.
        /// </summary>
        [Tooltip("True to show incoming data bytes.")]
        [SerializeField]
        private bool _showIncoming = true;

        /// <summary>
        /// Sets ShowIncoming value.
        /// </summary>
        /// <param name="value"></param>
        public void SetShowIncoming(bool value) => _showIncoming = value;
        #endregion

#if UNITY_EDITOR || !UNITY_SERVER

        #region Private.
        /// <summary>
        /// Style for drawn ping.
        /// </summary>
        private readonly GUIStyle _style = new();
        /// <summary>
        /// Text to show for client in/out data.
        /// </summary>
        private string _clientText;
        /// <summary>
        /// Text to show for server in/out data.
        /// </summary>
        private string _serverText;
        /// <summary>
        /// First found NetworkTrafficStatistics.
        /// </summary>
        private NetworkTraficStatistics _networkTrafficStatistics;
        /// <summary>
        /// Averages for client.
        /// </summary>
        private InOutAverage _clientAverages;
        /// <summary>
        /// Averages for server.
        /// </summary>
        private InOutAverage _serverAverages;
        #endregion

        private void Start()
        {
            SetSecondsAveraged(_secondsAveraged);

            _networkTrafficStatistics = InstanceFinder.NetworkManager.StatisticsManager.NetworkTraffic;
            //Subscribe to both traffic updates.
            _networkTrafficStatistics.OnClientNetworkTraffic += NetworkTraffic_OnClientNetworkTraffic;
            _networkTrafficStatistics.OnServerNetworkTraffic += NetworkTraffic_OnServerNetworkTraffic;

            if (!_networkTrafficStatistics.UpdateClient && !_networkTrafficStatistics.UpdateServer)
                Debug.LogWarning($"StatisticsManager.NetworkTraffic is not updating for client nor server. To see results ensure your NetworkManager has a StatisticsManager component added with the NetworkTraffic values configured.");
        }

        private void OnDestroy()
        {
            if (_networkTrafficStatistics != null)
            {
                _networkTrafficStatistics.OnClientNetworkTraffic -= NetworkTraffic_OnClientNetworkTraffic;
                _networkTrafficStatistics.OnServerNetworkTraffic -= NetworkTraffic_OnServerNetworkTraffic;
            }
        }
        
        /// <summary>
        /// Sets a new number of seconds to average from.
        /// </summary>
        public void SetSecondsAveraged(byte seconds)
        {
            if (seconds <= 0)
                seconds = 1;

            _clientAverages = new(seconds);
            _serverAverages = new(seconds);
        }

        /// <summary>
        /// Called when client network traffic is updated.
        /// </summary>
        private void NetworkTraffic_OnClientNetworkTraffic(NetworkTrafficArgs obj)
        {
            string nl = System.Environment.NewLine;
            string result = string.Empty;

            _clientAverages.AddIn(obj.ToServerBytes);
            _clientAverages.AddOut(obj.FromServerBytes);

            if (_showIncoming)
                result += $"Client In: {NetworkTraficStatistics.FormatBytesToLargest(_clientAverages.GetAverage(inAverage: true))}/s{nl}";
            if (_showOutgoing)
                result += $"Client Out: {NetworkTraficStatistics.FormatBytesToLargest(_clientAverages.GetAverage(inAverage: false))}/s{nl}";

            _clientText = result;
        }

        /// <summary>
        /// Called when server network traffic is updated.
        /// </summary>
        private void NetworkTraffic_OnServerNetworkTraffic(NetworkTrafficArgs obj)
        {
            string nl = System.Environment.NewLine;
            string result = string.Empty;

            _serverAverages.AddIn(obj.ToServerBytes);
            _serverAverages.AddOut(obj.FromServerBytes);

            if (_showIncoming)
                result += $"Server In: {NetworkTraficStatistics.FormatBytesToLargest(_serverAverages.GetAverage(inAverage: true))}/s{nl}";
            if (_showOutgoing)
                result += $"Server Out: {NetworkTraficStatistics.FormatBytesToLargest(_serverAverages.GetAverage(inAverage: false))}/s{nl}";

            _serverText = result;
        }

        private void OnGUI()
        {
            _style.normal.textColor = _color;
            _style.fontSize = 15;

            float width = 100f;
            float height = 0f;
            if (_showIncoming)
                height += 15f;
            if (_showOutgoing)
                height += 15f;

            bool isClient = InstanceFinder.IsClientStarted;
            bool isServer = InstanceFinder.IsServerStarted;
            if (!isClient)
                ResetCalculationsAndDisplay(forServer: false);
            if (!isServer)
                ResetCalculationsAndDisplay(forServer: true);
            if (isServer && isClient)
                height *= 2f;

            float edge = 10f;

            float horizontal;
            float vertical;

            if (_placement == Corner.TopLeft)
            {
                horizontal = 10f;
                vertical = 10f;
                _style.alignment = TextAnchor.UpperLeft;
            }
            else if (_placement == Corner.TopRight)
            {
                horizontal = Screen.width - width - edge;
                vertical = 10f;
                _style.alignment = TextAnchor.UpperRight;
            }
            else if (_placement == Corner.BottomLeft)
            {
                horizontal = 10f;
                vertical = Screen.height - height - edge;
                _style.alignment = TextAnchor.LowerLeft;
            }
            else
            {
                horizontal = Screen.width - width - edge;
                vertical = Screen.height - height - edge;
                _style.alignment = TextAnchor.LowerRight;
            }

            GUI.Label(new(horizontal, vertical, width, height), (_clientText + _serverText), _style);
        }

        [ContextMenu("Reset Averages")]
        private void ResetAverages()
        {
            ResetCalculationsAndDisplay(forServer: true);
            ResetCalculationsAndDisplay(forServer: false);
        }

        private void ResetCalculationsAndDisplay(bool forServer)
        {
            if (forServer)
            {
                _serverText = string.Empty;
                _serverAverages.ResetState();
            }
            else
            {
                _clientText = string.Empty;
                _clientAverages.ResetState();
            }
        }
#endif
    }
}