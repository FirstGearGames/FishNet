using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.SceneManagement;

namespace FishNet.Connection
{

    /// <summary>
    /// A container for a connected client used to perform actions on and gather information for the declared client.
    /// </summary>
    public partial class NetworkConnection : IEquatable<NetworkConnection>
    {

        #region Public.
        /// <summary>
        /// Called after this connection has loaded start scenes. Boolean will be true if asServer. Available to this connection and server.
        /// </summary>
        public event Action<NetworkConnection, bool> OnLoadedStartScenes;
        /// <summary>
        /// Called after connection gains ownership of an object, and after the object has been added to Objects. Available to this connection and server.
        /// </summary>
        public event Action<NetworkObject> OnObjectAdded;
        /// <summary>
        /// Called after connection loses ownership of an object, and after the object has been removed from Objects. Available to this connection and server.
        /// </summary>
        public event Action<NetworkObject> OnObjectRemoved;
        /// <summary>
        /// NetworkManager managing this class.
        /// </summary>
        public NetworkManager NetworkManager { get; private set; }
        /// <summary>
        /// True if connection has loaded start scenes. Available to this connection and server.
        /// </summary>
        public bool LoadedStartScenes => (_loadedStartScenesAsServer || _loadedStartScenesAsClient);
        /// <summary>
        /// True if loaded start scenes as server.
        /// </summary>
        private bool _loadedStartScenesAsServer;
        /// <summary>
        /// True if loaded start scenes as client.
        /// </summary>
        private bool _loadedStartScenesAsClient;
        /// <summary>
        /// True if this connection is authenticated. Only available to server.
        /// </summary>
        public bool Authenticated { get; private set; }
        /// <summary>
        /// True if this connection IsValid and not Disconnecting.
        /// </summary>
        public bool IsActive => (ClientId >= 0 && !Disconnecting);
        /// <summary>
        /// True if this connection is valid. An invalid connection indicates no client is set for this reference.
        /// </summary>
        public bool IsValid => (ClientId >= 0);
        /// <summary>
        /// Unique Id for this connection.
        /// </summary>
        public int ClientId = -1;
        /// <summary>
        /// 
        /// </summary>
        private HashSet<NetworkObject> _objects = new HashSet<NetworkObject>();
        /// <summary>
        /// Objects owned by this connection. Available to this connection and server.
        /// </summary>
        public IReadOnlyCollection<NetworkObject> Objects => _objects;
        /// <summary>
        /// The first object within Objects.
        /// </summary>
        public NetworkObject FirstObject { get; private set; }
        /// <summary>
        /// Scenes this connection is in. Available to this connection and server.
        /// </summary>
        public HashSet<Scene> Scenes { get; private set; } = new HashSet<Scene>();
        /// <summary>
        /// True if this connection is being disconnected. Only available to server.
        /// </summary>
        public bool Disconnecting { get; private set; }
        /// <summary>
        /// Tick when Disconnecting was set.
        /// </summary>
        internal uint DisconnectingTick { get; private set; }
        /// <summary>
        /// Custom data associated with this connection which may be modified by the user.
        /// The value of this field are not synchronized over the network.
        /// </summary>
        public object CustomData = null;
        /// <summary>
        /// Local tick of the server when this connection last replicated.
        /// </summary>
        public uint LocalReplicateTick { get; internal set; }
        /// <summary>
        /// Tick of the last packet received from this connection.
        /// This value is only available on the server.
        /// </summary>
        /* This is not used internally. At this time it's just
         * here for the users convienence. */
        public uint LastPacketTick { get; private set; }
        /// <summary>
        /// Sets LastPacketTick value.
        /// </summary>
        /// <param name="value"></param>
        internal void SetLastPacketTick(uint value)
        {
            //If new largest tick from the client then update client tick data.
            if (value > LastPacketTick)
            {
                _latestTick = value;
                _serverLatestTick = NetworkManager.TimeManager.Tick;
            }
            LastPacketTick = value;
        }
        /// <summary>
        /// Latest tick that did not arrive out of order from this connection.
        /// </summary>
        private uint _latestTick;
        /// <summary>
        /// Tick on the server when latestTick was set.
        /// </summary>
        private uint _serverLatestTick;
        /// <summary>
        /// Current approximate network tick as it is on this connection.
        /// </summary>
        public uint Tick
        {
            get
            {
                NetworkManager nm = NetworkManager;
                if (nm != null)
                {
                    uint diff = (nm.TimeManager.Tick - _serverLatestTick);
                    return (diff + _latestTick);
                }

                //Fall through, could not process.
                return 0;
            }
        }

        #endregion

        #region Comparers.
        public override bool Equals(object obj)
        {
            if (obj is NetworkConnection nc)
                return (nc.ClientId == this.ClientId);
            else
                return false;
        }
        public bool Equals(NetworkConnection nc)
        {
            if (nc is null)
                return false;
            //If either is -1 Id.
            if (this.ClientId == -1 || nc.ClientId == -1)
                return false;
            //Same object.
            if (System.Object.ReferenceEquals(this, nc))
                return true;

            return (this.ClientId == nc.ClientId);
        }
        public override int GetHashCode()
        {
            return ClientId;
        }
        public static bool operator ==(NetworkConnection a, NetworkConnection b)
        {
            if (a is null && b is null)
                return true;
            if (a is null && !(b is null))
                return false;

            return (b == null) ? a.Equals(b) : b.Equals(a);
        }
        public static bool operator !=(NetworkConnection a, NetworkConnection b)
        {
            return !(a == b);
        }
        #endregion

        [APIExclude]
        public NetworkConnection() { }
        [APIExclude]
        public NetworkConnection(NetworkManager manager, int clientId, bool asServer)
        {
            Initialize(manager, clientId, asServer);
        }

        public void Dispose()
        {
            foreach (PacketBundle p in _toClientBundles)
                p.Dispose();
            _toClientBundles.Clear();
        }

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Initialize(NetworkManager nm, int clientId, bool asServer)
        {
            NetworkManager = nm;
            ClientId = clientId;
            //Only the server uses the ping and buffer.
            if (asServer)
            {
                InitializeBuffer();
                InitializePing();
            }
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Reset()
        {
            _latestTick = 0;
            _serverLatestTick = 0;
            LastPacketTick = 0;
            ClientId = -1;
            ClearObjects();
            Authenticated = false;
            NetworkManager = null;
            _loadedStartScenesAsClient = false;
            _loadedStartScenesAsServer = false;
            SetDisconnecting(false);
            Scenes.Clear();
            ResetPingPong();
        }

        /// <summary>
        /// Sets Disconnecting boolean for this connection.
        /// </summary>
        internal void SetDisconnecting(bool value)
        {
            Disconnecting = value;
            if (Disconnecting)
                DisconnectingTick = NetworkManager.TimeManager.LocalTick;
        }

        /// <summary>
        /// Disconnects this connection.
        /// </summary>
        /// <param name="immediately">True to disconnect immediately. False to send any pending data first.</param>
        public void Disconnect(bool immediately)
        {
            if (Disconnecting)
            {
                NetworkManager.LogWarning($"ClientId {ClientId} is already disconnecting.");
                return;
            }

            SetDisconnecting(true);
            //If immediately then force disconnect through transport.
            if (immediately)
                NetworkManager.TransportManager.Transport.StopConnection(ClientId, true);
            //Otherwise mark dirty so server will push out any pending information, and then disconnect.
            else
                ServerDirty();
        }

        /// <summary>
        /// Returns if just loaded start scenes and sets them as loaded if not.
        /// </summary>
        /// <returns></returns>
        internal bool SetLoadedStartScenes(bool asServer)
        {
            bool loadedToCheck = (asServer) ? _loadedStartScenesAsServer : _loadedStartScenesAsClient;
            //Result becomes true if not yet loaded start scenes.
            bool result = !loadedToCheck;
            if (asServer)
                _loadedStartScenesAsServer = true;
            else
                _loadedStartScenesAsClient = true;

            OnLoadedStartScenes?.Invoke(this, asServer);

            return result;
        }

        /// <summary>
        /// Sets connection as authenticated.
        /// </summary>
        internal void ConnectionAuthenticated()
        {
            Authenticated = true;
        }

        /// <summary>
        /// Adds to Objects owned by this connection.
        /// </summary>
        /// <param name="nob"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddObject(NetworkObject nob)
        {
            _objects.Add(nob);
            //If adding the first object then set new FirstObject.
            if (_objects.Count == 1)
                FirstObject = nob;

            OnObjectAdded?.Invoke(nob);
        }

        /// <summary>
        /// Removes from Objects owned by this connection.
        /// </summary>
        /// <param name="nob"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveObject(NetworkObject nob)
        {
            _objects.Remove(nob);
            //If removing the first object then set a new one.
            if (nob == FirstObject)
                SetFirstObject();

            OnObjectRemoved?.Invoke(nob);
        }

        /// <summary>
        /// Clears all Objects.
        /// </summary>
        private void ClearObjects()
        {
            _objects.Clear();
            FirstObject = null;
        }

        /// <summary>
        /// Sets FirstObject using the first element in Objects.
        /// </summary>
        private void SetFirstObject()
        {
            if (_objects.Count == 0)
            {
                FirstObject = null;
            }
            else
            {
                foreach (NetworkObject nob in Objects)
                {
                    FirstObject = nob;
                    break;
                }
            }
        }

        /// <summary>
        /// Adds a scene to this connections Scenes.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        internal bool AddToScene(Scene scene)
        {
            return Scenes.Add(scene);
        }

        /// <summary>
        /// Removes a scene to this connections Scenes.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        internal bool RemoveFromScene(Scene scene)
        {
            return Scenes.Remove(scene);
        }

    }


}