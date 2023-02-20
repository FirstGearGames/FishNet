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
        /// Next time a SyncVar may send data/
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
        /// Called after OnStartXXXX has occurred.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        public virtual void OnStartCallback(bool asServer) { }

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
            bool result = (Settings.ReadPermission == ReadPermission.ExcludeOwner && NetworkBehaviour.IsOwner);
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
                NetworkManager.LogWarning($"Cannot complete operation as server when server is not active.");
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
        internal bool WriteTimeMet(uint tick)
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
                NextSyncTick = NetworkManager.TimeManager.Tick + _timeToTicks;

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
        [Obsolete("Use Read(PooledReader, bool).")]
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
        public virtual void Reset()
        {
            NextSyncTick = 0;
            ResetDirty();
        }
    }


}