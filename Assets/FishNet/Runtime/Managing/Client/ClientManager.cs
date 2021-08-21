using FishNet.Managing.Client.Object;
using FishNet.Managing.Transporting;
using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Managing.Client
{

    [DisallowMultipleComponent]
    public partial class ClientManager
    {
        #region Public.
        /// <summary>
        /// Called after local clients connections state changes. This is performed after the state change has been handled internally.
        /// </summary>
        public event Action<ClientConnectionStateArgs> OnClientConnectionState;
        /// <summary>
        /// True if the client connection is connected to the server.
        /// </summary>
        public bool Started { get; private set; } = false;
        /// <summary>
        /// NetworkConnection client is using to send data to the server.
        /// </summary>
        public NetworkConnection Connection = null;
        /// <summary>
        /// ObjectHandler for client objects.
        /// </summary>
        public ClientObjects Objects { get; private set; } = null;
        /// <summary>
        /// NetworkManager for client.
        /// </summary>
        public NetworkManager NetworkManager = null;
        /// <summary>
        /// Used to read splits.
        /// </summary>
        private SplitReader _splitReader = new SplitReader();
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void FirstInitialize(NetworkManager manager)
        {
            NetworkManager = manager;
            Objects = new ClientObjects(manager);
            /* Unsubscribe before subscribing.
             * Shouldn't be but better safe than sorry. */
            SubscribeToEvents(false);
            SubscribeToEvents(true);
        }


        /// <summary>
        /// Changes subscription status to transport.
        /// </summary>
        /// <param name="subscribe"></param>
        private void SubscribeToEvents(bool subscribe)
        {
            if (NetworkManager == null || NetworkManager.TransportManager == null || NetworkManager.TransportManager.Transport == null)
                return;

            if (!subscribe)
            {
                NetworkManager.TransportManager.OnIterateIncomingEnd -= TransportManager_OnIterateIncomingEnd;
                NetworkManager.TransportManager.Transport.OnClientReceivedData -= Transport_OnClientReceivedData;
                NetworkManager.TransportManager.Transport.OnClientConnectionState -= Transport_OnClientConnectionState;
            }
            else
            {
                NetworkManager.TransportManager.OnIterateIncomingEnd += TransportManager_OnIterateIncomingEnd;
                NetworkManager.TransportManager.Transport.OnClientReceivedData += Transport_OnClientReceivedData;
                NetworkManager.TransportManager.Transport.OnClientConnectionState += Transport_OnClientConnectionState;
            }
        }

        /// <summary>
        /// Called when a connection state changes local server or client, or a remote client.
        /// </summary>
        /// <param name="args"></param>
        private void Transport_OnClientConnectionState(ClientConnectionStateArgs args)
        {
            Started = (args.ConnectionState == LocalConnectionStates.Started);
            Objects.OnClientConnectionState(args);
            //Clear connection after so objects can update using current Connection value.
            if (!Started)
                Connection = null;
            else
                Debug.Log("Client connected to server."); //tmp.

            OnClientConnectionState?.Invoke(args);
        }

        /// <summary>
        /// Called when a socket receives data.
        /// </summary>
        private void Transport_OnClientReceivedData(ClientReceivedDataArgs args)
        {
            ParseReceived(args);
        }

        /// <summary>
        /// Called after IterateIncoming has completed.
        /// </summary>
        private void TransportManager_OnIterateIncomingEnd()
        {
            /* Should the last packet received be a spawn or despawn
             * then the cache won't yet be iterated because it only
             * iterates when a packet is anything but those two. Because
             * of such if any object caches did come in they must be iterated
             * at the end of the incoming cycle. This isn't as clean as I'd
             * like but it does ensure there will be no missing network object
             * references on spawned objects. */
            if (Started)
                Objects.IterateObjectCache();
        }


        /// <summary>
        /// Parses received data.
        /// </summary>
        private void ParseReceived(ClientReceivedDataArgs args)
        {
            ArraySegment<byte> segment = args.Data;
            if (segment.Count == 0)
                return;

            using (PooledReader reader = ReaderPool.GetReader(segment, NetworkManager))
            {
                /* This is a special condition where a message may arrive split.
                 * When this occurs buffer each packet until all packets are
                 * received. */
                if ((PacketId)reader.PeekByte() == PacketId.Split)
                {
                    ArraySegment<byte> result =
                        _splitReader.Write(reader,
                        NetworkManager.TransportManager.Transport.GetMTU((byte)args.Channel)
                        );

                    /* If there is no data in result then the split isn't fully received.
                     * Since splits arrive in reliable order exit method and wait for next
                     * packet. Once all packets are received the data will be processed. */
                    if (result.Count == 0)
                        return;
                    //Split has been read in full.
                    else
                        reader.Initialize(result, NetworkManager);
                }

                while (reader.Remaining > 0)
                {
                    PacketId packetId = (PacketId)reader.ReadByte();
                    bool spawnOrDespawn = (packetId == PacketId.ObjectSpawn || packetId == PacketId.ObjectDespawn);
                    //Is spawn or despawn; cache packet.
                    if (spawnOrDespawn)
                    {
                        if (packetId == PacketId.ObjectSpawn)
                            Objects.CacheSpawn(reader);
                        else if (packetId == PacketId.ObjectDespawn)
                            Objects.CacheDespawn(reader);
                    }
                    //Not spawn or despawn.
                    else
                    {
                        /* Iterate object cache should any of the
                         * incoming packets rely on it. Objects
                         * in cache will always be received before any messages
                         * that use them. */
                        Objects.IterateObjectCache();
                        //Then process packet normally.
                        if (packetId == PacketId.ObserversRpc)
                        {
                            Objects.ParseObserversRpc(reader);
                        }
                        else if (packetId == PacketId.TargetRpc)
                        {
                            Objects.ParseTargetRpc(reader);
                        }
                        else if (packetId == PacketId.Broadcast)
                        {
                            ParseBroadcast(reader);
                        }
                        else if (packetId == PacketId.SyncVar)
                        {
                            Objects.ParseSyncType(reader, false);
                        }
                        else if (packetId == PacketId.SyncObject)
                        {
                            Objects.ParseSyncType(reader, true);
                        }
                        else if (packetId == PacketId.OwnershipChange)
                        {
                            Objects.ParseOwnershipChange(reader);
                        }
                        else if (packetId == PacketId.ConnectionId)
                        {
                            ParseConnectionId(reader);
                        }
                        else
                        {
                            Debug.LogError($"Unhandled PacketId of {(byte)packetId}. Remaining data has been purged.");
                            return;
                        }
                    }

                }

                /* Iterate cache when reader is emptied.
                 * This is incase the last packet received
                 * was a spawned, which wouldn't trigger
                 * the above iteration. There's no harm
                 * in doing this check multiple times as there's
                 * an exit early check. */
                Objects.IterateObjectCache();
            }
        }

        /// <summary>
        /// Parses a received connectionId. This is received before client receives connection state change.
        /// </summary>
        /// <param name="reader"></param>
        private void ParseConnectionId(PooledReader reader)
        {
            int connectionId = reader.ReadInt32();
            Connection = new NetworkConnection(NetworkManager, connectionId);
        }

    }

}
