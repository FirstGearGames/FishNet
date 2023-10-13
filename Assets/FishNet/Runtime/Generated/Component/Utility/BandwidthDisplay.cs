using FishNet.Managing.Statistic;
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
        private GUIStyle _style = new GUIStyle();
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
        #endregion

        private void Start()
        {
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
        /// Called when client network traffic is updated.
        /// </summary>
        private void NetworkTraffic_OnClientNetworkTraffic(NetworkTrafficArgs obj)
        {
            string nl = System.Environment.NewLine;
            string result = string.Empty;
            if (_showIncoming)
                result += $"Client In: {NetworkTraficStatistics.FormatBytesToLargest(obj.FromServerBytes)}/s{nl}";
            if (_showOutgoing)
                result += $"Client Out: {NetworkTraficStatistics.FormatBytesToLargest(obj.ToServerBytes)}/s{nl}";

            _clientText = result;
        }

        /// <summary>
        /// Called when client network traffic is updated.
        /// </summary>
        private void NetworkTraffic_OnServerNetworkTraffic(NetworkTrafficArgs obj)
        {
            string nl = System.Environment.NewLine;
            string result = string.Empty;
            if (_showIncoming)
                result += $"Server In: {NetworkTraficStatistics.FormatBytesToLargest(obj.ToServerBytes)}/s{nl}";
            if (_showOutgoing)
                result += $"Server Out: {NetworkTraficStatistics.FormatBytesToLargest(obj.FromServerBytes)}/s{nl}";

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

            bool isClient = InstanceFinder.IsClient;
            bool isServer = InstanceFinder.IsServer;
            if (!isClient)
                _clientText = string.Empty;
            if (!isServer)
                _serverText = string.Empty;
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

            GUI.Label(new Rect(horizontal, vertical, width, height), (_clientText + _serverText), _style);
        }
#endif

    }


}