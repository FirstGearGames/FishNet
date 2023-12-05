﻿using FishNet.Broadcast;
using FishNet.Broadcast.Helping;
using FishNet.Managing.Utility;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Handler for registered broadcasts.
        /// </summary>
        private readonly Dictionary<ushort, BroadcastHandlerBase> _broadcastHandlers = new Dictionary<ushort, BroadcastHandlerBase>();
        #endregion

        /// <summary>
        /// Registers a method to call when a Broadcast arrives.
        /// </summary>
        /// <typeparam name="T">Type of broadcast being registered.</typeparam>
        /// <param name="handler">Method to call.</param>
        public void RegisterBroadcast<T>(Action<T, Channel> handler) where T : struct, IBroadcast
        {
            if (handler == null)
            {
                NetworkManager.LogError($"Broadcast cannot be registered because handler is null. This may occur when trying to register to objects which require initialization, such as events.");
                return;
            }

            ushort key = BroadcastHelper.GetKey<T>();
            //Create new IBroadcastHandler if needed.
            BroadcastHandlerBase bhs;
            if (!_broadcastHandlers.TryGetValueIL2CPP(key, out bhs))
            {
                bhs = new ServerBroadcastHandler<T>();
                _broadcastHandlers.Add(key, bhs);
            }
            //Register handler to IBroadcastHandler.
            bhs.RegisterHandler(handler);
        }

        /// <summary>
        /// Unregisters a method call from a Broadcast type.
        /// </summary>
        /// <typeparam name="T">Type of broadcast being unregistered.</typeparam>
        /// <param name="handler">Method to unregister.</param>
        public void UnregisterBroadcast<T>(Action<T, Channel> handler) where T : struct, IBroadcast
        {
            ushort key = BroadcastHelper.GetKey<T>();            
            if (_broadcastHandlers.TryGetValueIL2CPP(key, out BroadcastHandlerBase bhs))
                bhs.UnregisterHandler(handler);
        }

        /// <summary>
        /// Parses a received broadcast.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseBroadcast(PooledReader reader, Channel channel)
        {
            ushort key = reader.ReadUInt16();
            int dataLength = Packets.GetPacketLength((ushort)PacketId.Broadcast, reader, channel);
            // try to invoke the handler for that message
            if (_broadcastHandlers.TryGetValueIL2CPP(key, out BroadcastHandlerBase bhs))
                bhs.InvokeHandlers(reader, channel);
            else
                reader.Skip(dataLength);
        }


        /// <summary>
        /// Sends a Broadcast to the server.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="channel">Channel to send on.</param>
        public void Broadcast<T>(T message, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            //Check local connection state.
            if (!Started)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to server because client is not active.");
                return;
            }

            PooledWriter writer = WriterPool.Retrieve();
            Broadcasts.WriteBroadcast<T>(NetworkManager, writer, message, ref channel);
            ArraySegment<byte> segment = writer.GetArraySegment();

            NetworkManager.TransportManager.SendToServer((byte)channel, segment);
            writer.Store();
        }

    }


}
