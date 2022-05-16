using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Serializing;
using FishNet.Utility.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Transporting.Multipass
{
    public class Multipass : Transport
    {
        #region Types.
        public struct TransportIdData
        {
            public int MultipassId;
            public int TransportIndex;

            public TransportIdData(int multipassId, int transportIndex)
            {
                MultipassId = multipassId;
                TransportIndex = transportIndex;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// While true server actions such as starting or stopping the server will run on all transport.
        /// </summary>
        [Tooltip("While true server actions such as starting or stopping the server will run on all transport.")]
        public bool GlobalServerActions = true;
        /// <summary>
        /// 
        /// </summary>
        private Transport _clientTransport;
        /// <summary>
        /// Transport the client is using.
        /// Use SetClientTransport to assign this value.
        /// </summary>
        [HideInInspector]
        public Transport ClientTransport
        {
            get
            {
                //If not yet set.
                if (_clientTransport == null)
                {
                    bool canLogError = base.NetworkManager.CanLog(LoggingType.Error);
                    //If there are transports to set from.
                    if (_transports.Count != 0)
                        _clientTransport = _transports[0];

                    /* Give feedback to developer that transport was not set
                    * before accessing this. Transport should always be set
                    * manually rather than assuming the default client
                    * transport. */
                    if (canLogError)
                    {
                        if (_clientTransport == null)
                            Debug.LogError($"ClientTransport in Multipass could not be set to the first transport. This can occur if no trnasports are specified or if the first entry is null.");
                        else
                            Debug.LogError($"ClientTransport in Multipass is being automatically set to {_clientTransport.GetType()}. For production use SetClientTransport before attempting to access the ClientTransport.");
                    }
                }

                return _clientTransport;
            }

            private set
            {
                _clientTransport = value;
            }

        }
        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Transports to use.")]
        [SerializeField]
        private List<Transport> _transports = new List<Transport>();
        /// <summary>
        /// Transports to use.
        /// </summary>
        public IReadOnlyCollection<Transport> Transports => _transports;
        #endregion

        #region Private. 
        /// <summary>
        /// Key is the transport connectionid, Value is the TransportIdData.
        /// </summary>
        private Dictionary<int, TransportIdData> _multipassToTransport = new Dictionary<int, TransportIdData>();
        /// <summary>
        /// Key is the Multipass connectionId, Value is the transport connectionId.
        /// </summary>
        private List<Dictionary<int, int>> _transportToMultipass = new List<Dictionary<int, int>>();
        /// <summary>
        /// Ids available to new connections.
        /// </summary>
        private Queue<int> _availableIds = new Queue<int>();
        #endregion

        #region Const.
        /// <summary>
        /// Id to use for client when acting as host.
        /// </summary>
        internal const int CLIENT_HOST_ID = short.MaxValue;
        #endregion

        public override void Initialize(NetworkManager networkManager, int transportIndex)
        {
            base.Initialize(networkManager, transportIndex);

            //Remove any null transports and warn.
            for (int i = 0; i < _transports.Count; i++)
            {
                if (_transports[i] == null)
                {
                    if (base.NetworkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Transports contains a null entry on index {i}.");
                    _transports.RemoveAt(i);
                    i--;
                }
            }

            //No transports to use.
            if (Transports.Count == 0)
            {
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"No transports are set within Multipass.");
                return;
            }

            //Create transportsToMultipass.
            for (int i = 0; i < Transports.Count; i++)
            {
                Dictionary<int, int> dict = new Dictionary<int, int>();
                _transportToMultipass.Add(dict);
            }

            //Initialize each transport.
            for (int i = 0; i < Transports.Count; i++)
            {
                _transports[i].Initialize(networkManager, i);
                _transports[i].OnClientConnectionState += Multipass_OnClientConnectionState;
                _transports[i].OnServerConnectionState += Multipass_OnServerConnectionState;
                _transports[i].OnRemoteConnectionState += Multipass_OnRemoteConnectionState;
                _transports[i].OnClientReceivedData += Multipass_OnClientReceivedData;
                _transports[i].OnServerReceivedData += Multipass_OnServerReceivedData;
            }
        }

        private void OnDestroy()
        {
            //Initialize each transport.
            foreach (Transport t in Transports)
                t.Shutdown();
        }


        #region ClientIds.
        /// <summary>
        /// Clears ClientIds when appropriate.
        /// </summary>
        private void TryResetClientIds(bool force)
        {
            //Can only clear when every transport server isnt connected.
            if (!force)
            {
                foreach (Transport t in Transports)
                {
                    //Cannot clear if a server is running still.
                    if (t.GetConnectionState(true) == LocalConnectionStates.Started)
                        return;
                }
            }

            _multipassToTransport.Clear();
            foreach (Dictionary<int, int> item in _transportToMultipass)
                item.Clear();
            CreateAvailableIds();
        }

        /// <summary>
        /// Gets the Multipass connectionId using a transport connectionid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetMultipassId(int transportIndex, int transportId, out int multipassId)
        {
            Dictionary<int, int> dict = _transportToMultipass[transportIndex];
            if (!dict.TryGetValueIL2CPP(transportId, out multipassId))
            {
                multipassId = -1;
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Multipass connectionId could not be found for transportIndex {transportIndex}, connectionId of {transportId}.");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the TransportIdData using a Multipass connectionId.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetTransportIdData(int multipassId, out TransportIdData data)
        {
            if (!_multipassToTransport.TryGetValueIL2CPP(multipassId, out data))
            {
                //Fall through.
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"TransportIdData could not be found for Multiass connectionId of {multipassId}.");
                return false;
            }

            return true;
        }
        #endregion

        #region ConnectionStates.
        /// <summary>
        /// Gets the IP address of a remote connectionId.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string GetConnectionAddress(int connectionId)
        {
            TransportIdData data;
            if (!GetTransportIdData(connectionId, out data))
                return string.Empty;

            return _transports[data.TransportIndex].GetConnectionAddress(connectionId);
        }
        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        /// <summary>
        /// Called when a connection state changes for the local server.
        /// </summary>
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        /// <summary>
        /// Gets the current local ConnectionState of the first transport.
        /// </summary>
        /// <param name="server">True if getting ConnectionState for the server.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override LocalConnectionStates GetConnectionState(bool server)
        {
            if (server)
            {
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"This method is not supported for server. Use GetConnectionState(server, transportIndex) instead.");
                return LocalConnectionStates.Stopped;
            }

            if (IsClientTransportSetWithError("GetConnectionState"))
                return GetConnectionState(server, ClientTransport.Index);
            else
                return LocalConnectionStates.Stopped;
        }
        /// <summary>
        /// Gets the current local ConnectionState of the transport on index.
        /// </summary>
        /// <param name="server">True if getting ConnectionState for the server.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LocalConnectionStates GetConnectionState(bool server, int index)
        {
            if (!IndexInRange(index, true))
                return LocalConnectionStates.Stopped;

            return _transports[index].GetConnectionState(server);
        }
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override RemoteConnectionStates GetConnectionState(int connectionId)
        {

            TransportIdData data;
            if (!GetTransportIdData(connectionId, out data))
                return RemoteConnectionStates.Stopped;

            return _transports[data.TransportIndex].GetConnectionState(connectionId);
        }
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server of the transport on index.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RemoteConnectionStates GetConnectionState(int connectionId, int index)
        {
            if (!IndexInRange(index, true))
                return RemoteConnectionStates.Stopped;

            return _transports[index].GetConnectionState(connectionId);
        }


        /// <summary>
        /// Handles a ConnectionStateArgs for the local client.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        private void Multipass_OnClientConnectionState(ClientConnectionStateArgs connectionStateArgs)
        {
            OnClientConnectionState?.Invoke(connectionStateArgs);
        }
        /// <summary>
        /// Handles a ConnectionStateArgs for the local server.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        private void Multipass_OnServerConnectionState(ServerConnectionStateArgs connectionStateArgs)
        {
            OnServerConnectionState?.Invoke(connectionStateArgs);
            TryResetClientIds(false);
        }
        /// <summary>
        /// Handles a ConnectionStateArgs for a remote client.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        private void Multipass_OnRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs)
        {
            /* When starting Multipass needs to get a new
            * connectionId to be used within FN. This is the 'ClientId'
             * that is passed around for ownership, rpcs, ect.
             * 
             * The new connectionId will be linked with the connectionId
             * from the transport, named transportConnectionid. 
             * 
             * When data arrives the transportStateId is used as a key
             * in fromClientIds, where Multipass Id is returned. The argument values
             * are then overwritten with the MultipassId.
             * 
             * When data is being sent the same process is performed but reversed.
             * The connectionId is looked up in toClientIds, where the transportConnectionId
             * is output. Then as before the argument values are overwritten with the
             * transportConnectionId. */

            int transportIndex = connectionStateArgs.TransportIndex;
            int transportId = connectionStateArgs.ConnectionId;
            int multipassId;
            Dictionary<int, int> transportToMultipassDict = _transportToMultipass[transportIndex];

            //Started.
            if (connectionStateArgs.ConnectionState == RemoteConnectionStates.Started)
            {
                multipassId = _availableIds.Dequeue();
                transportToMultipassDict[transportId] = multipassId;
                _multipassToTransport[multipassId] = new TransportIdData(transportId, transportIndex);
            }
            //Stopped.
            else
            {
                if (!GetMultipassId(transportIndex, transportId, out multipassId))
                    return;

                _availableIds.Enqueue(multipassId);
                transportToMultipassDict.Remove(transportId);
                _multipassToTransport.Remove(multipassId);
            }

            connectionStateArgs.ConnectionId = multipassId;
            OnRemoteConnectionState?.Invoke(connectionStateArgs);
        }
        #endregion

        #region Iterating.
        /// <summary>
        /// Processes data received by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public override void IterateIncoming(bool server)
        {
            foreach (Transport t in Transports)
                t.IterateIncoming(server);
        }

        /// <summary>
        /// Processes data to be sent by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public override void IterateOutgoing(bool server)
        {
            foreach (Transport t in Transports)
                t.IterateOutgoing(server);
        }
        #endregion

        #region ReceivedData.
        /// <summary>
        /// Called when client receives data.
        /// </summary>
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        /// <summary>
        /// Handles a ClientReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs"></param>
        private void Multipass_OnClientReceivedData(ClientReceivedDataArgs receivedDataArgs)
        {
            OnClientReceivedData?.Invoke(receivedDataArgs);
        }
        /// <summary>
        /// Called when server receives data.
        /// </summary>
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;
        /// <summary>
        /// Handles a ClientReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs"></param>
        private void Multipass_OnServerReceivedData(ServerReceivedDataArgs receivedDataArgs)
        {
            int multipassId;
            if (!GetMultipassId(receivedDataArgs.TransportIndex, receivedDataArgs.ConnectionId, out multipassId))
                return;

            receivedDataArgs.ConnectionId = multipassId;
            OnServerReceivedData?.Invoke(receivedDataArgs);
        }
        #endregion

        #region Sending.
        /// <summary>
        /// Sends to the server on ClientTransport.
        /// </summary>
        /// <param name="channelId">Channel to use.</param>
        /// /// <param name="segment">Data to send.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (ClientTransport != null)
                ClientTransport.SendToServer(channelId, segment);
        }
        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <param name="connectionId"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            TransportIdData data;
            if (GetTransportIdData(connectionId, out data))
                _transports[data.TransportIndex].SendToClient(channelId, segment, data.MultipassId);
        }
        #endregion

        #region Configuration.
        /// <summary>
        /// Returns if GlobalServerActions is true and if not logs an error.
        /// </summary>
        /// <returns></returns>
        private bool UseGlobalServerActionsWithError(string methodText)
        {
            if (!GlobalServerActions)
            {
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Method {methodText} is not supported while GlobalServerActions is false.");
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Returns if ClientTransport is set and if not logs an error.
        /// </summary>
        /// <param name="methodText"></param>
        /// <returns></returns>
        private bool IsClientTransportSetWithError(string methodText)
        {
            if (ClientTransport == null)
            {
                if (base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"ClientTransport is not set. Use SetClientTransport before calling {methodText}.");
                return false;
            }
            else
            {
                return true;
            }
        }
        /// <summary>
        /// Populates the availableIds collection.
        /// </summary>
        private void CreateAvailableIds()
        {
            _availableIds.Clear();
            for (int i = 0; i < short.MaxValue; i++)
                _availableIds.Enqueue(i);
        }

        /// <summary>
        /// Sets the client transport to the first of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void SetClientTransport<T>()
        {
            int index = -1;
            for (int i = 0; i < _transports.Count; i++)
            {
                if (_transports[i].GetType() == typeof(T))
                {
                    index = i;
                    break;
                }
            }

            SetClientTransport(index);
        }
        /// <summary>
        /// Sets the client transport to the matching reference of transport.
        /// </summary>
        /// <param name="transport"></param>
        public void SetClientTransport(Transport transport)
        {
            int index = -1;
            for (int i = 0; i < _transports.Count; i++)
            {
                if (_transports[i] == transport)
                {
                    index = i;
                    break;
                }
            }

            SetClientTransport(index);
        }
        /// <summary>
        /// Sets the client transport to the transport on index.
        /// </summary>
        /// <param name="index"></param>
        public void SetClientTransport(int index)
        {
            if (!IndexInRange(index, true))
                return;

            ClientTransport = _transports[index];
        }
        /// <summary>
        /// Gets the Transport on index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Transport GetTransport(int index)
        {
            if (!IndexInRange(index, true))
                return null;

            return _transports[index];
        }
        /// <summary>
        /// Gets the Transport on of type T.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Transport GetTransport<T>()
        {
            foreach (Transport t in Transports)
            {
                if (t.GetType() == typeof(T))
                    return t;
            }

            return null;
        }
        /// <summary>
        /// Returns if the transport for connectId is a local transport.
        /// While true several security checks are disabled.
        /// </summary>
        public override bool IsLocalTransport(int connectionid)
        {
            //If able to get transport data return value from transport.
            if (GetTransportIdData(connectionid, out TransportIdData data))
                return _transports[data.TransportIndex].IsLocalTransport(connectionid);
            //Otherwise return false forcing checks.
            else
                return false;
        }

        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server. If the transport does not support this method the value -1 is returned.
        /// The first transport is used.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetMaximumClients()
        {
            if (base.NetworkManager.CanLog(LoggingType.Error))
                Debug.LogError($"This method is not supported. Use GetMaximumClients(transportIndex) instead.");

            return -1;
        }
        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server. If the transport does not support this method the value -1 is returned.
        /// The first transport is used.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMaximumClients(int transportIndex)
        {
            if (!IndexInRange(transportIndex, true))
                return -1;

            return _transports[transportIndex].GetMaximumClients();
        }
        /// <summary>
        /// Sets maximum number of clients allowed to connect to the server. If applied at runtime and clients exceed this value existing clients will stay connected but new clients may not connect.
        /// This sets the value to the transport on the first index.
        /// </summary>
        /// <param name="value"></param>
        public override void SetMaximumClients(int value)
        {
            if (base.NetworkManager.CanLog(LoggingType.Error))
                Debug.LogError($"This method is not supported. Use SetMaximumClients(value, transportIndex) instead.");
        }
        /// <summary>
        /// Sets maximum number of clients allowed to connect to the server. If applied at runtime and clients exceed this value existing clients will stay connected but new clients may not connect.
        /// This sets the value to the transport on index.
        /// </summary>
        /// <param name="value"></param>
        public void SetMaximumClients(int value, int transportIndex)
        {
            if (!IndexInRange(transportIndex, true))
                return;

            _transports[transportIndex].SetMaximumClients(value);
        }
        /// <summary>
        /// Sets which address the client will connect to.
        /// This will set the address for every transport.
        /// </summary>
        /// <param name="address"></param>
        public override void SetClientAddress(string address)
        {
            foreach (Transport t in Transports)
                t.SetClientAddress(address);
        }
        public override void SetServerBindAddress(string address, IPAddressType addressType)
        {
            if (base.NetworkManager.CanLog(LoggingType.Error))
                Debug.LogError($"This method is not supported. Use SetServerBindAddress(address, transportIndex) instead.");
        }
        /// Sets which address the server will bind to.
        /// This is called on the transport of index.
        /// </summary>
        /// <param name="address"></param>
        public void SetServerBindAddress(string address, IPAddressType addressType, int index)
        {
            if (!IndexInRange(index, true))
                return;

            _transports[index].SetServerBindAddress(address, addressType);
        }
        /// <summary>
        /// Sets which port to use on the first transport.
        /// </summary>
        public override void SetPort(ushort port)
        {
            if (base.NetworkManager.CanLog(LoggingType.Error))
                Debug.LogError($"This method is not supported. Use SetPort(port, transportIndex) instead.");
        }
        /// <summary>
        /// Sets which port to use on transport of index.
        /// </summary>
        public void SetPort(ushort port, int index)
        {
            if (!IndexInRange(index, true))
                return;

            _transports[index].SetPort(port);
        }
        #endregion

        #region Start and stop.
        /// <summary>
        /// Starts the local server or client using configured settings on the first transport.
        /// </summary>
        /// <param name="server">True to start server.</param>
        public override bool StartConnection(bool server)
        {
            //Server.
            if (server)
            {
                if (!UseGlobalServerActionsWithError("StartConnection"))
                    return false;

                bool success = true;
                for (int i = 0; i < Transports.Count; i++)
                {
                    if (!StartConnection(true, i))
                        success = false;
                }

                return success;
            }
            //Client.
            else
            {
                if (IsClientTransportSetWithError("StartConnection"))
                    return StartConnection(false, ClientTransport.Index);
                else
                    return false;
            }
        }

        /// <summary>
        /// Starts the local server or client using configured settings on transport of index.
        /// </summary>
        /// <param name="server">True to start server.</param>
        public bool StartConnection(bool server, int index)
        {
            if (server)
            { 
                return StartServer(index);
            }
            else
            {
                if (IsClientTransportSetWithError("StartConnection"))
                    return StartClient();
                else
                    return false;
            }
        }


        /// <summary>
        /// Stops the local server or client on the first transport.
        /// </summary>
        /// <param name="server">True to stop server.</param>
        public override bool StopConnection(bool server)
        {
            //Server
            if (server)
            {
                if (!UseGlobalServerActionsWithError("StopConnection"))
                    return false;

                bool success = true;
                for (int i = 0; i < Transports.Count; i++)
                {
                    if (!StopConnection(true, i))
                        success = false;
                }

                return success;
            }
            //Client.
            else
            {
                if (IsClientTransportSetWithError("StopConnection"))
                    return StopConnection(false, ClientTransport.Index);
                else
                    return false;
            }
        }
        /// <summary>
        /// Stops the local server or client on transport of index.
        /// </summary>
        /// <param name="server">True to stop server.</param>
        public bool StopConnection(bool server, int index)
        {
            if (server)
            {
                return StopServer(index);
            }
            else
            {
                if (IsClientTransportSetWithError("StopConnection"))
                    return StopClient();
                else
                    return false;
            }
        }

        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        public override bool StopConnection(int connectionId, bool immediately)
        {
            return StopClient(connectionId, immediately);
        }

        /// <summary>
        /// Stops the server connection on transportIndex.
        /// </summary>
        /// <param name="sendDisconnectMessage">True to send a disconnect message to connections before stopping them.</param>
        /// <param name="transportIndex">Index of transport to stop on.</param>
        public bool StopServerConnection(bool sendDisconnectMessage, int transportIndex)
        {
            if (sendDisconnectMessage)
            {
                //Get connectionIds as ServerManager knows them.
                int[] multipassIds = _transportToMultipass[transportIndex].Keys.ToArray();
                //Tell serve manager to write disconnect for those ids.
                base.NetworkManager.ServerManager.SendDisconnectMessages(multipassIds);
                //Iterate outgoing on transport which is being stopped.
                _transports[transportIndex].IterateOutgoing(true);
            }

            return StopConnection(true, transportIndex);
        }

        /// <summary>
        /// Stops both client and server on all transports.
        /// </summary>
        public override void Shutdown()
        {
            foreach (Transport t in Transports)
            {
                //Stops client then server connections.
                t.StopConnection(false);
                t.StopConnection(true);
            }
        }

        #region Privates.
        /// <summary>
        /// Starts server of transport on index.
        /// </summary>
        /// <returns>True if there were no blocks. A true response does not promise a socket will or has connected.</returns>
        private bool StartServer(int index)
        {
            if (!IndexInRange(index, true))
                return false;

            return _transports[index].StartConnection(true);
        }

        /// <summary>
        /// Stops server of transport on index.
        /// </summary>
        private bool StopServer(int index)
        {
            if (!IndexInRange(index, true))
                return false;

            return _transports[index].StopConnection(true);
        }

        /// <summary>
        /// Starts the client on ClientTransport.
        /// </summary>
        /// <param name="address"></param>
        /// <returns>True if there were no blocks. A true response does not promise a socket will or has connected.</returns>
        private bool StartClient()
        {
            return ClientTransport.StartConnection(false);
        }

        /// <summary>
        /// Stops the client on ClientTransport.
        /// </summary>
        private bool StopClient()
        {
            return ClientTransport.StopConnection(false);
        }

        /// <summary>
        /// Stops a remote client on the server.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        private bool StopClient(int connectionId, bool immediately)
        {
            TransportIdData data;
            if (!GetTransportIdData(connectionId, out data))
                return false;

            return _transports[data.TransportIndex].StopConnection(connectionId, immediately);
        }
        #endregion
        #endregion

        #region Channels.
        /// <summary>
        /// Gets the MTU for a channel on the first transport. This should take header size into consideration.
        /// For example, if MTU is 1200 and a packet header for this channel is 10 in size, this method should return 1190.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public override int GetMTU(byte channel)
        {
            return GetMTU(channel, 0);
        }
        /// <summary>
        /// Gets the MTU for a channel of transport on index. This should take header size into consideration.
        /// For example, if MTU is 1200 and a packet header for this channel is 10 in size, this method should return 1190.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public int GetMTU(byte channel, int index)
        {
            if (!IndexInRange(index, true))
                return -1;

            return _transports[index].GetMTU(channel);
        }

        #endregion

        #region Misc.
        /// <summary>
        /// Returns if an index is within range of the Transports collection.
        /// </summary>
        private bool IndexInRange(int index, bool error)
        {
            if (index >= Transports.Count || index < 0)
            {
                if (error && base.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Index of {index} is out of Transports range.");
                return false;
            }
            else
            {
                return true;
            }
        }

        //perf change events to direct calls in transports.
        public override void HandleServerConnectionState(ServerConnectionStateArgs connectionStateArgs) { }
        public override void HandleRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs) { }
        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs) { }
        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs receivedDataArgs) { }
        public override void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs) { }
        #endregion

    }
}