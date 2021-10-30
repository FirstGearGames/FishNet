using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Timing;
using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private uint _lastPingTick = 0;
        /// <summary>
        /// Number of times client has excessively sent a ping.
        /// </summary>
        private float _excessivePingCount = 0f;
        /// <summary>
        /// Ticks expected between each ping.
        /// </summary>
        private uint _requiredPingTicks = 0;
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
            float requiredInterval = (TimeManager.PING_INTERVAL * 0.85f);
            //Round down so required ticks is lower.
            _requiredPingTicks = NetworkManager.TimeManager.TimeToTicks(requiredInterval, TickRounding.RoundDown);
        }
        /// <summary>
        /// Called when a ping is received from this connection. Returns if can respond to ping.
        /// </summary>
        /// <returns>True to respond to ping, false to kick connection.</returns>
        internal bool CanPingPong()
        {
            uint currentTick = InstanceFinder.TimeManager.Tick;
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
        }
    }


}