using FishNet.Broadcast;
using FishNet.Broadcast.Helping;
using FishNet.Managing.Logging;
using FishNet.Managing.Utility;
using FishNet.Object.Helping;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Extension;
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
        /// Delegate to read received broadcasts.
        /// </summary>
        /// <param name="reader"></param>
        private delegate void ServerBroadcastDelegate(PooledReader reader);
        /// <summary>
        /// Delegates for each key.
        /// </summary>
        private readonly Dictionary<ushort, HashSet<ServerBroadcastDelegate>> _broadcastHandlers = new Dictionary<ushort, HashSet<ServerBroadcastDelegate>>();
        /// <summary>
        /// Delegate targets for each key.
        /// </summary>
        private Dictionary<ushort, HashSet<(int, ServerBroadcastDelegate)>> _handlerTargets = new Dictionary<ushort, HashSet<(int, ServerBroadcastDelegate)>>();
        #endregion

        /// <summary>
        /// Registers a method to call when a Broadcast arrives.
        /// </summary>
        /// <typeparam name="T">Type of broadcast being registered.</typeparam>
        /// <param name="handler">Method to call.</param>
        public void RegisterBroadcast<T>(Action<T> handler) where T : struct, IBroadcast
        {
            ushort key = typeof(T).FullName.GetStableHash16();
            /* Create delegate and add for
             * handler method. */
            HashSet<ServerBroadcastDelegate> handlers;
            if (!_broadcastHandlers.TryGetValueIL2CPP(key, out handlers))
            {
                handlers = new HashSet<ServerBroadcastDelegate>();
                _broadcastHandlers.Add(key, handlers);
            }
            ServerBroadcastDelegate del = CreateBroadcastDelegate(handler);
            handlers.Add(del);

            /* Add hashcode of target for handler.
             * This is so we can unregister the target later. */
            int handlerHashCode = handler.GetHashCode();
            HashSet<(int, ServerBroadcastDelegate)> targetHashCodes;
            if (!_handlerTargets.TryGetValueIL2CPP(key, out targetHashCodes))
            {
                targetHashCodes = new HashSet<(int, ServerBroadcastDelegate)>();
                _handlerTargets.Add(key, targetHashCodes);
            }

            targetHashCodes.Add((handlerHashCode, del));
        }

        /// <summary>
        /// Unregisters a method call from a Broadcast type.
        /// </summary>
        /// <typeparam name="T">Type of broadcast being unregistered.</typeparam>
        /// <param name="handler">Method to unregister.</param>
        public void UnregisterBroadcast<T>(Action<T> handler) where T : struct, IBroadcast
        {
            ushort key = BroadcastHelper.GetKey<T>();

            /* If key is found for T then look for
             * the appropriate handler to remove. */
            if (_broadcastHandlers.TryGetValueIL2CPP(key, out HashSet<ServerBroadcastDelegate> handlers))
            {
                HashSet<(int, ServerBroadcastDelegate)> targetHashCodes;
                if (_handlerTargets.TryGetValueIL2CPP(key, out targetHashCodes))
                {
                    int handlerHashCode = handler.GetHashCode();
                    ServerBroadcastDelegate result = null;
                    foreach ((int targetHashCode, ServerBroadcastDelegate del) in targetHashCodes)
                    {
                        if (targetHashCode == handlerHashCode)
                        {
                            result = del;
                            targetHashCodes.Remove((targetHashCode, del));
                            break;
                        }
                    }
                    //If no more in targetHashCodes then remove from handlerTarget.
                    if (targetHashCodes.Count == 0)
                        _handlerTargets.Remove(key);

                    if (result != null)
                        handlers.Remove(result);
                }

                //If no more in handlers then remove broadcastHandlers.
                if (handlers.Count == 0)
                    _broadcastHandlers.Remove(key);
            }
        }

        /// <summary>
        /// Creates a ServerBroadcastDelegate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        /// <param name="requireAuthentication"></param>
        /// <returns></returns>
        private ServerBroadcastDelegate CreateBroadcastDelegate<T>(Action<T> handler)
        {
            void LogicContainer(PooledReader reader)
            {
                T broadcast = reader.Read<T>();
                handler?.Invoke(broadcast);
            }
            return LogicContainer;
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
            if (_broadcastHandlers.TryGetValueIL2CPP(key, out HashSet<ServerBroadcastDelegate> handlers))
            {
                int readerStartPosition = reader.Position;
                /* //muchlater resetting the position could be better by instead reading once and passing in
                 * the object to invoke with. */
                foreach (ServerBroadcastDelegate handler in handlers)
                {
                    reader.Position = readerStartPosition;
                    handler.Invoke(reader);
                }
            }
            else
            {
                reader.Skip(dataLength);
            }
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
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Cannot send broadcast to server because client is not active.");
                return;
            }

            using (PooledWriter writer = WriterPool.GetWriter())
            {
                Broadcasts.WriteBroadcast<T>(writer, message, channel);
                ArraySegment<byte> segment = writer.GetArraySegment();

                NetworkManager.TransportManager.SendToServer((byte)channel, segment);
            }
        }

    }


}
