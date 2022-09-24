using FishNet.Managing.Timing;
using UnityEngine;

namespace FishNet.Component.Utility
{
    /// <summary>
    /// Add to any object to display current ping(round trip time).
    /// </summary>
    [AddComponentMenu("FishNet/Component/PingDisplay")]
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
        /// Color for text.
        /// </summary>
        [Tooltip("Color for text.")]
        [SerializeField]
        private Color _color = Color.white;
        /// <summary>
        /// Which corner to display ping in.
        /// </summary>
        [Tooltip("Which corner to display ping in.")]
        [SerializeField]
        private Corner _placement = Corner.TopRight;
        /// <summary>
        /// True to show the real ping. False to include tick rate latency within the ping.
        /// </summary>
        [Tooltip("True to show the real ping. False to include tick rate latency within the ping.")]
        [SerializeField]
        private bool _hideTickRate = true;
        #endregion

        #region Private.
        /// <summary>
        /// Style for drawn ping.
        /// </summary>
        private GUIStyle _style = new GUIStyle();
        #endregion

        private void OnGUI()
        {
            //No need to perform these actions on server.
#if !UNITY_EDITOR && UNITY_SERVER
            return;
#endif

            //Only clients can see pings.
            if (!InstanceFinder.IsClient)
                return;

            _style.normal.textColor = _color;
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

            long ping;
            TimeManager tm = InstanceFinder.TimeManager;
            if (tm == null)
            {
                ping = 0;
            }
            else
            {
                ping = tm.RoundTripTime;
                long deduction = 0;
                if (_hideTickRate)
                {
                    deduction = (long)(tm.TickDelta * 1000d);
                    /* If host subtract two ticks, if client only subtract one tick.
                    * This will reflect the users real ping without the tick rate latency. */
                    if (InstanceFinder.IsHost)
                        deduction *= 2;
                }

                ping = (long)Mathf.Max(0, ping - deduction);
            }

            GUI.Label(new Rect(horizontal, vertical, width, height), $"Ping: {ping}ms", _style);
        }
    }


}