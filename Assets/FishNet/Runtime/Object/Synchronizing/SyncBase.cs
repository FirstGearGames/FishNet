using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Serializing;
using FishNet.Transporting;
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
        public float SendTickRate => Settings.SendTickRate;
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
                SendTickRate = tickRate,
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
            _timeToTicks = NetworkManager.TimeManager.TimeToTicks(Settings.SendTickRate, TickRounding.RoundUp);
        }

        /// <summary>
        /// Called after OnStartXXXX has occurred.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        protected internal virtual void OnStartCallback(bool asServer) { }

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
        /// Sets current value.
        /// </summary>
        /// <param name="reader"></param>
        public virtual void Read(PooledReader reader) { }
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