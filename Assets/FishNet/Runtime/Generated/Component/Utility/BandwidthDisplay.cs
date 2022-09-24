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

        #region Private.
        /// <summary>
        /// Style for drawn ping.
        /// </summary>
        private GUIStyle _style = new GUIStyle();
        /// <summary>
        /// Text to display OnGui.
        /// </summary>
        private string _displayedText;
        /// <summary>
        /// First found NetworkTrafficStatistics.
        /// </summary>
        private NetworkTraficStatistics _networkTrafficStatistics;
        #endregion

        private void Start()
        {
            _networkTrafficStatistics = InstanceFinder.NetworkManager.StatisticsManager.NetworkTraffic;
            _networkTrafficStatistics.OnClientNetworkTraffic += NetworkTraffic_OnClientNetworkTraffic;
            if (!_networkTrafficStatistics.UpdateClient && !_networkTrafficStatistics.UpdateServer)
                Debug.LogWarning($"StatisticsManager.NetworkTraffic is not updating for client nor server. To see results ensure your NetworkManager has a StatisticsManager component added with the NetworkTraffic values configured.");
        }

        private void OnDestroy()
        {
            if (_networkTrafficStatistics != null)
                _networkTrafficStatistics.OnClientNetworkTraffic -= NetworkTraffic_OnClientNetworkTraffic;
        }


        /// <summary>
        /// Called when client network traffic is updated.
        /// </summary>
        private void NetworkTraffic_OnClientNetworkTraffic(NetworkTrafficArgs obj)
        {
            string nl = System.Environment.NewLine;
            string result = string.Empty;
            if (_showIncoming)
                result += $"In: {NetworkTraficStatistics.FormatBytesToLargest(obj.FromServerBytes)}/s{nl}";
            if (_showOutgoing)
                result += $"Out: {NetworkTraficStatistics.FormatBytesToLargest(obj.ToServerBytes)}/s{nl}";

            _displayedText = result;
        }


        private void OnGUI()
        {
            //No need to perform these actions on server.
#if !UNITY_EDITOR && UNITY_SERVER
            return;
#endif

            _style.normal.textColor = _color;
            _style.fontSize = 15;
            float width = 100f;
            float height = 0f;
            if (_showIncoming)
                height += 15f;
            if (_showOutgoing)
                height += 15f;

            float edge = 10f;

            float horizontal;
            float vertical;

            if (_placement == Corner.TopLeft)
            {
                horizontal = 10f;
                vertical = 10f;
            }
            else if (_placement == Corner.TopRight)
            {
                horizontal = Screen.width - width - edge;
                vertical = 10f;
            }
            else if (_placement == Corner.BottomLeft)
            {
                horizontal = 10f;
                vertical = Screen.height - height - edge;
            }
            else
            {
                horizontal = Screen.width - width - edge;
                vertical = Screen.height - height - edge;
            }

            GUI.Label(new Rect(horizontal, vertical, width, height), _displayedText, _style);
        }
    }


}