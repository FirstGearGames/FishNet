﻿using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Connection
{
    public partial class NetworkConnection
    {

        #region Private.
        /// <summary>
        /// PacketBundles to send to this connection. An entry will be made for each channel.
        /// </summary>
        private List<PacketBundle> _toClientBundles = new List<PacketBundle>();
        /// <summary>
        /// True if this object has been dirtied.
        /// </summary>
        private bool _serverDirtied;
        #endregion

        /// <summary>
        /// Initializes this script.
        /// </summary>
        private void InitializeBuffer()
        {
            for (byte i = 0; i < TransportManager.CHANNEL_COUNT; i++)
            {
                int mtu = NetworkManager.TransportManager.Transport.GetMTU(i);
                _toClientBundles.Add(new PacketBundle(NetworkManager, mtu));
            }
        }


        /// <summary>
        /// Sends a broadcast to this connection.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the client must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        public void Broadcast<T>(T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!IsActive)
                    NetworkManager.LogError($"Connection is not valid, cannot send broadcast.");
            else
                NetworkManager.ServerManager.Broadcast<T>(this, message, requireAuthenticated, channel);
        }

        /// <summary>
        /// Sends data from the server to a client.
        /// </summary>
        /// <param name="forceNewBuffer">True to force data into a new buffer.</param>
        internal void SendToClient(byte channel, ArraySegment<byte> segment, bool forceNewBuffer = false)
        {
            //Cannot send data when disconnecting.
            if (Disconnecting)
                return;

            if (!IsActive)
            {
                NetworkManager.LogWarning($"Data cannot be sent to connection {ClientId} because it is not active.");
                return;
            }
            //If channel is out of bounds then default to the first channel.
            if (channel >= _toClientBundles.Count)
                channel = 0;

            _toClientBundles[channel].Write(segment, forceNewBuffer);
            ServerDirty();
        }

        /// <summary>
        /// Returns a PacketBundle for a channel. ResetPackets must be called afterwards.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns>True if PacketBundle is valid on the index and contains data.</returns>
        internal bool GetPacketBundle(int channel, out PacketBundle packetBundle)
        {
            return PacketBundle.GetPacketBundle(channel, _toClientBundles, out packetBundle);
        }

        /// <summary>
        /// Indicates the server has data to send to this connection.
        /// </summary>
        private void ServerDirty()
        {
            bool wasDirty = _serverDirtied;
            _serverDirtied = true;

            //If not yet dirty then tell transport manager this is dirty.
            if (!wasDirty)
                NetworkManager.TransportManager.ServerDirty(this);
        }

        /// <summary>
        /// Resets that there is data to send.
        /// </summary>
        internal void ResetServerDirty()
        {
            _serverDirtied = false;
        }
    }


}