using FishNet.Component.Observing;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FishNet.Managing.Timing.EstimatedTick;

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
        public bool LoadedStartScenes() => (_loadedStartScenesAsServer || _loadedStartScenesAsClient);
        /// <summary>
        /// 
        /// </summary>
        public bool LoadedStartScenes(bool asServer)
        {
            if (asServer)
                return _loadedStartScenesAsServer;
            else
                return _loadedStartScenesAsClient;
        }
        /// <summary>
        /// True if loaded start scenes as server.
        /// </summary>
        private bool _loadedStartScenesAsServer;
        /// <summary>
        /// True if loaded start scenes as client.
        /// </summary>
        private bool _loadedStartScenesAsClient;
        /// <summary>
        /// ObjectIds to use for predicted spawning.
        /// </summary>
        internal Queue<int> PredictedObjectIds = new Queue<int>();
        /// <summary>
        /// TransportIndex this connection is on.
        /// For security reasons this value will be unset on clients if this is not their connection.
        /// </summary>
        public int TransportIndex { get; internal set; } = -1;
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
        /// Objects owned by this connection. Available to this connection and server.
        /// </summary>
        public HashSet<NetworkObject> Objects = new HashSet<NetworkObject>();
        /// <summary>
        /// The first object within Objects.
        /// </summary>
        public NetworkObject FirstObject { get; private set; }
        /// <summary>
        /// Sets a custom FirstObject. This connection must be owner of the specified object.
        /// </summary>
        /// <param name="nob"></param>
        public void SetFirstObject(NetworkObject nob)
        {
            //Invalid object.
            if (!Objects.Contains(nob))
            {
                string errMessage = $"FirstObject for {ClientId} cannot be set to {nob.name} as it's not within Objects for this connection.";
                if (NetworkManager == null)
                    NetworkManager.StaticLogError(errMessage);
                else
                    NetworkManager.LogError(errMessage);

                return;
            }

            FirstObject = nob;
        }
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
        /// LocalTick of the server when this connection was established. This value is not set for clients.
        /// </summary>
        internal uint ServerConnectionTick;
        /// <summary>
        /// Tick of the last packet received from this connection which was not out of order.
        /// This value is only available on the server.
        /// </summary>
        public EstimatedTick PacketTick;
        [Obsolete("Use LocalTick instead.")] //Remove on 2023/06/01
        public uint Tick => LocalTick.Value(NetworkManager.TimeManager);
        /// <summary>
        /// Approximate local tick as it is on this connection.
        /// This also contains the last set value for local and remote.
        /// </summary>
        public EstimatedTick LocalTick;
        #endregion

        #region Const.
        /// <summary>
        /// Value used when ClientId has not been set.
        /// </summary>
        public const int UNSET_CLIENTID_VALUE = -1;
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
            if (this.ClientId == NetworkConnection.UNSET_CLIENTID_VALUE || nc.ClientId == NetworkConnection.UNSET_CLIENTID_VALUE)
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
        public NetworkConnection(NetworkManager manager, int clientId, int transportIndex, bool asServer)
        {
            Initialize(manager, clientId, transportIndex, asServer);
        }

        internal void Dispose()
        {
            Deinitialize();
        }

        /// <summary>
        /// Outputs data about this connection as a string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            int clientId = ClientId;
            string ip = (NetworkManager != null) ? NetworkManager.TransportManager.Transport.GetConnectionAddress(clientId) : "Unset";
            return $"Id [{ClientId}] Address [{ip}]";
        }

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Initialize(NetworkManager nm, int clientId, int transportIndex, bool asServer)
        {
            NetworkManager = nm;
            if (asServer)
                ServerConnectionTick = nm.TimeManager.LocalTick;
            TransportIndex = transportIndex;
            ClientId = clientId;
            /* Set PacketTick to current values so
            * that timeouts and other things around
           * first packet do not occur due to an unset value. */
            PacketTick.Update(nm.TimeManager, 0, OldTickOption.SetLastRemoteTick);
            Observers_Initialize(nm);
            Prediction_Initialize(nm, asServer);
            //Only the server uses the ping and buffer.
            if (asServer)
            {
                InitializeBuffer();
                InitializePing();
            }
        }

        /// <summary>
        /// Deinitializes this NetworkConnection. This is called prior to resetting.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Deinitialize()
        {
            MatchCondition.RemoveFromMatchesWithoutRebuild(this, NetworkManager);

            foreach (PacketBundle p in _toClientBundles)
                p.Dispose();
            _toClientBundles.Clear();

            ServerConnectionTick = 0;
            PacketTick.Reset();
            TransportIndex = -1;
            ClientId = -1;
            ClearObjects();
            Authenticated = false;
            NetworkManager = null;
            _loadedStartScenesAsClient = false;
            _loadedStartScenesAsServer = false;
            SetDisconnecting(false);
            Scenes.Clear();
            PredictedObjectIds.Clear();
            ResetPingPong();
            ResetStates_Lod();
            AllowedForcedLodUpdates = 0;
            LastLevelOfDetailUpdate = 0;
            LevelOfDetailInfractions = 0;
            Observers_Reset();
            Prediction_Reset();
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
            if (!IsValid)
            {
                //NetworkManager is likely null if invalid.
                NetworkManager.StaticLogWarning($"Disconnect called on an invalid connection.");
                return;
            }
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
            if (!IsValid)
                return;

            Objects.Add(nob);
            //If adding the first object then set new FirstObject.
            if (Objects.Count == 1)
                SetFirstObject();

            OnObjectAdded?.Invoke(nob);
        }

        /// <summary>
        /// Removes from Objects owned by this connection.
        /// </summary>
        /// <param name="nob"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveObject(NetworkObject nob)
        {
            if (!IsValid)
            {
                ClearObjects();
                return;
            }

            Objects.Remove(nob);
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
            Objects.Clear();
            FirstObject = null;
        }

        /// <summary>
        /// Sets FirstObject using the first element in Objects.
        /// </summary>
        private void SetFirstObject()
        {
            if (Objects.Count == 0)
            {
                FirstObject = null;
            }
            else
            {
                foreach (NetworkObject nob in Objects)
                {
                    FirstObject = nob;
                    Observers_FirstObjectChanged();
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