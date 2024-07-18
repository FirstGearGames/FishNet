using FishNet.CodeGenerating;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Synchronizing.Internal
{
    public class SyncBase
    {
        #region Public.
        /// <summary>
        /// True if this SyncBase has been initialized on its NetworkBehaviour.
        /// Being true does not mean that the NetworkBehaviour has been initialized on the network, but rather that this SyncBase has been configured with the basics to be networked.
        /// </summary>
        public bool IsInitialized { get; private set; }
        /// <summary>
        /// True if the object for which this SyncType is for has been initialized for the network.
        /// </summary>
        public bool IsNetworkInitialized => (IsInitialized && (NetworkBehaviour.IsServerStarted || NetworkBehaviour.IsClientStarted));
        /// <summary>
        /// True if a SyncObject, false if a SyncVar.
        /// </summary>
        public bool IsSyncObject { get; private set; }
        /// <summary>
        /// The settings for this SyncVar.
        /// </summary>
        [MakePublic]
        internal SyncTypeSettings Settings;
        /// <summary>
        /// How often updates may send.
        /// </summary>
        [MakePublic]
        internal float SendRate => Settings.SendRate;
        /// <summary>
        /// True if this SyncVar needs to send data.
        /// </summary>        
        public bool IsDirty { get; private set; }
        /// <summary>
        /// NetworkManager this uses.
        /// </summary>
        [MakePublic]
        internal NetworkManager NetworkManager = null;
        /// <summary>
        /// NetworkBehaviour this SyncVar belongs to.
        /// </summary>
        [MakePublic]
        internal NetworkBehaviour NetworkBehaviour = null;
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
        [MakePublic]
        internal uint NextSyncTick = 0;
        /// <summary>
        /// Index within the sync collection.
        /// </summary>
        public uint SyncIndex { get; protected set; } = 0;
        /// <summary>
        /// Channel to send on.
        /// </summary>
        internal Channel Channel => _currentChannel;
        /// <summary>
        /// Sets a new currentChannel.
        /// </summary>
        /// <param name="channel"></param>
        internal void SetCurrentChannel(Channel channel) => _currentChannel = channel;
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


        #region Constructors
        public SyncBase() : this(new SyncTypeSettings()) { }
        public SyncBase(SyncTypeSettings settings)
        {
            Settings = settings;
        }
        #endregion

        /// <summary>
        /// Updates settings with new values.
        /// </summary>
        public void UpdateSettings(SyncTypeSettings settings)
        {
            Settings = settings;
            SetTimeToTicks();
        }
        /// <summary>
        /// Updates settings with new values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePermissions(WritePermission writePermissions, ReadPermission readPermissions)
        {
            UpdatePermissions(writePermissions);
            UpdatePermissions(readPermissions);
        }
        /// <summary>
        /// Updates settings with new values.
        /// </summary>
        public void UpdatePermissions(WritePermission writePermissions) => Settings.WritePermission = writePermissions;
        /// <summary>
        /// Updates settings with new values.
        /// </summary>
        public void UpdatePermissions(ReadPermission readPermissions) => Settings.ReadPermission = readPermissions;
        /// <summary>
        /// Updates settings with new values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateSendRate(float sendRate)
        {
            Settings.SendRate = sendRate;
            SetTimeToTicks();
        }
        /// <summary>
        /// Updates settings with new values.
        /// </summary>
        public void UpdateSettings(Channel channel)
        {
            CheckChannel(ref channel);
            _currentChannel = channel;
        }
        /// <summary>
        /// Updates settings with new values.
        /// </summary>
        public void UpdateSettings(WritePermission writePermissions, ReadPermission readPermissions, float sendRate, Channel channel)
        {
            CheckChannel(ref channel);
            _currentChannel = channel;
            Settings = new SyncTypeSettings(writePermissions, readPermissions, sendRate, channel);
            SetTimeToTicks();
        }

        /// <summary>
        /// Checks channel and corrects if not valid.
        /// </summary>
        /// <param name="c"></param>
        private void CheckChannel(ref Channel c)
        {
            if (c == Channel.Unreliable && IsSyncObject)
            {
                c = Channel.Reliable;
                string warning = $"Channel cannot be unreliable for SyncObjects. Channel has been changed to reliable.";
                NetworkManager.LogWarning(warning);
            }
        }

        /// <summary>
        /// Initializes this SyncBase before user Awake code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [MakePublic]
        internal void InitializeEarly(NetworkBehaviour nb, uint syncIndex, bool isSyncObject)
        {
            NetworkBehaviour = nb;
            SyncIndex = syncIndex;
            IsSyncObject = isSyncObject;

            NetworkBehaviour.RegisterSyncType(this, SyncIndex);
        }

        /// <summary>
        /// Called during InitializeLate in NetworkBehaviours to indicate user Awake code has executed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [MakePublic]
        internal void InitializeLate()
        {
            Initialized();
        }

        /// <summary>
        /// Called when the SyncType has been registered, but not yet initialized over the network.
        /// </summary>
        protected virtual void Initialized()
        {
            IsInitialized = true;
        }

        /// <summary>
        /// PreInitializes this for use with the network.
        /// </summary>
        [MakePublic]
        internal protected void PreInitialize(NetworkManager networkManager)
        {
            NetworkManager = networkManager;

            if (Settings.IsDefault())
            {
                float sendRate = Mathf.Max(networkManager.ServerManager.GetSyncTypeRate(), (float)networkManager.TimeManager.TickDelta);
                Settings = new SyncTypeSettings(sendRate);
            }

            SetTimeToTicks();
        }

        /// <summary>
        /// Sets ticks needed to pass for send rate.
        /// </summary>
        private void SetTimeToTicks()
        {
            if (NetworkManager == null)
                return;
            _timeToTicks = NetworkManager.TimeManager.TimeToTicks(Settings.SendRate, TickRounding.RoundUp);
        }

        /// <summary>
        /// Called after OnStartXXXX has occurred for the NetworkBehaviour.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        [MakePublic]
        internal protected virtual void OnStartCallback(bool asServer)
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
        [MakePublic]
        internal protected virtual void OnStopCallback(bool asServer)
        {
            if (asServer)
                OnStartServerCalled = false;
            else
                OnStartClientCalled = false;
        }

        /// <summary>
        /// True if can set values and send them over the network.
        /// </summary>
        /// <param name="warn"></param>
        /// <returns></returns>
        protected bool CanNetworkSetValues(bool warn = true)
        {
            /* If not registered then values can be set
             * since at this point the object is still being initialized
             * in awake so we want those values to be applied. */
            if (!IsInitialized)
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
            if (NetworkBehaviour.IsServerStarted)
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
        /// <param name="sendRpc">True to send current dirtied values immediately as a RPC. When this occurs values will arrive in the order they are sent and interval is ignored.</param>
        protected bool Dirty()//bool sendRpc = false)
        {
            //if (sendRpc)
            //    NextSyncTick = 0;
            /* Reset channel even if already dirty.
             * This is because the value might have changed
             * which will reset the eventual consistency state. */
            _currentChannel = Settings.Channel;

            /* Once dirty don't undirty until it's
             * processed. This ensures that data
             * is flushed. */
            bool canDirty = NetworkBehaviour.DirtySyncType();
            //If first time dirtying increase dirtyId.
            if (IsDirty != canDirty)
                _changeId++;
            IsDirty |= canDirty;

            return canDirty;
        }

        /// <summary>
        /// Returns if callbacks can be invoked with asServer ture.
        /// This is typically used when the value is changing through user code, causing supplier to be unknown.
        /// </summary>
        /// <returns></returns>
        protected bool CanInvokeCallbackAsServer() => (!IsNetworkInitialized || NetworkBehaviour.IsServerStarted);

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
        /// <param name="resetSyncTick">True to set the next time data may sync.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [MakePublic]
        internal protected virtual void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
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

            writer.WriteUInt8Unpacked((byte)SyncIndex);
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
        [MakePublic]
        internal protected virtual void WriteFull(PooledWriter writer)
        {
            FullWritten();
        }
        /// <summary>
        /// Sets current value as server or client through deserialization.
        /// </summary>
        [MakePublic]
        internal protected virtual void Read(PooledReader reader, bool asServer) { }
        /// <summary>
        /// Resets initialized values for server and client.
        /// </summary>
        internal protected virtual void ResetState()
        {
            ResetState(true);
            ResetState(false);
        }

        /// <summary>
        /// Resets initialized values for server or client.
        /// </summary>
        [MakePublic]
        internal protected virtual void ResetState(bool asServer)
        {
            if (asServer)
            {
                _lastWriteFullLocalTick = 0;
                _changeId = 0;
                NextSyncTick = 0;
                SetCurrentChannel(Settings.Channel);
                IsDirty = false;
            }

            /* This only needs to be reset for clients, since
             * it only applies to clients. But if the server is resetting
             * that means the object is deinitializing, and won't have any
             * client observers anyway. Because of this it's safe to reset
             * with asServer true, or false.
             * 
             * This change is made to resolve a bug where asServer:false
             * sometimes does not invoke when stopping clientHost while not
             * also stopping play mode. */
            _lastReadDirtyId = DEFAULT_LAST_READ_DIRTYID;
        }


    }
}