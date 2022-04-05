using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Timing;
using System;
using UnityEngine;

namespace FishNet.Connection
{

    /// <summary>
    /// A container for a connected client used to perform actions on and gather information for the declared client.
    /// </summary>
    public partial class NetworkConnection : IEquatable<NetworkConnection>
    {
        #region Private.
        /// <summary>
        /// Last tick this connection sent a ping.
        /// </summary>
        private uint _lastPingTick;
        /// <summary>
        /// Number of times client has excessively sent a ping.
        /// </summary>
        private float _excessivePingCount;
        /// <summary>
        /// Ticks expected between each ping.
        /// </summary>
        private uint _requiredPingTicks;
        #endregion

        #region Const.
        /// <summary>
        /// Number of times a ping may occur excessively before server will punish connection.
        /// </summary>
        private const byte EXCESSIVE_PING_LIMIT = 10;
        #endregion

        /// <summary>
        /// Initializes for ping.
        /// </summary>
        private void InitializePing()
        {
            //Give the client some room for error.
            float requiredInterval = (NetworkManager.TimeManager.PingInterval * 0.85f);
            //Round down so required ticks is lower.
            _requiredPingTicks = NetworkManager.TimeManager.TimeToTicks(requiredInterval, TickRounding.RoundDown);
        }
        /// <summary>
        /// Called when a ping is received from this connection. Returns if can respond to ping.
        /// </summary>
        /// <returns>True to respond to ping, false to kick connection.</returns>
        internal bool CanPingPong()
        {
            /* Only check ping conditions in build. Editors are prone to pausing which can
             * improperly kick clients. */
#if UNITY_EDITOR
            return true;
#else
            TimeManager tm = (NetworkManager == null) ? InstanceFinder.TimeManager : NetworkManager.TimeManager;
            uint currentTick = tm.Tick;
            uint difference = (currentTick - _lastPingTick);
            _lastPingTick = currentTick;

            //Ping sent too quickly.
            if (difference < _requiredPingTicks)
            {
                _excessivePingCount += 1f;
                //Ping limit hit.
                if (_excessivePingCount >= EXCESSIVE_PING_LIMIT)
                {
                    if (NetworkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Kicked connectionId {ClientId} for excessive pings.");
                    Disconnect(true);
                }

                //Return to not send pong back.
                return false;
            }
            //Ping isnt too fast.
            else
            {
                _excessivePingCount = Mathf.Max(0f, _excessivePingCount - 0.5f);
                return true;
            }
#endif
        }
    }


}