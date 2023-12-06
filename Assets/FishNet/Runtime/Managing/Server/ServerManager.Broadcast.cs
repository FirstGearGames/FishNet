using FishNet.Broadcast;
using FishNet.Broadcast.Helping;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Transporting;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using GameKit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public sealed partial class ServerManager : MonoBehaviour
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
        /// <summary>
        /// Connections which can be broadcasted to after having excluded removed.
        /// </summary>
        private HashSet<NetworkConnection> _connectionsWithoutExclusions = new HashSet<NetworkConnection>();
        #endregion

        /// <summary>
        /// Registers a method to call when a Broadcast arrives.
        /// </summary>
        /// <typeparam name="T">Type of broadcast being registered.</typeparam>
        /// <param name="handler">Method to call.</param>
        /// <param name="requireAuthentication">True if the client must be authenticated for the method to call.</param>
        public void RegisterBroadcast<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true) where T : struct, IBroadcast
        {
            if (handler == null)
            {
                NetworkManager.LogError($"Broadcast cannot be registered because handler is null. This may occur when trying to register to objects which require initialization, such as events.");
                return;
            }

            ushort key = BroadcastHelper.GetKey<T>();

            /* Create delegate and add for
             * handler method. */
            HashSet<ClientBroadcastDelegate> handlers;
            if (!_broadcastHandlers.TryGetValueIL2CPP(key, out handlers))
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
            if (!_handlerTargets.TryGetValueIL2CPP(key, out targetHashCodes))
            {
                targetHashCodes = new HashSet<(int, ClientBroadcastDelegate)>();
                _handlerTargets.Add(key, targetHashCodes);
            }

            targetHashCodes.Add((handlerHashCode, del));
        }

        /// <summary>
        /// Unregisters a method call from a Broadcast type.
        /// </summary>
        /// <typeparam name="T">Type of broadcast being unregistered.</typeparam>
        /// <param name="handler">Method to unregister.</param>
        public void UnregisterBroadcast<T>(Action<NetworkConnection, T> handler) where T : struct, IBroadcast
        {
            ushort key = BroadcastHelper.GetKey<T>();

            /* If key is found for T then look for
             * the appropriate handler to remove. */
            if (_broadcastHandlers.TryGetValueIL2CPP(key, out HashSet<ClientBroadcastDelegate> handlers))
            {
                HashSet<(int, ClientBroadcastDelegate)> targetHashCodes;
                if (_handlerTargets.TryGetValueIL2CPP(key, out targetHashCodes))
                {
                    int handlerHashCode = handler.GetHashCode();
                    ClientBroadcastDelegate result = null;
                    foreach ((int targetHashCode, ClientBroadcastDelegate del) in targetHashCodes)
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
                    connection.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {connection.ClientId} sent broadcast {typeof(T).Name} which requires authentication, but client was not authenticated. Client has been disconnected.");
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ParseBroadcast(PooledReader reader, NetworkConnection conn, Channel channel)
        {
            ushort key = reader.ReadUInt16();
            int dataLength = Packets.GetPacketLength((ushort)PacketId.Broadcast, reader, channel);

            //Try to invoke the handler for that message
            if (_broadcastHandlers.TryGetValueIL2CPP(key, out HashSet<ClientBroadcastDelegate> handlers))
            {
                int readerStartPosition = reader.Position;
                /* //muchlater resetting the position could be better by instead reading once and passing in
                 * the object to invoke with. */
                bool rebuildHandlers = false;
                //True if data is read at least once. Otherwise it's length will have to be purged.
                bool dataRead = false;
                foreach (ClientBroadcastDelegate handler in handlers)
                {
                    if (handler.Target == null)
                    {
                        NetworkManager.LogWarning($"A Broadcast handler target is null. This can occur when a script is destroyed but does not unregister from a Broadcast.");
                        rebuildHandlers = true;
                    }
                    else
                    {
                        reader.Position = readerStartPosition;
                        handler.Invoke(conn, reader);
                        dataRead = true;
                    }
                }

                //If rebuilding handlers...
                if (rebuildHandlers)
                {
                    List<ClientBroadcastDelegate> dels = handlers.ToList();
                    handlers.Clear();
                    for (int i = 0; i < dels.Count; i++)
                    {
                        if (dels[i].Target != null)
                            handlers.Add(dels[i]);
                    }
                }
                //Make sure data was read as well.
                if (!dataRead)
                    reader.Skip(dataLength);
            }
            else
            {
                reader.Skip(dataLength);
            }
        }

        /// <summary>
        /// Sends a broadcast to a connection.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="connection">Connection to send to.</param>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the client must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        public void Broadcast<T>(NetworkConnection connection, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }
            if (requireAuthenticated && !connection.Authenticated)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to client because they are not authenticated.");
                return;
            }

            PooledWriter writer = WriterPool.Retrieve();
            Broadcasts.WriteBroadcast<T>(NetworkManager, writer, message, ref channel);
            ArraySegment<byte> segment = writer.GetArraySegment();
            NetworkManager.TransportManager.SendToClient((byte)channel, segment, connection);
            writer.Store();
        }


        /// <summary>
        /// Sends a broadcast to connections.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="connections">Connections to send to.</param>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the clients must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        public void Broadcast<T>(HashSet<NetworkConnection> connections, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }

            bool failedAuthentication = false;
            PooledWriter writer = WriterPool.Retrieve();
            Broadcasts.WriteBroadcast<T>(NetworkManager, writer, message, ref channel);
            ArraySegment<byte> segment = writer.GetArraySegment();

            foreach (NetworkConnection conn in connections)
            {
                if (requireAuthenticated && !conn.Authenticated)
                    failedAuthentication = true;
                else
                    NetworkManager.TransportManager.SendToClient((byte)channel, segment, conn);
            }
            writer.Store();

            if (failedAuthentication)
            {
                NetworkManager.LogWarning($"One or more broadcast did not send to a client because they were not authenticated.");
                return;
            }
        }


        /// <summary>
        /// Sends a broadcast to connections except excluded.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="connections">Connections to send to.</param>
        /// <param name="excludedConnection">Connection to exclude.</param>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the clients must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        public void BroadcastExcept<T>(HashSet<NetworkConnection> connections, NetworkConnection excludedConnection, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }

            //Fast exit if no exclusions.
            if (excludedConnection == null || !excludedConnection.IsValid)
            {
                Broadcast(connections, message, requireAuthenticated, channel);
                return;
            }

            connections.Remove(excludedConnection);
            Broadcast(connections, message, requireAuthenticated, channel);
        }


        /// <summary>
        /// Sends a broadcast to connections except excluded.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="connections">Connections to send to.</param>
        /// <param name="excludedConnections">Connections to exclude.</param>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the clients must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        public void BroadcastExcept<T>(HashSet<NetworkConnection> connections, HashSet<NetworkConnection> excludedConnections, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }

            //Fast exit if no exclusions.
            if (excludedConnections == null || excludedConnections.Count == 0)
            {
                Broadcast(connections, message, requireAuthenticated, channel);
                return;
            }

            /* I'm not sure if the hashset API such as intersect generates
             * GC or not but I'm betting doing remove locally is faster, or
             * just as fast. */
            foreach (NetworkConnection ec in excludedConnections)
                connections.Remove(ec);

            Broadcast(connections, message, requireAuthenticated, channel);
        }

        /// <summary>
        /// Sends a broadcast to all connections except excluded.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="excludedConnection">Connection to exclude.</param>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the clients must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        public void BroadcastExcept<T>(NetworkConnection excludedConnection, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }

            //Fast exit if there are no excluded.
            if (excludedConnection == null || !excludedConnection.IsValid)
            {
                Broadcast(message, requireAuthenticated, channel);
                return;
            }

            _connectionsWithoutExclusions.Clear();
            /* It will be faster to fill the entire list then
             * remove vs checking if each connection is contained within excluded. */
            foreach (NetworkConnection c in Clients.Values)
                _connectionsWithoutExclusions.Add(c);
            //Remove
            _connectionsWithoutExclusions.Remove(excludedConnection);

            Broadcast(_connectionsWithoutExclusions, message, requireAuthenticated, channel);
        }

        /// <summary>
        /// Sends a broadcast to all connections except excluded.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="excludedConnections">Connections to send to.</param>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the clients must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        public void BroadcastExcept<T>(HashSet<NetworkConnection> excludedConnections, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }

            //Fast exit if there are no excluded.
            if (excludedConnections == null || excludedConnections.Count == 0)
            {
                Broadcast(message, requireAuthenticated, channel);
                return;
            }

            _connectionsWithoutExclusions.Clear();
            /* It will be faster to fill the entire list then
             * remove vs checking if each connection is contained within excluded. */
            foreach (NetworkConnection c in Clients.Values)
                _connectionsWithoutExclusions.Add(c);
            //Remove
            foreach (NetworkConnection c in excludedConnections)
                _connectionsWithoutExclusions.Remove(c);

            Broadcast(_connectionsWithoutExclusions, message, requireAuthenticated, channel);
        }

        /// <summary>
        /// Sends a broadcast to observers.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="networkObject">NetworkObject to use Observers from.</param>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the clients must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Broadcast<T>(NetworkObject networkObject, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (networkObject == null)
            {
                NetworkManager.LogWarning($"Cannot send broadcast because networkObject is null.");
                return;
            }

            Broadcast(networkObject.Observers, message, requireAuthenticated, channel);
        }


        /// <summary>
        /// Sends a broadcast to all clients.
        /// </summary>
        /// <typeparam name="T">Type of broadcast to send.</typeparam>
        /// <param name="message">Broadcast data being sent; for example: an instance of your broadcast type.</param>
        /// <param name="requireAuthenticated">True if the clients must be authenticated for this broadcast to send.</param>
        /// <param name="channel">Channel to send on.</param>
        public void Broadcast<T>(T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
        {
            if (!Started)
            {
                NetworkManager.LogWarning($"Cannot send broadcast to client because server is not active.");
                return;
            }

            bool failedAuthentication = false;
            PooledWriter writer = WriterPool.Retrieve();
            Broadcasts.WriteBroadcast<T>(NetworkManager, writer, message, ref channel);
            ArraySegment<byte> segment = writer.GetArraySegment();

            foreach (NetworkConnection conn in Clients.Values)
            {
                //
                if (requireAuthenticated && !conn.Authenticated)
                    failedAuthentication = true;
                else
                    NetworkManager.TransportManager.SendToClient((byte)channel, segment, conn);
            }
            writer.Store();

            if (failedAuthentication)
            {
                NetworkManager.LogWarning($"One or more broadcast did not send to a client because they were not authenticated.");
                return;
            }
        }

    }


}
