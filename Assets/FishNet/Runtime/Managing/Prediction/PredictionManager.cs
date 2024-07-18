using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FishNet.Managing.Predicting
{

    /// <summary>
    /// Additional options for managing the observer system.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/PredictionManager")]
    public sealed class PredictionManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called before performing a reconcile. Contains the client and server tick the reconcile is for.
        /// </summary>
        public event PreReconcileDel OnPreReconcile;

        public delegate void PreReconcileDel(uint clientTick, uint serverTick);
        /// <summary>
        /// Called when performing a reconcile.
        /// This is used internally to reconcile objects and does not gaurantee your subscriptions to this event will process before or after internal components.
        /// </summary>
        public event ReconcileDel OnReconcile;
        public delegate void ReconcileDel(uint clientTick, uint serverTick);
        /// <summary>
        /// Called after performing a reconcile. Contains the client and server tick the reconcile is for.
        /// </summary>
        public event PostReconcileDel OnPostReconcile;
        public delegate void PostReconcileDel(uint clientTick, uint serverTick);
        /// <summary>
        /// Called before Physics SyncTransforms are run after a reconcile.
        /// This will only invoke if physics are set to TimeManager, within the TimeManager inspector.
        /// </summary>
        public event PrePhysicsSyncTransformDel OnPrePhysicsTransformSync;
        public delegate void PrePhysicsSyncTransformDel(uint clientTick, uint serverTick);
        /// <summary>
        /// Called after Physics SyncTransforms are run after a reconcile.
        /// This will only invoke if physics are set to TimeManager, within the TimeManager inspector.
        /// </summary>
        public event PostPhysicsSyncTransformDel OnPostPhysicsTransformSync;
        public delegate void PostPhysicsSyncTransformDel(uint clientTick, uint serverTick);

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// </summary>
        public event PreReplicateReplayDel OnPreReplicateReplay;
        public delegate void PreReplicateReplayDel(uint clientTick, uint serverTick);
        /// <summary>
        /// Called when replaying a replication.
        /// This is called before physics are simulated.
        /// This is used internally to replay objects and does not gaurantee your subscriptions to this event will process before or after internal components.
        /// </summary>
        internal event ReplicateReplayDel OnReplicateReplay;
        public delegate void ReplicateReplayDel(uint clientTick, uint serverTick);
        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// </summary>
        public event PostReplicateReplayDel OnPostReplicateReplay;
        public delegate void PostReplicateReplayDel(uint clientTick, uint serverTick);
        /// <summary>
        /// True if client timing needs to be reduced. This is fine-tuning of the prediction system.
        /// </summary>
        internal bool ReduceClientTiming;
        /// <summary>
        /// True if prediction is currently reconciling. While reconciling run replicates will be replays.
        /// </summary>
        public bool IsReconciling { get; private set; }
        /// <summary>
        /// When not unset this is the current tick which local client is replaying authoraitive inputs on.
        /// </summary>
        public uint ClientReplayTick { get; private set; } = TimeManager.UNSET_TICK;
        /// <summary>
        /// When not unset this is the current tick which local client is replaying non-authoraitive inputs on.
        /// </summary>
        public uint ServerReplayTick { get; private set; } = TimeManager.UNSET_TICK;
        /// <summary>
        /// Local tick on the most recent performed reconcile.
        /// </summary>
        public uint ClientStateTick { get; private set; } = TimeManager.UNSET_TICK;
        /// <summary>
        /// Server tick on the most recent performed reconcile.
        /// </summary>
        public uint ServerStateTick { get; private set; } = TimeManager.UNSET_TICK;
        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to drop replicates from clients which are being received excessively. This can help with attacks but may cause client to temporarily desynchronize during connectivity issues. When false the server will hold at most up to 3 seconds worth of replicates, consuming multiple per tick to clear out the buffer quicker. This is good to ensure all inputs are executed but potentially could allow speed hacking.")]
        [SerializeField]
        private bool _dropExcessiveReplicates = true;
        /// <summary>
        /// True to drop replicates from clients which are being received excessively. This can help with attacks but may cause client to temporarily desynchronize during connectivity issues.
        /// When false the server will hold at most up to 3 seconds worth of replicates, consuming multiple per tick to clear out the buffer quicker. This is good to ensure all inputs are executed but potentially could allow speed hacking.
        /// </summary>
        internal bool DropExcessiveReplicates => _dropExcessiveReplicates;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of replicates a server can queue per object. Higher values will put more load on the server and add replicate latency for the client.")]
        [SerializeField]
        private byte _maximumServerReplicates = 15;
        /// <summary>
        /// Maximum number of replicates a server can queue per object. Higher values will put more load on the server and add replicate latency for the client.
        /// </summary>
        public byte GetMaximumServerReplicates() => _maximumServerReplicates;
        /// <summary>
        /// Sets the maximum number of replicates a server can queue per object.
        /// </summary>
        /// <param name="value"></param>
        public void SetMaximumServerReplicates(byte value)
        {
            _maximumServerReplicates = (byte)Mathf.Clamp(value, MINIMUM_REPLICATE_QUEUE_SIZE, MAXIMUM_REPLICATE_QUEUE_SIZE);
        }
        /// <summary>
        /// No more than this value of replicates should be stored as a buffer.
        /// </summary>
        internal ushort MaximumPastReplicates => (ushort)(_networkManager.TimeManager.TickRate * 5);
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How many states to try and hold in a buffer before running them on clients. Larger values add resilience against network issues at the cost of running states later.")]
        [Range(0, MAXIMUM_PAST_INPUTS)]
        [FormerlySerializedAs("_redundancyCount")] //Remove on V5.
        [FormerlySerializedAs("_interpolation")] //Remove on V5.
        [SerializeField]
        private byte _stateInterpolation = 1;
        /// <summary>
        /// How many states to try and hold in a buffer before running them. Larger values add resilience against network issues at the cost of running states later.
        /// </summary> 
        internal byte StateInterpolation => _stateInterpolation;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("The order in which clients run states. Future favors performance and does not depend upon reconciles, while Past favors accuracy but clients must reconcile every tick.")]
        [SerializeField]
        private ReplicateStateOrder _stateOrder = ReplicateStateOrder.Appended;
        /// <summary>
        /// The order in which states are run. Future favors performance and does not depend upon reconciles, while Past favors accuracy but clients must reconcile every tick.
        /// </summary>
        public ReplicateStateOrder StateOrder => _stateOrder;
        /// <summary>
        /// True if StateOrder is set to future.
        /// </summary>
        internal bool IsAppendedStateOrder => (_stateOrder == ReplicateStateOrder.Appended);
        /// <summary>
        /// Sets the current ReplicateStateOrder. This may be changed at runtime.
        /// Changing this value only affects the client which it is changed on.
        /// </summary>
        /// <param name="stateOrder"></param>
        public void SetStateOrder(ReplicateStateOrder stateOrder)
        {
            //Server doesnt use state order, exit early if server.
            if (_networkManager.IsServerStarted)
                return;
            //Same as before, do nothing.
            if (stateOrder == _stateOrder)
                return;

            _stateOrder = stateOrder;
            /* If client is started and if new order is
             * past then tell all spawned objects to
             * clear future queue. */
            if (stateOrder == ReplicateStateOrder.Inserted && _networkManager.IsClientStarted)
            {
                foreach (NetworkObject item in _networkManager.ClientManager.Objects.Spawned.Values)
                    item.EmptyReplicatesQueueIntoHistory();
            }
        }
        /// <summary>
        /// Number of past inputs to send, which is also the number of times to resend final datas.
        /// </summary>
        internal byte RedundancyCount => (byte)(_stateInterpolation + 1);
        ///// <summary>
        ///// 
        ///// </summary>
        //[Tooltip("How many states to try and hold in a buffer before running them on server. Larger values add resilience against network issues at the cost of running states later.")]
        //[Range(0, MAXIMUM_PAST_INPUTS + 30)]
        //[SerializeField]
        //private byte _serverInterpolation = 1;
        ///// <summary>
        ///// How many states to try and hold in a buffer before running them on server. Larger values add resilience against network issues at the cost of running states later.
        ///// </summary>
        //internal byte ServerInterpolation => _serverInterpolation;
        #endregion

        #region Private.
        /// <summary>
        /// Number of reconciles dropped due to high latency.
        /// This is not necessarily needed but can save performance on machines struggling to keep up with simulations when combined with low frame rate.
        /// </summary>
        private byte _droppedReconcilesCount;
        /// <summary>
        /// Current reconcile state to use.
        /// </summary>
        //private StatePacket _reconcileState;
        private Queue<StatePacket> _reconcileStates = new Queue<StatePacket>();
        /// <summary>
        /// Look up to find states by their tick.
        /// Key: client LocalTick on the state.
        /// Value: StatePacket stored.
        /// </summary>
        private Dictionary<uint, StatePacket> _stateLookups = new Dictionary<uint, StatePacket>();
        /// <summary>
        /// Last ordered tick read for a reconcile state.
        /// </summary>
        private uint _lastOrderedReadReconcileTick;
        /// <summary>
        /// NetworkManager used with this.
        /// </summary>
        private NetworkManager _networkManager;
        #endregion

        #region Const.
        /// <summary>
        /// Minimum number of past inputs which can be sent.
        /// </summary>
        private const byte MINIMUM_PAST_INPUTS = 1;
        /// <summary>
        /// Maximum number of past inputs which can be sent.
        /// </summary>
        internal const byte MAXIMUM_PAST_INPUTS = 5;
        /// <summary>
        /// Minimum amount of replicate queue size.
        /// </summary>
        private const byte MINIMUM_REPLICATE_QUEUE_SIZE = (MINIMUM_PAST_INPUTS + 1);
        /// <summary>
        /// Maxmimum amount of replicate queue size.
        /// </summary>
        private const byte MAXIMUM_REPLICATE_QUEUE_SIZE = byte.MaxValue;
        #endregion

        internal void InitializeOnce(NetworkManager manager)
        {
            _networkManager = manager;
            ClampInterpolation();
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        }

        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            _droppedReconcilesCount = 0;
            _lastOrderedReadReconcileTick = 0;
        }

        /// <summary>
        /// Amount to reserve for the header of a state update.
        /// </summary>
        internal const int STATE_HEADER_RESERVE_LENGTH = (TransportManager.PACKETID_LENGTH + TransportManager.UNPACKED_TICK_LENGTH + TransportManager.UNPACKED_SIZE_LENGTH);

        /// <summary>
        /// Clamps queued inputs to a valid value.
        /// </summary>
        private void ClampInterpolation()
        {
            ushort startingValue = _stateInterpolation;
            //Check for setting if dropping.
            if (_dropExcessiveReplicates && _stateInterpolation > _maximumServerReplicates)
                _stateInterpolation = (byte)(_maximumServerReplicates - 1);

            //If changed.
            if (_stateInterpolation != startingValue)
                _networkManager.Log($"Interpolation has been set to {_stateInterpolation}.");
        }

        internal class StatePacket : IResettable
        {
            public struct IncomingData
            {
                public ArraySegment<byte> Data;
                public Channel Channel;

                public IncomingData(ArraySegment<byte> data, Channel channel)
                {
                    Data = data;
                    Channel = channel;
                }
            }
            public List<IncomingData> Datas;
            public uint ClientTick;
            public uint ServerTick;

            public void Update(ArraySegment<byte> data, uint clientTick, uint serverTick, Channel channel)
            {
                AddData(data, channel);
                ServerTick = serverTick;
                ClientTick = clientTick;
            }

            public void AddData(ArraySegment<byte> data, Channel channel)
            {
                Datas.Add(new IncomingData(data, channel));
            }

            public void ResetState()
            {
                for (int i = 0; i < Datas.Count; i++)
                    ByteArrayPool.Store(Datas[i].Data.Array);

                CollectionCaches<IncomingData>.StoreAndDefault(ref Datas);
            }

            public void InitializeState()
            {
                Datas = CollectionCaches<IncomingData>.RetrieveList();
            }
        }

        /// <summary>
        /// Reconciles to received states.
        /// </summary>
        internal void ReconcileToStates()
        {            
            if (!_networkManager.IsClientStarted)
                return;
            //No states.
            if (_reconcileStates.Count == 0)
                return;

            TimeManager tm = _networkManager.TimeManager;
            uint localTick = tm.LocalTick;
            uint estimatedLastRemoteTick = tm.LastPacketTick.Value();

            /* When there is an excessive amount of states try to consume
             * some.This only happens when the client gets really far behind
             * and has to catch up, such as a latency increase then drop. 
             * Limit the number of states consumed per tick so the clients
             * computer doesn't catch fire. */
            int iterations = 0;

            while (_reconcileStates.Count > 0)
            {
                iterations++;
                /* Typically there should only be 'interpolation' amount in queue but
                 * there can be more if the clients network is unstable and they are
                 * arriving in burst.
                 * If there's more than interpolation (+1 for as a leniency buffer) then begin to
                 * consume multiple. */
                byte stateInterpolation = StateInterpolation;
                int maxIterations = (_reconcileStates.Count > (stateInterpolation + 1)) ? 2 : 1;
                //At most 2 iterations.
                if (iterations > maxIterations)
                    return;

                StatePacket sp;
                if (!ConditionsMet(_reconcileStates.Peek()))
                    return;
                else
                    sp = _reconcileStates.Dequeue();

                //Condition met. See if the next one matches condition, if so drop current.
                //Returns if a state has it's conditions met.
                bool ConditionsMet(StatePacket spChecked)
                {
                    if (spChecked == null)
                        return false;

                    /* varianceAllowance gives a few ticks to provide opportunity for late
                     * packets to arrive. This adds on varianceAllowance to replays but greatly
                     * increases the chances of the state being received before skipping past it
                     * in a replay.
                     * 
                     * When using Inserted (not AppendedStateOrder) there does not need to be any
                     * additional allowance since there is no extra queue like appended, they rather just
                     * go right into the past. */
                    uint varianceAllowance = (IsAppendedStateOrder) ? (uint)2 : (uint)0;
                    uint serverTickDifferenceRequirement = (varianceAllowance + stateInterpolation);

                    bool serverPass = (spChecked.ServerTick < (estimatedLastRemoteTick - serverTickDifferenceRequirement));
                    bool clientPass = spChecked.ClientTick < (localTick - stateInterpolation);
                    return (serverPass && clientPass);
                }

                bool dropReconcile = false;
                uint clientTick = sp.ClientTick;
                uint serverTick = sp.ServerTick;

                /* If client has a low frame rate
                 * then limit the number of reconciles to prevent further performance loss. */
                if (_networkManager.TimeManager.LowFrameRate)
                {
                    /* Limit 3 drops a second. DropValue will be roughly the same
                     * as every 330ms. */
                    int reconcileValue = Mathf.Max(1, (_networkManager.TimeManager.TickRate / 3));
                    //If cannot drop then reset dropcount.
                    if (_droppedReconcilesCount >= reconcileValue)
                    {
                        _droppedReconcilesCount = 0;
                    }
                    //If can drop...
                    else
                    {
                        dropReconcile = true;
                        _droppedReconcilesCount++;
                    }
                }
                //}
                //No reason to believe client is struggling, allow reconcile.
                else
                {
                    _droppedReconcilesCount = 0;
                }

                if (!dropReconcile)
                {
                    IsReconciling = true;
                    ClientStateTick = clientTick;
                    /* This is the tick which the reconcile is for.
                     * Since reconciles are performed after replicate, if
                     * the replicate was on tick 100 then this reconcile is the state
                     * on tick 100, after the replicate is performed. */
                    ServerStateTick = serverTick;

                    //Have the reader get processed.
                    foreach (StatePacket.IncomingData item in sp.Datas)
                    {
                        PooledReader reader = ReaderPool.Retrieve(item.Data, _networkManager, Reader.DataSource.Server);
                        _networkManager.ClientManager.ParseReader(reader, item.Channel);
                        ReaderPool.Store(reader);
                    }

                    bool timeManagerPhysics = (tm.PhysicsMode == PhysicsMode.TimeManager);
                    float tickDelta = ((float)tm.TickDelta * _networkManager.TimeManager.GetPhysicsTimeScale());

                    OnPreReconcile?.Invoke(ClientStateTick, ServerStateTick);
                    OnReconcile?.Invoke(ClientStateTick, ServerStateTick);

                    if (timeManagerPhysics)
                    {
                        OnPrePhysicsTransformSync?.Invoke(ClientStateTick, ServerStateTick);
                        Physics.SyncTransforms();
                        Physics2D.SyncTransforms();
                        OnPostPhysicsTransformSync?.Invoke(ClientStateTick, ServerStateTick);
                    }
                    /* Set first replicate to be the 1 tick
                     * after reconcile. This is because reconcile calcs
                     * should be performed after replicate has run. 
                     * In result object will reconcile to data AFTER
                     * the replicate tick, and then run remaining replicates as replay. 
                     *
                     * Replay up to localtick, excluding localtick. There will
                     * be no input for localtick since reconcile runs before
                     * OnTick. */
                    ClientReplayTick = ClientStateTick + 1;
                    ServerReplayTick = ServerStateTick + 1;

                    /* Only replay up to but excluding local tick.
                     * This prevents client from running 1 local tick into the future
                     * since the OnTick has not run yet. 
                     *
                     * EG: if localTick is 100 replay will run up to 99, then OnTick
                     * will fire for 100.                     */
                    while (ClientReplayTick < localTick)
                    {
                        OnPreReplicateReplay?.Invoke(ClientReplayTick, ServerReplayTick);
                        OnReplicateReplay?.Invoke(ClientReplayTick, ServerReplayTick);
                        if (timeManagerPhysics)
                        {
                            Physics.Simulate(tickDelta);
                            Physics2D.Simulate(tickDelta);
                        }
                        OnPostReplicateReplay?.Invoke(ClientReplayTick, ServerReplayTick);
                        ClientReplayTick++;
                        ServerReplayTick++;
                    }

                    OnPostReconcile?.Invoke(ClientStateTick, ServerStateTick);

                    ClientStateTick = TimeManager.UNSET_TICK;
                    ServerStateTick = TimeManager.UNSET_TICK;
                    ClientReplayTick = TimeManager.UNSET_TICK;
                    ServerReplayTick = TimeManager.UNSET_TICK;
                    IsReconciling = false;
                }

                DisposeOfStatePacket(sp);
            }
        }
        
        /// <summary>
        /// Sends written states for clients.
        /// </summary>
        internal void SendStateUpdate()
        {
            byte stateInterpolation = StateInterpolation;
            TransportManager tm = _networkManager.TransportManager;
            //Must have replicated within two timing intervals.
            uint recentReplicateToTicks = (_networkManager.TimeManager.TimingTickInterval * 2);

            foreach (NetworkConnection nc in _networkManager.ServerManager.Clients.Values)
            {
                uint lastReplicateTick;
                //If client has performed a replicate recently.
                if (!nc.ReplicateTick.IsUnset)
                {
                    lastReplicateTick = nc.ReplicateTick.Value();
                }
                /* If not then use what is estimated to be the clients
                 * current tick along with desired interpolation.
                 * This should be just about the same as if the client used replicate recently. */
                else
                {
                    uint ncLocalTick = nc.LocalTick.Value();
                    uint interpolationDifference = ((uint)stateInterpolation * 2);
                    if (ncLocalTick < interpolationDifference)
                        ncLocalTick = 0;

                    lastReplicateTick = ncLocalTick;
                }

                foreach (PooledWriter writer in nc.PredictionStateWriters)
                {
                    /* Packet is sent as follows...
                     * PacketId.
                     * LastReplicateTick of receiver.
                     * Length of packet.
                     * Data. */
                    ArraySegment<byte> segment = writer.GetArraySegment();
                    writer.Position = 0;
                    writer.WritePacketIdUnpacked(PacketId.StateUpdate);
                    writer.WriteTickUnpacked(lastReplicateTick);

                    /* Send the full length of the writer excluding
                     * the reserve count of the header. The header reserve
                     * count will always be the same so that can be parsed
                     * off immediately upon receiving. */
                    int dataLength = (segment.Count - STATE_HEADER_RESERVE_LENGTH);
                    //Write length.
                    writer.WriteInt32Unpacked(dataLength);
                    //Channel is defaulted to unreliable.
                    Channel channel = Channel.Unreliable;
                    //If a single state exceeds MTU it must be sent on reliable. This is extremely unlikely.
                    _networkManager.TransportManager.CheckSetReliableChannel(segment.Count, ref channel);
                    tm.SendToClient((byte)channel, segment, nc, true);
                }

                nc.StorePredictionStateWriters();
            }
        }


        /// <summary>
        /// Parses a received state update.
        /// </summary>
        internal void ParseStateUpdate(PooledReader reader, Channel channel)
        {
            uint lastRemoteTick = _networkManager.TimeManager.LastPacketTick.LastRemoteTick;
            //If server or state is older than another received state.
            if (_networkManager.IsServerStarted || (lastRemoteTick < _lastOrderedReadReconcileTick))
            {
                /* If the server is receiving a state update it can
                 * simply discard the data since the server will never
                 * need to reset states. This can occur on the clientHost
                 * side. */
                reader.ReadTickUnpacked();
                int length = reader.ReadInt32Unpacked();
                reader.Skip(length);
            }
            else
            {
                _lastOrderedReadReconcileTick = lastRemoteTick;

                /* There should never really be more than queuedInputs so set
                 * a limit a little beyond to prevent reconciles from building up. 
                 * This is more of a last result if something went terribly
                 * wrong with the network. */
                int adjustedStateInterpolation = (StateInterpolation * 4) + 2;
                /* If appending allow an additional of stateInterpolation since
                 * entries arent added into the past until they are run on the appended
                 * queue for each networkObject. */
                if (IsAppendedStateOrder)
                    adjustedStateInterpolation += StateInterpolation;
                int maxAllowedStates = Mathf.Max(adjustedStateInterpolation, 4);

                while (_reconcileStates.Count > maxAllowedStates)
                {
                    StatePacket oldSp = _reconcileStates.Dequeue();
                    DisposeOfStatePacket(oldSp);
                }

                //LocalTick of this client the state is for.
                uint clientTick = reader.ReadTickUnpacked();
                //Length of packet.
                int length = reader.ReadInt32Unpacked();
                //Read data into array.
                byte[] arr = ByteArrayPool.Retrieve(length);
                reader.ReadUInt8Array(ref arr, length);
                //Make segment and store into states.
                ArraySegment<byte> segment = new ArraySegment<byte>(arr, 0, length);

                /* See if an entry was already added for the clientTick. If so then
                 * add onto the datas. Otherwise add a new state packet. */
                if (_stateLookups.TryGetValue(clientTick, out StatePacket sp1))
                {
                    sp1.AddData(segment, channel);
                }
                else
                {
                    StatePacket sp2 = ResettableObjectCaches<StatePacket>.Retrieve();
                    sp2.Update(segment, clientTick, lastRemoteTick, channel);
                    _stateLookups[clientTick] = sp2;
                    _reconcileStates.Enqueue(sp2);
                }
            }
        }

        /// <summary>
        /// Disposes of and cleans up everything related to a StatePacket.
        /// </summary>
        private void DisposeOfStatePacket(StatePacket sp)
        {
            uint clientTick = sp.ClientTick;
            _stateLookups.Remove(clientTick);
            ResettableObjectCaches<StatePacket>.Store(sp);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ClampInterpolation();
        }

#endif
    }

}
