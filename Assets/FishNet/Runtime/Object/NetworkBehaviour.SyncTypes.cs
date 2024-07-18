using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Object.Delegating;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Types.
        /// <summary>
        /// Used to generate data sent from synctypes.
        /// </summary>
        private class SyncTypeWriter
        {
            /// <summary>
            /// Clients which can be synchronized.
            /// </summary>
            public ReadPermission ReadPermission;
            /// <summary>
            /// Writers for each channel.
            /// </summary>
            public PooledWriter[] Writers { get; private set; }

            public SyncTypeWriter(ReadPermission readPermission)
            {
                ReadPermission = readPermission;
                Writers = new PooledWriter[TransportManager.CHANNEL_COUNT];
                for (int i = 0; i < Writers.Length; i++)
                    Writers[i] = WriterPool.Retrieve();
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

        #region Private.
        /// <summary>
        /// Writers for syncTypes. A writer will exist for every ReadPermission type.
        /// </summary>
        private SyncTypeWriter[] _syncTypeWriters;
        /// <summary>
        /// SyncTypes within this NetworkBehaviour.
        /// </summary>
        private Dictionary<uint, SyncBase> _syncTypes = new Dictionary<uint, SyncBase>();
        /// <summary>
        /// True if at least one syncType is dirty.
        /// </summary>
        internal bool SyncTypeDirty;
        /// <summary>
        /// All ReadPermission values.
        /// </summary>
        private static ReadPermission[] _readPermissions;
        #endregion

        /// <summary>
        /// Registers a SyncType.
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="index"></param>
        internal void RegisterSyncType(SyncBase sb, uint index)
        {
            if (!_syncTypes.TryAdd(index, sb))
                NetworkManager.LogError($"SyncType key {index} has already been added for {GetType().FullName} on {gameObject.name}");
        }
        /// <summary>
        /// Sets a SyncVar as dirty.
        /// </summary>
        /// <param name="isSyncObject">True if dirtying a syncObject.</param>
        /// <returns>True if able to dirty SyncType.</returns>
        internal bool DirtySyncType()
        {
            if (!IsServerStarted)
                return false;
            /* No reason to dirty if there are no observers.
             * This can happen even if a client is going to see
             * this object because the server side initializes
             * before observers are built. */
            if (_networkObjectCache.Observers.Count == 0 && !_networkObjectCache.PredictedSpawner.IsValid)
                return false;

            if (!SyncTypeDirty)
                _networkObjectCache.NetworkManager.ServerManager.Objects.SetDirtySyncType(this);
            SyncTypeDirty = true;

            return true;
        }

        /// <summary>
        /// Initializes SyncTypes. This will only call once even as host.
        /// </summary>
        private void InitializeOnceSyncTypes(bool asServer)
        {
            if (asServer)
            {
                if (!_initializedOnceServer)
                {
                    //optimization Cache synctypewriters on despawn and get from cache on spawn.
                    //Only need to initialize readpermissions once, it's static.
                    if (_readPermissions == null)
                    {
                        System.Array arr = System.Enum.GetValues(typeof(ReadPermission));
                        _readPermissions = new ReadPermission[arr.Length];

                        int count = 0;
                        foreach (ReadPermission rp in arr)
                        {
                            _readPermissions[count] = rp;
                            count++;
                        }
                    }

                    //Build writers for observers and owner.
                    _syncTypeWriters = new SyncTypeWriter[_readPermissions.Length];
                    for (int i = 0; i < _syncTypeWriters.Length; i++)
                        _syncTypeWriters[i] = new SyncTypeWriter(_readPermissions[i]);
                }
                else
                {
                    //Reset writers.
                    for (int i = 0; i < _syncTypeWriters.Length; i++)
                        _syncTypeWriters[i].Reset();
                }
            }

            /* Initialize synctypes every spawn because there could be
             * callbacks which occur that the user or even we may implement
             * during the initialization. */
            foreach (SyncBase sb in _syncTypes.Values)
                sb.PreInitialize(_networkObjectCache.NetworkManager);
        }


        /// <summary>
        /// Reads a SyncVar.
        /// </summary>
        /// <param name="reader"></param>
        internal void OnSyncType(PooledReader reader, int length, bool asServer = false)
        {
            int readerStart = reader.Position;
            while (reader.Position - readerStart < length)
            {
                byte index = reader.ReadUInt8Unpacked();
                if (_syncTypes.TryGetValueIL2CPP(index, out SyncBase sb))
                    sb.Read(reader, asServer);
                else
                    NetworkManager.LogWarning($"SyncType not found for index {index} on {transform.name}. Remainder of packet may become corrupt.");
            }
        }

        /// <summary>
        /// Writers dirty SyncTypes if their write tick has been met.
        /// </summary>
        /// <returns>True if there are no pending dirty sync types.</returns>
        internal bool WriteDirtySyncTypes(bool ignoreInterval = false, bool forceReliable = false, bool writeOnlyOwner = false)
        {
            /* Can occur when a synctype is queued after
             * the object is marked for destruction. This should not
             * happen under most conditions since synctypes will be
             * pushed through when despawn is called. */
            if (!IsSpawned)
            {
                SyncTypes_ResetState();
                return true;
            }

            /* If there is nothing dirty then return true, indicating no more
             * pending dirty checks. */
            if ((!writeOnlyOwner && !SyncTypeDirty) || _syncTypes.Count == 0)
                return true;

            //Number of syncTypes which are/were dirty.
            int dirtyCount = 0;
            //Number of syncTypes which were written.
            int writtenCount = 0;

            /* True if writers have been reset for this check.
             * For perf writers are only reset when data is to be written. */
            bool writersReset = false;
            uint tick = _networkObjectCache.NetworkManager.TimeManager.Tick;

            foreach (SyncBase sb in _syncTypes.Values)
            {
                bool isForceOwnerWrite = (writeOnlyOwner && sb.Settings.ReadPermission == ReadPermission.OwnerOnly);
                /* If not forceOwnerOnly and is not OwnerOnly, or if not
                 * Dirty then continue. */
                //If forceOnlyOwner and is owner, or is dirty then proceed.
                if (!isForceOwnerWrite && !sb.IsDirty)
                    continue;

                dirtyCount++;
                if (ignoreInterval || sb.SyncTimeMet(tick))
                {
                    writtenCount++;
                    //If writers still need to be reset.
                    if (!writersReset)
                    {
                        writersReset = true;
                        //Reset writers.
                        for (int i = 0; i < _syncTypeWriters.Length; i++)
                            _syncTypeWriters[i].Reset();
                    }

                    if (forceReliable)
                        sb.SetCurrentChannel(Channel.Reliable);
                    //Find channel.
                    byte channel = (byte)sb.Channel;
                    sb.ResetDirty();
                    //If ReadPermission is owner but no owner skip this syncvar write.
                    if (sb.Settings.ReadPermission == ReadPermission.OwnerOnly && !_networkObjectCache.Owner.IsValid)
                        continue;

                    //Find PooledWriter to use.
                    PooledWriter writer = null;
                    for (int i = 0; i < _syncTypeWriters.Length; i++)
                    {
                        if (_syncTypeWriters[i].ReadPermission == sb.Settings.ReadPermission)
                        {
                            /* Channel for syncVar is beyond available channels in transport.
                             * Use default reliable. */
                            if (channel >= _syncTypeWriters[i].Writers.Length)
                                channel = (byte)Channel.Reliable;

                            writer = _syncTypeWriters[i].Writers[channel];
                            break;
                        }
                    }

                    if (writer == null)
                    {
                        NetworkManager.LogError($"Writer couldn't be found for permissions {sb.Settings.ReadPermission} on channel {channel}.");
                    }
                    else
                    {
                        if (isForceOwnerWrite)
                            sb.WriteFull(writer);
                        else
                            sb.WriteDelta(writer);
                    }
                }
            }

            //If no dirty were found.
            if (dirtyCount == 0)
            {
                SyncTypeDirty = false;
                return true;
            }
            //At least one sync type was dirty.
            else if (writtenCount > 0)
            {
                for (int i = 0; i < _syncTypeWriters.Length; i++)
                {
                    for (byte channel = 0; channel < _syncTypeWriters[i].Writers.Length; channel++)
                    {
                        PooledWriter channelWriter = _syncTypeWriters[i].Writers[channel];
                        //If there is data to send.
                        if (channelWriter.Length > 0)
                        {
                            PooledWriter headerWriter = WriterPool.Retrieve();
                            //Write the packetId and NB information.
                            headerWriter.WritePacketIdUnpacked(PacketId.SyncType);
                            PooledWriter dataWriter = WriterPool.Retrieve();
                            dataWriter.WriteNetworkBehaviour(this);
                            dataWriter.WriteUInt8ArrayAndSize(channelWriter.GetBuffer(), 0, channelWriter.Length);
                            //Attach data onto packetWriter.
                            headerWriter.WriteArraySegment(dataWriter.GetArraySegment());
                            dataWriter.Store();


                            //If only sending to owner.
                            if (_syncTypeWriters[i].ReadPermission == ReadPermission.OwnerOnly)
                            {
                                _networkObjectCache.NetworkManager.TransportManager.SendToClient(channel, headerWriter.GetArraySegment(), _networkObjectCache.Owner);
                            }
                            //Sending to observers.
                            else
                            {
                                bool excludeOwner = (_syncTypeWriters[i].ReadPermission == ReadPermission.ExcludeOwner);
                                SetNetworkConnectionCache(false, excludeOwner);
                                _networkObjectCache.NetworkManager.TransportManager.SendToClients((byte)channel, headerWriter.GetArraySegment(), _networkObjectCache.Observers, _networkConnectionCache);

                            }

                            headerWriter.Store();
                        }
                    }
                }

                /* If the number written is the same as those which were dirty
                 * then no dirty rename. Return true if no dirty remain. */
                bool wroteAllDirty = (writtenCount == dirtyCount);
                if (wroteAllDirty)
                    SyncTypeDirty = false;
                return wroteAllDirty;
            }
            //If here then at least one was dirty but none were written. This means some still need to write.
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Resets all SyncTypes for this NetworkBehaviour for server and client.
        /// </summary>
        internal void SyncTypes_ResetState()
        {
            foreach (SyncBase item in _syncTypes.Values)
                item.ResetState();

            SyncTypeDirty = false;
        }
        /// <summary>
        /// Resets all SyncTypes for this NetworkBehaviour for server or client.
        /// </summary>
        internal void ResetState_SyncTypes(bool asServer)
        {
            foreach (SyncBase item in _syncTypes.Values)
                item.ResetState(asServer);

            if (asServer)
                SyncTypeDirty = false;
        }

        /// <summary>
        /// Resets all SyncVar fields for the class to the values within their SyncVar class.
        /// EG: _mySyncVar = generated_mySyncVar.GetValue(...)
        /// </summary>
        [MakePublic]
        internal virtual void ResetSyncVarFields() { }

        /// <summary>
        /// Writes syncVars for a spawn message.
        /// </summary>
        /// <param name="conn">Connection SyncTypes are being written for.</param>
        internal void WriteSyncTypesForSpawn(PooledWriter writer, NetworkConnection conn)
        {
            PooledWriter syncTypeWriter = WriterPool.Retrieve();
            /* Since all values are being written everything is
             * written in order so there's no reason to pass
             * indexes. */
            foreach (SyncBase sb in _syncTypes.Values)
            {
                /* If connection is null then write for all.
                 * This can only occur when client is sending syncTypes
                 * to the server. This will be removed when predicted
                 * spawning payload is added in. */ //todo remove this after predicted spawning payload.
                if (conn != null)
                {
                    //True if conn is the owner of this object.
                    bool connIsOwner = (conn == _networkObjectCache.Owner);
                    //Read permissions for the synctype.
                    ReadPermission rp = sb.Settings.ReadPermission;
                    /* SyncType only allows owner to receive values and
                     * conn is not the owner. */
                    if (rp == ReadPermission.OwnerOnly && !connIsOwner)
                        continue;
                    //Write to everyone but the owner.
                    if (rp == ReadPermission.ExcludeOwner && connIsOwner)
                        continue;
                }

                //Anything beyond this is fine to write for everyone.
                sb.WriteFull(syncTypeWriter);
            }

            writer.WriteUInt8ArrayAndSize(syncTypeWriter.GetBuffer(), 0, syncTypeWriter.Length);
            syncTypeWriter.Store();
        }

    }


}

