using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object
{
    
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Types.
        /// <summary>
        /// Used to generate data sent from syncvars.
        /// </summary>
        private class SyncVarWriter
        {
            /// <summary>
            /// Clients which can be synchronized.
            /// </summary>
            public ReadPermission ReadPermission;
            /// <summary>
            /// Writers for each channel.
            /// </summary>
            public PooledWriter[] Writers { get; private set; } = null;

            public SyncVarWriter(ReadPermission readPermission, byte channelCount)
            {
                ReadPermission = readPermission;
                Writers = new PooledWriter[channelCount];
                for (int i = 0; i < Writers.Length; i++)
                    Writers[i] = WriterPool.GetWriter();
            }

            /// <summary>
            /// Resets Writers.
            /// </summary>
            public void Reset()
            {
                if (Writers == null)
                    return;

                for (int i = 0; i < Writers.Length; i++)
                    Writers[i].Reset();
            }
        }
        #endregion

        /// <summary>
        /// Writers for syncvars. A writer will exist for every ReadPermission type.
        /// </summary>
        private SyncVarWriter[] _syncTypeWriters = new SyncVarWriter[2];
        /// <summary>
        /// SyncVars within this NetworkBehaviour.
        /// </summary>
        private Dictionary<uint, SyncBase> _syncVars = new Dictionary<uint, SyncBase>();
        /// <summary>
        /// True if at least one syncVar is dirty.
        /// </summary>
        private bool _syncVarDirty = false;
        /// <summary>
        /// SyncVars within this NetworkBehaviour.
        /// </summary>
        private Dictionary<uint, SyncBase> _syncObjects = new Dictionary<uint, SyncBase>();
        /// <summary>
        /// True if at least one syncObject is dirty.
        /// </summary>
        private bool _syncObjectDirty = false;

        /// <summary>
        /// Registers a SyncType.
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="index"></param>
        internal void RegisterSyncType(SyncBase sb, uint index)
        {
            if (sb.IsSyncObject)
                _syncObjects.Add(index, sb);
            else
                _syncVars.Add(index, sb);
        }
        /// <summary>
        /// Sets a SyncVar as dirty.
        /// </summary>
        /// <param name="isSyncObject">True if dirtying a syncObject.</param>
        /// <returns>True if successfully dirtied syncType.</returns>
        internal bool DirtySyncType(bool isSyncObject)
        {
            //No reason to serialize data if no clients exist.
            if (NetworkObject.NetworkManager.ServerManager.Clients.Count == 0)
                return false;

            /* Already dirtied checks. I could skip this and use a hashset
             * on the NetworkManager.Server to avoid adding duplicates
             * but adding to and iterating a HashSet is slightly slower
             * than a list. When it comes to scaling a simple bool check
             * vs iterating slower collections will likely perform better. */
            if (isSyncObject)
            {
                if (_syncObjectDirty)
                    return false;
                _syncObjectDirty = true;
            }
            else
            {
                if (_syncVarDirty)
                    return false;
                _syncVarDirty = true;
            }

            NetworkObject.NetworkManager.ServerManager.Objects.SetDirtySyncType(this, isSyncObject);
            return true;
        }

        /// <summary>
        /// Prepares this script for initialization.
        /// </summary>
        /// <param name="networkObject"></param>
        /// <param name="componentIndex"></param>
        private void PreInitializeSyncTypes(NetworkObject networkObject)
        {
            //Build writers for observers and owner.
            byte channelCount = NetworkObject.NetworkManager.TransportManager.Transport.GetChannelCount();
            _syncTypeWriters[0] = new SyncVarWriter(ReadPermission.Observers, channelCount);
            _syncTypeWriters[1] = new SyncVarWriter(ReadPermission.OwnerOnly, channelCount);

            foreach (SyncBase sb in _syncVars.Values)
                sb.PreInitialize(networkObject.NetworkManager);
        }


        /// <summary>
        /// Reads a SyncVar.
        /// </summary>
        /// <param name="reader"></param>
        internal void OnSyncType(PooledReader reader, int length, bool isSyncObject)
        {
            int readerStart = reader.Position;
            SyncBase sb;
            while (reader.Position - readerStart < length)
            {
                //byte index = reader.ReadByte();
                uint index = reader.ReadUInt32(AutoPackType.Unpacked);
                if (isSyncObject)
                {
                    if (_syncObjects.TryGetValue(index, out sb))
                        sb.Read(reader);
                    else
                        Debug.LogError($"SyncObject not found for index {index} on {transform.name}.");
                }
                else
                {
                    if (_syncVars.ContainsKey(index))
                        ReadSyncVar(reader, index);
                    else
                        Debug.LogError($"SyncVar not found for index {index} on {transform.name}.");
                }
            }
        }

        /// <summary>
        /// Codegen overrides this method to read syncVars for each script which inherits NetworkBehaviour.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="index"></param>
        internal virtual void ReadSyncVar(PooledReader reader, uint index) { }

        /// <summary>
        /// Writers dirty SyncTypes if their write tick has been met.
        /// </summary>
        /// <returns>True if there are no pending dirty sync types.</returns>
        internal bool WriteDirtySyncTypes(bool isSyncObject)
        {
            /* If there is nothing dirty then return true, indicating no more
             * pending dirty checks. */
            if (isSyncObject && (!_syncObjectDirty || _syncObjects.Count == 0))
                return true;
            else if (!isSyncObject && (!_syncVarDirty || _syncVars.Count == 0))
                return true;

            /* True if writers have been reset for this check.
             * For perf writers are only reset when data is to be written. */
            bool writersReset = false;
            uint tick = NetworkObject.NetworkManager.TimeManager.Tick;

            //True if a syncvar is found to still be dirty.
            bool dirtyFound = false;
            //True if data has been written and is ready to send.
            bool dataWritten = false;
            Dictionary<uint, SyncBase> collection = (isSyncObject) ? _syncObjects : _syncVars;
            foreach (SyncBase sb in collection.Values)
            {
                if (!sb.IsDirty)
                    continue;

                dirtyFound = true;
                if (sb.WriteTimeMet(tick))
                {
                    //If writers still need to be reset.
                    if (!writersReset)
                    {
                        writersReset = true;
                        //Reset writers.
                        for (int i = 0; i < _syncTypeWriters.Length; i++)
                            _syncTypeWriters[i].Reset();
                    }

                    //Find channel.
                    byte channel = (byte)sb.Channel;
                    sb.ResetDirty();
                    //If ReadPermission is owner but no owner skip this syncvar write.
                    if (sb.Settings.ReadPermission == ReadPermission.OwnerOnly && !NetworkObject.OwnerIsValid)
                        continue;

                    dataWritten = true;
                    //Find PooledWriter to use.
                    PooledWriter writer = null;
                    for (int i = 0; i < _syncTypeWriters.Length; i++)
                    {
                        if (_syncTypeWriters[i].ReadPermission == sb.Settings.ReadPermission)
                        {
                            /* Channel for syncVar is beyond available channels in transport.
                             * Use default reliable. */
                            if (channel >= _syncTypeWriters[i].Writers.Length)
                                channel = NetworkObject.NetworkManager.TransportManager.Transport.GetDefaultReliableChannel();

                            writer = _syncTypeWriters[i].Writers[channel];
                            break;
                        }
                    }

                    int beforeWrite = writer.Length;
                    if (writer == null)
                        Debug.LogError($"Writer couldn't be found for permissions {sb.Settings.ReadPermission} on channel {channel}.");
                    else
                        sb.Write(writer);
                }
            }

            //If no dirty were found.
            if (!dirtyFound)
            {
                if (isSyncObject)
                    _syncObjectDirty = false;
                else
                    _syncVarDirty = false;
                return true;
            }
            //At least one sync var was dirty.
            else if (dataWritten)
            {
                for (int i = 0; i < _syncTypeWriters.Length; i++)
                {
                    for (byte channel = 0; channel < _syncTypeWriters[i].Writers.Length; channel++)
                    {
                        PooledWriter channelWriter = _syncTypeWriters[i].Writers[channel];
                        //If there is data to send.
                        if (channelWriter.Length > 0)
                        {
                            using (PooledWriter packetWriter = WriterPool.GetWriter())
                            {
                                PacketId packetId = (isSyncObject) ? PacketId.SyncObject : PacketId.SyncVar;
                                packetWriter.WriteByte((byte)packetId);
                                packetWriter.WriteNetworkBehaviour(this);
                                packetWriter.WriteBytesAndSize(channelWriter.GetBuffer(), 0, channelWriter.Length);

                                //If sending to observers.
                                if (_syncTypeWriters[i].ReadPermission == ReadPermission.Observers)
                                    NetworkObject.NetworkManager.TransportManager.SendToClients((byte)channel, packetWriter.GetArraySegment(), this);
                                //Sending only to owner.
                                else
                                    NetworkObject.NetworkManager.TransportManager.SendToClient(channel, packetWriter.GetArraySegment(), NetworkObject.Owner);
                            }
                        }
                    }
                }
            }

            /* Fall through. If here then sync types are still pending
             * being written or were just written this frame. */
            return false;
        }   


        public bool SyncTypeEquals<T>(T a, T b)
        {
            return EqualityComparer<T>.Default.Equals(a, b);
        }
        /// <summary>
        /// Resets all SyncVars for this NetworkBehaviour.
        /// </summary>
        internal void ResetSyncTypes()
        {
            foreach (SyncBase sb in _syncVars.Values)
                sb.Reset();
        }

        /// <summary>
        /// Writers syncVars for a spawn message.
        /// </summary>
        /// <param name="writer"></param>
        ///<param name="forOwner">True to also include syncVars which are for owner only.</param>
        internal void WriteSyncTypesForSpawn(PooledWriter writer, bool forOwner)
        {
            WriteSyncType(_syncVars);
            WriteSyncType(_syncObjects);

            void WriteSyncType(Dictionary<uint, SyncBase> collection)
            {
                using (PooledWriter syncTypeWriter = WriterPool.GetWriter())
                {
                    /* Since all values are being written everything is
                     * written in order so there's no reason to pass
                     * indexes. */
                    foreach (SyncBase sb in collection.Values)
                    {
                        //If not for owner and syncvar is owner only.
                        if (!forOwner && sb.Settings.ReadPermission == ReadPermission.OwnerOnly)
                        {
                            //If there is an owner then skip.
                            if (NetworkObject.OwnerIsValid)
                                continue;
                        }

                        sb.WriteIfChanged(syncTypeWriter);
                    }

                    writer.WriteBytesAndSize(syncTypeWriter.GetBuffer(), 0, syncTypeWriter.Length);
                }
            }
        }




    }


}

