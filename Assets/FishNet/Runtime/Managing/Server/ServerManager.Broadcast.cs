using FishNet.Connection;
using FishNet.Serializing;
using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Broadcast;
using FishNet.Object.Helping;
using FishNet.Broadcast.Helping;
using FishNet.Transporting;
using FishNet.Object;
using FishNet.Serializing.Helping;

namespace FishNet.Managing.Server
{
    public partial class ServerManager : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Delegate to read received broadcasts.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="reader"></param>
        private delegate void ClientBroadcastDelegate(NetworkConnection connection, PooledReader reader);
        /// <summary>
        /// Delegates for each key.
        /// </summary>
        private readonly Dictionary<ushort, HashSet<ClientBroadcastDelegate>> _broadcastHandlers = new Dictionary<ushort, HashSet<ClientBroadcastDelegate>>();
        /// <summary>
        /// Delegate targets for each key.
        /// </summary>
        private Dictionary<ushort, HashSet<(int, ClientBroadcastDelegate)>> _handlerTargets = new Dictionary<ushort, HashSet<(int, ClientBroadcastDelegate)>>();
        #endregion

        /// <summary>
        /// Registers a method to call when a Broadcast arrives.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler">Method to call.</param>
        /// <param name="requireAuthentication">True if the client must be authenticated for the method to call.</param>
        public void RegisterBroadcast<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true) where T : struct, IBroadcast
        {
            ushort key = BroadcastHelper.GetKey<T>();

            /* Create delegate and add for
             * handler method. */
            HashSet<ClientBroadcastDelegate> handlers;
            if (!_broadcastHandlers.TryGetValue(key, out handlers))
            {
                handlers = new HashSet<ClientBroadcastDelegate>();
                _broadcastHandlers.Add(key, handlers);
            }
            ClientBroadcastDelegate del = CreateBroadcastDelegate(handler, requireAuthentication);
            handlers.Add(del);

            /* Add hashcode of target for handler.
             * This is so we can unregister the target later. */
            int handlerHashCode = handler.GetHashCode();
            HashSet<(int, ClientBroadcastDelegate)> targetHashCodes;
            if (!_handlerTargets.TryGetValue(key, out targetHashCodes))
            {
                targetHashCodes = new HashSet<(int, ClientBroadcastDelegate)>();
                _handlerTargets.Add(key, targetHashCodes);
            }

            targetHashCodes.Add((handlerHashCode, del));
        }

        /// <summary>
        /// Unregisters a method call from a Broadcast type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void UnregisterBroadcast<T>(Action<NetworkConnection, T> handler) where T : struct, IBroadcast
        {
            ushort key = BroadcastHelper.GetKey<T>();

            /* If key is found for T then look for
             * the appropriate handler to remove. */
            if (_broadcastHandlers.TryGetValue(key, out HashSet<ClientBroadcastDelegate> handlers))
            {
                HashSet<(int, ClientBroadcastDelegate)> targetHashCodes;
                if (_handlerTargets.TryGetValue(key, out targetHashCodes))
                {
                    int handlerHashCode = handler.GetHashCode();
                    ClientBroadcastDelegate result = null;
                    foreach ((int targetHashCode, ClientBroadcastDelegate del) in targetHashCodes)
                    {
                        if (targetHashCode == handlerHashCode)
                        {
                            result = del;
                            break;
                        }
                    }

                    if (result != null)
                        handlers.Remove(result);
                }
            }
        }

        /// <summary>
        /// Creates a ClientBroadcastDelegate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        /// <param name="requireAuthentication"></param>
        /// <returns></returns>
        private ClientBroadcastDelegate CreateBroadcastDelegate<T>(Action<NetworkConnection, T> handler, bool requireAuthentication)
        {
            void LogicContainer(NetworkConnection connection, PooledReader reader)
            {
                //If requires authentication and client isn't authenticated.
                if (requireAuthentication && !connection.Authenticated)
                {
                    if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                        Debug.LogWarning($"ClientId {connection.ClientId} sent broadcast {typeof(T).Name} which requires authentication, but client was not authenticated. Client has been disconnected.");
                    NetworkManager.TransportManager.Transport.StopConnection(connection.ClientId, true);
                    return;
                }

                T broadcast = reader.Read<T>();
                handler?.Invoke(connection, broadcast);
            }
            return LogicContainer;
        }

        /// <summary>
        /// Parses a received broadcast.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="connectionId"></param>
        private void ParseBroadcast(PooledReader reader, int connectionId)
        {
            ushort key = reader.ReadUInt16();

            // try to invoke the handler for that message
            if (_broadcastHandlers.TryGetValue(key, out HashSet<ClientBroadcastDelegate> handlers))
            {
                int readerStartPosition = reader.Position;
                /* //muchlater resetting the position could be better by instead reading once and passing in
                 * the object to invoke with. */
                foreach (ClientBroadcastDelegate handler in handlers)
                {
                    reader.Position = readerStartPosition;
                    //Find connection sending broadcast.
                    NetworkConnection conn;
                    if (!Clients.TryGetValue(connectionId, out conn))
                        conn = NetworkManager.EmptyConnection;

                    handler.Invoke(conn, reader);
                }
            }
        }

        /// <summary>
        /// Sends a Broadcast to connection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="message"></param>
        /// <param name="requireAuthenticated">True if the broadcast can only go to an authenticated connection.</param>
        /// <param name="channel"></param>
        public void Broadcast<T>(NetworkConnection connection, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }
            if (requireAuthenticated && !connection.Authenticated)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"Cannot send broadcast to client because they are not authenticated.");
                return;
            }

            using (PooledWriter writer = WriterPool.GetWriter())
            {
                Broadcasts.WriteBroadcast<T>(writer, message, channel);
                ArraySegment<byte> segment = writer.GetArraySegment();

                NetworkManager.TransportManager.SendToClient((byte)channel, segment, connection);
            }
        }

        /// <summary>
        /// Sends a Broadcast to connections.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connections"></param>
        /// <param name="message"></param>
        /// <param name="requireAuthenticated">True if the broadcast can only go to an authenticated connection.</param>
        /// <param name="channel"></param>
        public void Broadcast<T>(HashSet<NetworkConnection> connections, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }

            bool failedAuthentication = false;
            using (PooledWriter writer = WriterPool.GetWriter())
            {
                Broadcasts.WriteBroadcast<T>(writer, message, channel);
                ArraySegment<byte> segment = writer.GetArraySegment();

                foreach (NetworkConnection conn in connections)
                {
                    if (requireAuthenticated && !conn.Authenticated)
                        failedAuthentication = true;
                    else
                        NetworkManager.TransportManager.SendToClient((byte)channel, segment, conn);
                }
            }

            if (failedAuthentication)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"One or more broadcast did not send to a client because they were not authenticated.");
                return;
            }
        }

        /// <summary>
        /// Sends a Broadcast to observers for networkObject.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="networkObject"></param>
        /// <param name="message"></param>
        /// <param name="requireAuthenticated">True if the broadcast can only go to an authenticated connection.</param>
        /// <param name="channel"></param>
        public void Broadcast<T>(NetworkObject networkObject, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (networkObject == null)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"Cannot send broadcast because networkObject is null.");
                return;
            }

            Broadcast(networkObject.Observers, message, requireAuthenticated, channel);
        }


        /// <summary>
        /// Sends a broadcast to all clients.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <param name="requireAuthenticated">True if the broadcast can only go to an authenticated connection.</param>
        /// <param name="channel"></param>
        public void Broadcast<T>(T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }

            bool failedAuthentication = false;
            using (PooledWriter writer = WriterPool.GetWriter())
            {
                Broadcasts.WriteBroadcast<T>(writer, message, channel);
                ArraySegment<byte> segment = writer.GetArraySegment();
                
                foreach (NetworkConnection conn in Clients.Values)
                {
                    //
                    if (requireAuthenticated && !conn.Authenticated)
                        failedAuthentication = true;
                    else
                        NetworkManager.TransportManager.SendToClient((byte)channel, segment, conn);
                }
            }

            if (failedAuthentication)
            {
                if (NetworkManager.CanLog(Logging.LoggingType.Warning))
                    Debug.LogWarning($"One or more broadcast did not send to a client because they were not authenticated.");
                return;
            }
        }

    }


}
