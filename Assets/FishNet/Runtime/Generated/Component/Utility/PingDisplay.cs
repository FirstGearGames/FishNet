using FishNet.Managing.Timing;
using UnityEngine;

namespace FishNet.Component.Utility
{
    /// <summary>
    /// Add to any object to display current ping(round trip time).
    /// </summary>
    public class PingDisplay : MonoBehaviour
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
        /// Which corner to display ping in.
        /// </summary>
        [Tooltip("Which corner to display ping in.")]
        [SerializeField]
        private Corner _placement = Corner.TopRight;
        #endregion

        #region Private.
        /// <summary>
        /// Next time TimeManager can be polled. Throttle this to save performance.
        /// </summary>
        private float _nextTimeManagerTime;
        /// <summary>
        /// TimeManager to get ping from.
        /// </summary>
        private TimeManager _timeManager;
        /// <summary>
        /// Style for drawn ping.
        /// </summary>
        private GUIStyle _style = new GUIStyle();
        #endregion

        private void OnGUI()
        {
            if (_timeManager == null)
            {
                if (Time.unscaledTime < _nextTimeManagerTime)
                    return;

                _nextTimeManagerTime = Time.unscaledTime + 1f;
                _timeManager = InstanceFinder.TimeManager;
            }

            _style.normal.textColor = Color.white;
            _style.fontSize = 15;
            float width = 85f;
            float height = 15f;
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
            GUI.Label(new Rect(horizontal, vertical, width, height), $"Ping: {_timeManager.RoundTripTime}ms", _style);
        }
    }


}