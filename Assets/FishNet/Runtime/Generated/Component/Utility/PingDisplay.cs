using FishNet.Managing.Timing;
using UnityEngine;

namespace FishNet.Component.Utility
{
    public class PingDisplay : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Next time TimeManager can be polled. Throttle this to save performance.
        /// </summary>
        private float _nextTimeManagerTime = 0f;
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
            float edge = 10f;
            GUI.Label(new Rect(Screen.width - width - edge, 10, 100, 20), $"Ping: {_timeManager.RoundTripTime}ms", _style);
        }
    }


}