using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Object.Synchronizing.Internal
{
    public class SyncBase : ISyncType
    {

        #region Public.
        /// <summary>
        /// True if a SyncObject, false if a SyncVar.
        /// </summary>
        public bool IsSyncObject { get; private set; } = false;
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
        public bool IsDirty { get; private set; } = false;
        /// <summary>
        /// TimeManager to handle ticks.
        /// </summary>
        public TimeManager _timeManager = null;
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
        #endregion

        #region Private.
        /// <summary>
        /// Sync interval converted to ticks.
        /// </summary>
        private uint _timeToTicks = 0;
        #endregion

        /// <summary>
        /// Initializes this SyncBase.
        /// </summary>
        /// <param name="nb"></param>
        /// <param name="syncIndex"></param>
        /// <param name="writePermissions"></param>
        /// <param name="readPermissions"></param>
        /// <param name="tickRate"></param>
        /// <param name="channel"></param>
        public void InitializeInstance(WritePermission writePermissions, ReadPermission readPermissions, float tickRate, Channel channel, bool isSyncObject)
        {
            Settings = new Settings()
            {
                WritePermission = writePermissions,
                ReadPermission = readPermissions,
                SendTickRate = tickRate,
                Channel = channel
            };

            IsSyncObject = isSyncObject;
        }

        /// <summary>
        /// Sets the SyncIndex.
        /// </summary>
        /// <param name="index"></param>
        public void SetSyncIndex(NetworkBehaviour nb, uint index)
        {
            NetworkBehaviour = nb;
            SyncIndex = index;
            NetworkBehaviour.RegisterSyncType(this, SyncIndex);
        }

        /// <summary>
        /// PreInitializes this for use with the network.
        /// </summary>
        public void PreInitialize(NetworkManager networkManager)
        {
            _timeManager = networkManager.TimeManager;
            _timeToTicks = _timeManager.TimeToTicks(Settings.SendTickRate);
        }

        /// <summary>
        /// Dirties this Sync and the NetworkBehaviour.
        /// </summary>
        public void Dirty()
        {
            if (IsDirty)
                return;
            
            if (NetworkBehaviour.DirtySyncType(IsSyncObject))
                IsDirty = true;
        }

        /// <summary>
        /// Sets IsDirty to false.
        /// </summary>
        internal void ResetDirty()
        {
            IsDirty = false;
        }

        internal bool WriteTimeMet(uint tick)
        {
            return (IsDirty && tick >= NextSyncTick);
        }
        /// <summary>
        /// Writes current value.
        /// </summary>
        /// <param name="writer"></param>
        ///<param name="resetSyncTick">True to set the next time data may sync.</param>
        public virtual void Write(PooledWriter writer, bool resetSyncTick = true)
        {
            if (resetSyncTick)
                NextSyncTick += _timeToTicks;

            //writer.WriteByte((byte)SyncIndex);
            writer.WriteUInt32(SyncIndex, AutoPackType.Unpacked);
        }
        /// <summary>
        /// Writes current value if not initialized value.
        /// </summary>
        /// <param name="writer"></param>
        public virtual void WriteIfChanged(PooledWriter writer) { }
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