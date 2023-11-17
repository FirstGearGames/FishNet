#if FISHNET_RELEASE_MODE
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Synchronizing.Internal
{
    public class SyncBase : ISyncType
    {

        #region Public.
        /// <summary>
        /// True if this SyncBase has been registered within it's containing class.
        /// </summary>
        public bool IsRegistered { get; private set; }
        /// <summary>
        /// True if the object for which this SyncType is for has been initialized for the network.
        /// </summary>
        public bool IsNetworkInitialized => (IsRegistered && (NetworkBehaviour.IsServer || NetworkBehaviour.IsClient));
        /// <summary>
        /// True if a SyncObject, false if a SyncVar.
        /// </summary>
        public bool IsSyncObject { get; private set; }
        /// <summary>
        /// The settings for this SyncVar.
        /// </summary>
        public Settings Settings = new Settings();
        /// <summary>
        /// How often updates may send.
        /// </summary>
        public float SendRate => Settings.SendRate;
        /// <summary>
        /// True if this SyncVar needs to send data.
        /// </summary>
        public bool IsDirty { get; private set; }
        /// <summary>
        /// NetworkManager this uses.
        /// </summary>
        public NetworkManager NetworkManager = null;
        /// <summary>
        /// NetworkBehaviour this SyncVar belongs to.
        /// </summary>
        public NetworkBehaviour NetworkBehaviour = null;
        /// <summary>
        /// True if the server side has initialized this SyncType.
        /// </summary>
        public bool OnStartServerCalled { get; private set; }
        /// <summary>
        /// True if the client side has initialized this SyncType.
        /// </summary>
        public bool OnStartClientCalled { get; private set; }
        /// <summary>
        /// Next time this SyncType may send data.
        /// This is also the next time a client may send to the server when using client-authoritative SyncTypes.
        /// </summary>
        public uint NextSyncTick = 0;
        /// <summary>
        /// Index within the sync collection.
        /// </summary>
        public uint SyncIndex { get; protected set; } = 0;
        /// <summary>
        /// Channel to send on.
        /// </summary>
        internal Channel Channel => _currentChannel;
        #endregion

        #region Private.
        /// <summary>
        /// Sync interval converted to ticks.
        /// </summary>
        private uint _timeToTicks;
        /// <summary>
        /// Channel to use for next write. To ensure eventual consistency this eventually changes to reliable when Settings are unreliable.
        /// </summary>
        private Channel _currentChannel;
        #endregion

        /// <summary>
        /// Initializes this SyncBase.
        /// </summary>
        public void InitializeInstance(NetworkBehaviour nb, uint syncIndex, WritePermission writePermissions, ReadPermission readPermissions, float tickRate, Channel channel, bool isSyncObject)
        {
            NetworkBehaviour = nb;
            SyncIndex = syncIndex;
            _currentChannel = channel;
            IsSyncObject = isSyncObject;
            Settings = new Settings()
            {
                WritePermission = writePermissions,
                ReadPermission = readPermissions,
                SendRate = tickRate,
                Channel = channel
            };

            NetworkBehaviour.RegisterSyncType(this, SyncIndex);
        }

        /// <summary>
        /// Sets the SyncIndex.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRegistered()
        {
            Registered();
        }

        /// <summary>
        /// Called when the SyncType has been registered, but not yet initialized over the network.
        /// </summary>
        protected virtual void Registered()
        {
            IsRegistered = true;
        }

        /// <summary>
        /// PreInitializes this for use with the network.
        /// </summary>
        public void PreInitialize(NetworkManager networkManager)
        {
            NetworkManager = networkManager;
            if (Settings.SendRate < 0f)
                Settings.SendRate = networkManager.ServerManager.GetSynctypeRate();

            _timeToTicks = NetworkManager.TimeManager.TimeToTicks(Settings.SendRate, TickRounding.RoundUp);
        }

        /// <summary>
        /// Called after OnStartXXXX has occurred for the NetworkBehaviour.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        public virtual void OnStartCallback(bool asServer)
        {
            if (asServer)
                OnStartServerCalled = true;
            else
                OnStartClientCalled = true;
        }
        /// <summary>
        /// Called before OnStopXXXX has occurred for the NetworkBehaviour.
        /// </summary>
        /// <param name="asServer">True if OnStopServer was called, false if OnStopClient.</param>
        public virtual void OnStopCallback(bool asServer)
        {
            if (asServer)
                OnStartServerCalled = false;
            else
                OnStartClientCalled = false;
        }

        protected bool CanNetworkSetValues(bool warn = true)
        {
            /* If not registered then values can be set
             * since at this point the object is still being initialized
             * in awake so we want those values to be applied. */
            if (!IsRegistered)
                return true;
            /* If the network is not initialized yet then let
             * values be set. Values set here will not synchronize
             * to the network. We are assuming the user is setting
             * these values on client and server appropriately
             * since they are being applied prior to this object
             * being networked. */
            if (!IsNetworkInitialized)
                return true;
            //If server is active then values can be set no matter what.
            if (NetworkBehaviour.IsServer)
                return true;
            //Predicted spawning is enabled.
            if (NetworkManager != null && NetworkManager.PredictionManager.GetAllowPredictedSpawning() && NetworkBehaviour.NetworkObject.AllowPredictedSpawning)
                return true;
            /* If here then server is not active and additional
             * checks must be performed. */
            bool result = (Settings.WritePermission == WritePermission.ClientUnsynchronized) || (Settings.ReadPermission == ReadPermission.ExcludeOwner && NetworkBehaviour.IsOwner);
            if (!result && warn)
                LogServerNotActiveWarning();

            return result;
        }

        /// <summary>
        /// Logs that the operation could not be completed because the server is not active.
        /// </summary>
        protected void LogServerNotActiveWarning()
        {
            if (NetworkManager != null)
                NetworkManager.LogWarning($"Cannot complete operation as server when server is not active. You can disable this warning by setting WritePermissions to {WritePermission.ClientUnsynchronized.ToString()}.");
        }

        /// <summary>
        /// Dirties this Sync and the NetworkBehaviour.
        /// </summary>
        public bool Dirty()
        {
            /* Reset channel even if already dirty.
             * This is because the value might have changed
             * which will reset the eventual consistency state. */
            _currentChannel = Settings.Channel;

            /* Once dirty don't undirty until it's
             * processed. This ensures that data
             * is flushed. */
            bool canDirty = NetworkBehaviour.DirtySyncType(IsSyncObject);
            IsDirty |= canDirty;

            return canDirty;
        }

        /// <summary>
        /// Sets IsDirty to false.
        /// </summary>
        internal void ResetDirty()
        {
            //If not a sync object and using unreliable channel.
            if (!IsSyncObject && Settings.Channel == Channel.Unreliable)
            {
                //Check if dirty can be unset or if another tick must be run using reliable.
                if (_currentChannel == Channel.Unreliable)
                    _currentChannel = Channel.Reliable;
                //Already sent reliable, can undirty. Channel will reset next time this dirties.
                else
                    IsDirty = false;
            }
            //If syncObject or using reliable unset dirty.
            else
            {
                IsDirty = false;
            }
        }
        /// <summary>
        /// True if dirty and enough time has passed to write changes.
        /// </summary>
        /// <param name="tick"></param>
        /// <returns></returns>
        internal bool SyncTimeMet(uint tick)
        {
            return (IsDirty && tick >= NextSyncTick);
        }
        /// <summary>
        /// Writes current value.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="resetSyncTick">True to set the next time data may sync.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            WriteHeader(writer, resetSyncTick);
        }
        /// <summary>
        /// Writers the header for this SyncType.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="resetSyncTick"></param>
        protected virtual void WriteHeader(PooledWriter writer, bool resetSyncTick = true)
        {
            if (resetSyncTick)
                NextSyncTick = NetworkManager.TimeManager.LocalTick + _timeToTicks;

            writer.WriteByte((byte)SyncIndex);
        }
        /// <summary>
        /// Writes current value if not initialized value.
        /// </summary>
        /// <param name="writer"></param>
        public virtual void WriteFull(PooledWriter writer) { }
        /// <summary>
        /// Sets current value as client.
        /// </summary>
        /// <param name="reader"></param>
        [Obsolete("Use Read(PooledReader, bool).")] //Remove on 2023/06/01
        public virtual void Read(PooledReader reader) { }
        /// <summary>
        /// Sets current value as server or client.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="asServer"></param>
        public virtual void Read(PooledReader reader, bool asServer) { }
        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        [Obsolete("Use ResetState().")]
        public virtual void Reset() { }
        /// <summary>
        /// Resets initialized values.
        /// </summary>
        public virtual void ResetState()
        {
            NextSyncTick = 0;
            ResetDirty();
        }
    }


}
#else

using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Synchronizing.Internal
{
    public class SyncBase : ISyncType
    {

        #region Public.
        /// <summary>
        /// True if this SyncBase has been registered within it's containing class.
        /// </summary>
        public bool IsRegistered { get; private set; }
        /// <summary>
        /// True if the object for which this SyncType is for has been initialized for the network.
        /// </summary>
        public bool IsNetworkInitialized => (IsRegistered && (NetworkBehaviour.IsServer || NetworkBehaviour.IsClient));
        /// <summary>
        /// True if a SyncObject, false if a SyncVar.
        /// </summary>
        public bool IsSyncObject { get; private set; }
        /// <summary>
        /// The settings for this SyncVar.
        /// </summary>
        public Settings Settings = new Settings();
        /// <summary>
        /// How often updates may send.
        /// </summary>
        public float SendRate => Settings.SendRate;
        /// <summary>
        /// True if this SyncVar needs to send data.
        /// </summary>
        public bool IsDirty { get; private set; }
        /// <summary>
        /// NetworkManager this uses.
        /// </summary>
        public NetworkManager NetworkManager = null;
        /// <summary>
        /// NetworkBehaviour this SyncVar belongs to.
        /// </summary>
        public NetworkBehaviour NetworkBehaviour = null;
        /// <summary>
        /// True if the server side has initialized this SyncType.
        /// </summary>
        public bool OnStartServerCalled { get; private set; }
        /// <summary>
        /// True if the client side has initialized this SyncType.
        /// </summary>
        public bool OnStartClientCalled { get; private set; }
        /// <summary>
        /// Next time this SyncType may send data.
        /// This is also the next time a client may send to the server when using client-authoritative SyncTypes.
        /// </summary>
        public uint NextSyncTick = 0;
        /// <summary>
        /// Index within the sync collection.
        /// </summary>
        public uint SyncIndex { get; protected set; } = 0;
        /// <summary>
        /// Channel to send on.
        /// </summary>
        internal Channel Channel => _currentChannel;
        #endregion

        #region Private.
        /// <summary>
        /// Sync interval converted to ticks.
        /// </summary>
        private uint _timeToTicks;
        /// <summary>
        /// Channel to use for next write. To ensure eventual consistency this eventually changes to reliable when Settings are unreliable.
        /// </summary>
        private Channel _currentChannel;
        /// <summary>
        /// Last localTick when full data was written.
        /// </summary>
        protected uint _lastWriteFullLocalTick;
        /// <summary>
        /// Id of the current change since the last full write.
        /// This is used to prevent duplicates caused by deltas writing after full writes when clients already received the delta in the full write, such as a spawn message.
        /// </summary>
        protected uint _changeId;
        /// <summary>
        /// Last changeId read.
        /// </summary>
        private long _lastReadDirtyId = DEFAULT_LAST_READ_DIRTYID;
        #endregion

        #region Const.
        /// <summary>
        /// Default value for LastReadDirtyId.
        /// </summary>
        private const long DEFAULT_LAST_READ_DIRTYID = -1;
        #endregion

        /// <summary>
        /// Initializes this SyncBase.
        /// </summary>
        public void InitializeInstance(NetworkBehaviour nb, uint syncIndex, WritePermission writePermissions, ReadPermission readPermissions, float tickRate, Channel channel, bool isSyncObject)
        {
            NetworkBehaviour = nb;
            SyncIndex = syncIndex;
            _currentChannel = channel;
            IsSyncObject = isSyncObject;
            Settings = new Settings()
            {
                WritePermission = writePermissions,
                ReadPermission = readPermissions,
                SendRate = tickRate,
                Channel = channel
            };

            NetworkBehaviour.RegisterSyncType(this, SyncIndex);
        }

        /// <summary>
        /// Sets the SyncIndex.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRegistered()
        {
            Registered();
        }

        /// <summary>
        /// Called when the SyncType has been registered, but not yet initialized over the network.
        /// </summary>
        protected virtual void Registered()
        {
            IsRegistered = true;
        }

        /// <summary>
        /// PreInitializes this for use with the network.
        /// </summary>
        public void PreInitialize(NetworkManager networkManager)
        {
            NetworkManager = networkManager;
            if (Settings.SendRate < 0f)
                Settings.SendRate = networkManager.ServerManager.GetSynctypeRate();

            _timeToTicks = NetworkManager.TimeManager.TimeToTicks(Settings.SendRate, TickRounding.RoundUp);
        }

        /// <summary>
        /// Called after OnStartXXXX has occurred for the NetworkBehaviour.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        public virtual void OnStartCallback(bool asServer)
        {
            if (asServer)
                OnStartServerCalled = true;
            else
                OnStartClientCalled = true;
        }
        /// <summary>
        /// Called before OnStopXXXX has occurred for the NetworkBehaviour.
        /// </summary>
        /// <param name="asServer">True if OnStopServer was called, false if OnStopClient.</param>
        public virtual void OnStopCallback(bool asServer)
        {
            if (asServer)
                OnStartServerCalled = false;
            else
                OnStartClientCalled = false;
        }

        protected bool CanNetworkSetValues(bool warn = true)
        {
            /* If not registered then values can be set
             * since at this point the object is still being initialized
             * in awake so we want those values to be applied. */
            if (!IsRegistered)
                return true;
            /* If the network is not initialized yet then let
             * values be set. Values set here will not synchronize
             * to the network. We are assuming the user is setting
             * these values on client and server appropriately
             * since they are being applied prior to this object
             * being networked. */
            if (!IsNetworkInitialized)
                return true;
            //If server is active then values can be set no matter what.
            if (NetworkBehaviour.IsServer)
                return true;
            //Predicted spawning is enabled.
            if (NetworkManager != null && NetworkManager.PredictionManager.GetAllowPredictedSpawning() && NetworkBehaviour.NetworkObject.AllowPredictedSpawning)
                return true;
            /* If here then server is not active and additional
             * checks must be performed. */
            bool result = (Settings.WritePermission == WritePermission.ClientUnsynchronized) || (Settings.ReadPermission == ReadPermission.ExcludeOwner && NetworkBehaviour.IsOwner);
            if (!result && warn)
                LogServerNotActiveWarning();

            return result;
        }

        /// <summary>
        /// Logs that the operation could not be completed because the server is not active.
        /// </summary>
        protected void LogServerNotActiveWarning()
        {
            if (NetworkManager != null)
                NetworkManager.LogWarning($"Cannot complete operation as server when server is not active. You can disable this warning by setting WritePermissions to {WritePermission.ClientUnsynchronized.ToString()}.");
        }

        /// <summary>
        /// Dirties this Sync and the NetworkBehaviour.
        /// </summary>
        public bool Dirty()
        {
            /* Reset channel even if already dirty.
             * This is because the value might have changed
             * which will reset the eventual consistency state. */
            _currentChannel = Settings.Channel;

            /* Once dirty don't undirty until it's
             * processed. This ensures that data
             * is flushed. */
            bool canDirty = NetworkBehaviour.DirtySyncType(IsSyncObject);
            //If first time dirtying increase dirtyId.
            if (IsDirty != canDirty)
                _changeId++;
            IsDirty |= canDirty;

            return canDirty;
        }


        /// <summary>
        /// Reads the change Id and returns if changes should be ignored.
        /// </summary>
        /// <returns></returns>
        protected bool ReadChangeId(PooledReader reader)
        {
            bool reset = reader.ReadBoolean();

            uint id = reader.ReadUInt32();
            bool ignoreResults = !reset && (id <= _lastReadDirtyId);
            _lastReadDirtyId = id;
            return ignoreResults;
        }

        /// <summary>
        /// Writers the current ChangeId, and if it has been reset.
        /// </summary>
        protected void WriteChangeId(PooledWriter writer, bool fullWrite)
        {
            /* Fullwrites do not reset the Id, only
             * delta changes do. */
            bool resetId = (!fullWrite && NetworkManager.TimeManager.LocalTick > _lastWriteFullLocalTick);
            writer.WriteBoolean(resetId);
            //If to reset Id then do so.
            if (resetId)
                _changeId = 0;
            //Write Id.
            writer.WriteUInt32(_changeId);
        }

        /// <summary>
        /// Sets IsDirty to false.
        /// </summary>
        internal void ResetDirty()
        {
            //If not a sync object and using unreliable channel.
            if (!IsSyncObject && Settings.Channel == Channel.Unreliable)
            {
                //Check if dirty can be unset or if another tick must be run using reliable.
                if (_currentChannel == Channel.Unreliable)
                    _currentChannel = Channel.Reliable;
                //Already sent reliable, can undirty. Channel will reset next time this dirties.
                else
                    IsDirty = false;
            }
            //If syncObject or using reliable unset dirty.
            else
            {
                IsDirty = false;
            }
        }
        /// <summary>
        /// True if dirty and enough time has passed to write changes.
        /// </summary>
        /// <param name="tick"></param>
        /// <returns></returns>
        internal bool SyncTimeMet(uint tick)
        {
            return (IsDirty && tick >= NextSyncTick);
        }
        /// <summary>
        /// Writes current value.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="resetSyncTick">True to set the next time data may sync.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            WriteHeader(writer, resetSyncTick);
        }
        /// <summary>
        /// Writers the header for this SyncType.
        /// </summary>
        protected virtual void WriteHeader(PooledWriter writer, bool resetSyncTick = true)
        {
            if (resetSyncTick)
                NextSyncTick = NetworkManager.TimeManager.LocalTick + _timeToTicks;

            writer.WriteByte((byte)SyncIndex);
        }

        /// <summary>
        /// Indicates that a full write has occurred.
        /// This is called from WriteFull, or can be called manually.
        /// </summary>
        protected void FullWritten()
        {
            _lastWriteFullLocalTick = NetworkManager.TimeManager.LocalTick;
        }
        /// <summary>
        /// Writes all values for the SyncType.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void WriteFull(PooledWriter writer) 
        {
            FullWritten();
        }
        /// <summary>
        /// Sets current value as client.
        /// </summary>
        /// <param name="reader"></param>
        [Obsolete("Use Read(PooledReader, bool).")] //Remove on 2023/06/01
        public virtual void Read(PooledReader reader) { }
        /// <summary>
        /// Sets current value as server or client.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="asServer"></param>
        public virtual void Read(PooledReader reader, bool asServer) { }
        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        [Obsolete("Use ResetState().")]
        public virtual void Reset() { }
        /// <summary>
        /// Resets initialized values.
        /// </summary>
        public virtual void ResetState()
        {
            _lastWriteFullLocalTick = 0;
            _changeId = 0;
            _lastReadDirtyId = DEFAULT_LAST_READ_DIRTYID;
            NextSyncTick = 0;
            ResetDirty();
        }
    }


}

#endif