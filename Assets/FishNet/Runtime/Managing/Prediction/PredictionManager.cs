#if UNITY_EDITOR || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif
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
using FishNet.Managing.Statistic;
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
        #region Types.
        /// <summary>
        /// Responsible for throttling client reconcile amounts.
        /// </summary>
        internal class ClientReconcileThrottler
        {
            /// <summary>
            /// True if the last reconcile attempt was throttled.
            /// </summary>
            public bool WasLastAttemptThrottled { get; private set; }
            /// <summary>
            /// Last time a reconcile ran.
            /// </summary>
            private float _lastReconcileUnscaledTime;
            /// <summary>
            /// Number of frames passed.
            /// </summary>
            private ushort _accumulatedFrames;
            /// <summary>
            /// Last evaluated frame rate.
            /// </summary>
            private ushort _evaluatedFramerate;
            /// <summary>
            /// Total delta time added by frames since the last reset.
            /// </summary>
            private float _accumulatedDeltaTime;
            /// <summary>
            /// Value to use when no frames are recorded.
            /// </summary>
            private const ushort UNSET_FRAME_COUNT = 0;

            /// <summary>
            /// True if a reconcile can be performed and sets the next time a reconcile may occur if so.
            /// </summary>
            /// <returns></returns>
            public bool TryReconcile(ushort minimumFrameRate)
            {
                //No frames are set -- allow reconcile.
                if (_evaluatedFramerate == UNSET_FRAME_COUNT)
                    return ReturnTrueAndUpdateValues();

                if (_evaluatedFramerate >= minimumFrameRate)
                    return ReturnTrueAndUpdateValues();

                /* If here then frames are not met. */

                //Enough time has passed since last reconcile to run another.
                //Not enough time has passed.
                if (Time.fixedUnscaledTime - _lastReconcileUnscaledTime >= 0.25f)
                    return ReturnTrueAndUpdateValues();

                /* Reconcile will not be performed if here. */

                WasLastAttemptThrottled = true;
                return false;

                bool ReturnTrueAndUpdateValues()
                {
                    _lastReconcileUnscaledTime = Time.unscaledTime;
                    WasLastAttemptThrottled = false;

                    return true;
                }
            }

            /// <summary>
            /// Adds that a frame had occurred.
            /// </summary>
            public void AddFrame(float unscaledDeltaTime)
            {
                _accumulatedDeltaTime += unscaledDeltaTime;

                if (_accumulatedFrames < ushort.MaxValue)
                    _accumulatedFrames++;

                /* Frames will only be updated every three seconds. */
                if (_accumulatedDeltaTime < 3f)
                    return;

                //Update evaluated frame rate.
                _evaluatedFramerate = _accumulatedFrames;

                _accumulatedDeltaTime = 0f;
                _accumulatedFrames = 0;
            }

            /// <summary>
            /// Resets current values.
            /// </summary>
            public void ResetState()
            {
                _accumulatedDeltaTime = 0f;
                _accumulatedFrames = UNSET_FRAME_COUNT;
                WasLastAttemptThrottled = false;
                _lastReconcileUnscaledTime = 0f;
            }
        }

        private class StatePacketTick
        {
            private uint _client = TimeManager.UNSET_TICK;
            private uint _server = TimeManager.UNSET_TICK;
            /// <summary>
            /// Returns if ticks are unset.
            /// Only client needs to be checked, as they both are set with non default at the same time.
            /// </summary>
            public bool IsUnset => _client == TimeManager.UNSET_TICK;

            public void Update(uint client, uint server)
            {
                _client = client;
                _server = server;
            }

            /// <summary>
            /// Adds ticks onto each field.
            /// </summary>
            public void AddTick(uint quantity)
            {
                _client += quantity;
                _server += quantity;
            }
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
                if (data.Array != null)
                    Datas.Add(new(data, channel));
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
        #endregion

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

        public event PostPhysicsSyncTransformDel OnPostReconcileSyncTransforms;
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
        /// True to reduce reconciles when frame rate drops below a threshold.
        /// When frame rate drops below the specified value reconciles will be reduced to roughly 4-5 times a second.
        /// </summary>
        [Tooltip("True to reduce reconciles when frame rate drops below a threshold. When frame rate drops below the specified value reconciles will be reduced to roughly 4-5 times a second.")]
        [SerializeField]
        private bool _reduceReconcilesWithFramerate = true;
        /// <summary>
        /// Frame rate client must fall below to begin reducing how many reconciles the client runs locally.
        /// </summary>
        [Tooltip("Frame rate client must fall below to begin reducing how many reconciles the client runs locally.")]
        [Range(15, NetworkManager.MAXIMUM_FRAMERATE)]
        [SerializeField]
        private ushort _minimumClientReconcileFramerate = 50;
        /// <summary>
        /// True for the client to create local reconcile states. Enabling this feature allows reconciles to be sent less frequently and provides data to use for reconciles when packets are lost.
        /// </summary>
        internal bool CreateLocalStates => _createLocalStates;
        [FormerlySerializedAs("_localStates")]
        [Tooltip("True for the client to create local reconcile states. Enabling this feature allows reconciles to be sent less frequently and provides data to use for reconciles when packets are lost.")]
        [SerializeField]
        private bool _createLocalStates = true;
        /// <summary>
        /// How many states to try and hold in a buffer before running them. Larger values add resilience against network issues at the cost of running states later.
        /// </summary>
        public byte StateInterpolation => _stateInterpolation;
        [Tooltip("How many states to try and hold in a buffer before running them on clients. Larger values add resilience against network issues at the cost of running states later.")]
        [Range(0, MAXIMUM_PAST_INPUTS)]
        [FormerlySerializedAs("_redundancyCount")] // Remove on V5.
        [FormerlySerializedAs("_interpolation")] // Remove on V5.
        [SerializeField]
        private byte _stateInterpolation = 2;
        /// <summary>
        /// The order in which states are run. Future favors performance and does not depend upon reconciles, while Past favors accuracy but clients must reconcile every tick.
        /// </summary>
        public ReplicateStateOrder StateOrder => _stateOrder;
        [Tooltip("The order in which clients run states. Future favors performance and does not depend upon reconciles, while Past favors accuracy but clients must reconcile every tick.")]
        [SerializeField]
        private ReplicateStateOrder _stateOrder = ReplicateStateOrder.Appended;
        /// <summary>
        /// True if StateOrder is set to future.
        /// </summary>
        internal bool IsAppendedStateOrder => _stateOrder == ReplicateStateOrder.Appended;

        /// <summary>
        /// Sets the current ReplicateStateOrder. This may be changed at runtime.
        /// Changing this value only affects the client which it is changed on.
        /// </summary>
        /// <param name = "stateOrder"></param>
        public void SetStateOrder(ReplicateStateOrder stateOrder)
        {
            // Server doesn't use state order, exit early if server.
            if (_networkManager.IsServerStarted)
                return;
            // Same as before, do nothing.
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
        /// True to drop replicates from clients which are being received excessively. This can help with attacks but may cause client to temporarily desynchronize during connectivity issues.
        /// When false the server will hold at most up to 3 seconds worth of replicates, consuming multiple per tick to clear out the buffer quicker. This is good to ensure all inputs are executed but potentially could allow speed hacking.
        /// </summary>
        internal bool DropExcessiveReplicates => _dropExcessiveReplicates;
        [Tooltip("True to drop replicates from clients which are being received excessively. This can help with attacks but may cause client to temporarily desynchronize during connectivity issues. When false the server will hold at most up to 3 seconds worth of replicates, consuming multiple per tick to clear out the buffer quicker. This is good to ensure all inputs are executed but potentially could allow speed hacking.")]
        [SerializeField]
        private bool _dropExcessiveReplicates = true;
        /// <summary>
        /// No more than this value of replicates should be stored as a buffer.
        /// </summary>
        internal ushort MaximumPastReplicates => (ushort)(_networkManager.TimeManager.TickRate * 5);
        [Tooltip("Maximum number of replicates a server can queue per object. Higher values will reduce the chance of dropped input when the client's connection is unstable, but will potentially add latency to the client's object both on the server and client.")]
        [SerializeField]
        private byte _maximumServerReplicates = 15;

        /// <summary>
        /// Sets the maximum number of replicates a server can queue per object.
        /// </summary>
        public void SetMaximumServerReplicates(byte value) => _maximumServerReplicates = (byte)Mathf.Clamp(value, MINIMUM_REPLICATE_QUEUE_SIZE, MAXIMUM_REPLICATE_QUEUE_SIZE);

        /// <summary>
        /// Maximum number of replicates a server can queue per object. Higher values will reduce the chance of dropped input when the client's connection is unstable, but will potentially add latency to the client's object both on the server and client.
        /// </summary>
        public byte GetMaximumServerReplicates() => _maximumServerReplicates;

        /// <summary>
        /// Number of past inputs to send, which is also the number of times to resend final datas.
        /// </summary>
        internal byte RedundancyCount => (byte)(_stateInterpolation + 1);
        #endregion

        #region Private.
        /// <summary>
        /// Ticks for the last state packet to run.
        /// </summary>
        private StatePacketTick _lastStatePacketTick = new();
        /// <summary>
        /// Current reconcile state to use.
        /// </summary>
        // private StatePacket _reconcileState;
        private Queue<StatePacket> _reconcileStates = new();
        /// <summary>
        /// Look up to find states by their tick.
        /// Key: client LocalTick on the state.
        /// Value: StatePacket stored.
        /// </summary>
        private Dictionary<uint, StatePacket> _stateLookups = new();
        /// <summary>
        /// Last ordered tick read for a reconcile state.
        /// </summary>
        private uint _lastOrderedReadReconcileTick;
        /// <summary>
        /// </summary>
        private NetworkTrafficStatistics _networkTrafficStatistics;
        /// <summary>
        /// 
        /// </summary>
        private ClientReconcileThrottler _clientReconcileThrottler = new();
        /// <summary>
        /// True if the client-side had subscribed to the TimeManager.
        /// </summary>
        private bool _clientSubscribedToTimeManager;
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
        private const byte MINIMUM_REPLICATE_QUEUE_SIZE = MINIMUM_PAST_INPUTS + 1;
        /// <summary>
        /// Maxmimum amount of replicate queue size.
        /// </summary>
        private const byte MAXIMUM_REPLICATE_QUEUE_SIZE = byte.MaxValue;
        /// <summary>
        /// Recommended state interpolation value when using appended state order.
        /// </summary>
        internal const int MINIMUM_APPENDED_INTERPOLATION_RECOMMENDATION = 2;
        /// <summary>
        /// Recommended state interpolation value when using inserted state order.
        /// </summary>
        internal const int MINIMUM_INSERTED_INTERPOLATION_RECOMMENDATION = 1;
        /// <summary>
        /// Message when state interpolation is 0.
        /// </summary>
        internal static readonly string ZERO_STATE_INTERPOLATION_MESSAGE = $"When interpolation is 0 the chances of de-synchronizations on non-owned objects is increased drastically.";
        /// <summary>
        /// Message when state interpolation is less than ideal for appended state order.
        /// </summary>
        internal static readonly string LESS_THAN_MINIMUM_APPENDED_MESSAGE = $"When using Appended StateOrder and an interpolation less than {MINIMUM_APPENDED_INTERPOLATION_RECOMMENDATION} the chances of de-synchronizations on non-owned objects is increased.";
        /// <summary>
        /// Message when state interpolation is less than ideal for inserted state order.
        /// </summary>
        internal static readonly string LESS_THAN_MINIMUM_INSERTED_MESSAGE = $"When using Inserted StateOrder and an interpolation less than {MINIMUM_INSERTED_INTERPOLATION_RECOMMENDATION} the chances of de-synchronizations on non-owned objects is increased.";
        #endregion

        internal void InitializeOnce(NetworkManager manager)
        {
            _networkManager = manager;
            manager.StatisticsManager.TryGetNetworkTrafficStatistics(out _networkTrafficStatistics);

            ValidateClampInterpolation();
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        }

        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            _clientReconcileThrottler.ResetState();
            _lastOrderedReadReconcileTick = 0;

            //If state is started.
            if (obj.ConnectionState == LocalConnectionState.Started)
                SubscribeToTimeManager(subscribe: true, asServer: false);
            else
                SubscribeToTimeManager(subscribe: false, asServer: false);
        }

        /// <summary>
        /// Amount to reserve for the header of a state update.
        /// </summary>
        internal const int STATE_HEADER_RESERVE_LENGTH = TransportManager.PACKETID_LENGTH + TransportManager.UNPACKED_TICK_LENGTH + TransportManager.UNPACKED_SIZE_LENGTH;

        /// <summary>
        /// Clamps queued inputs to a valid value.
        /// </summary>
        private void ValidateClampInterpolation()
        {
            ushort startingValue = _stateInterpolation;
            // Check for setting if dropping.
            if (_dropExcessiveReplicates && _stateInterpolation > _maximumServerReplicates)
                _stateInterpolation = (byte)(_maximumServerReplicates - 1);

            // If changed.
            if (_stateInterpolation != startingValue)
                _networkManager.Log($"Interpolation has been set to {_stateInterpolation}.");

            // Check to warn if low value.
            if (_stateInterpolation == 0)
                _networkManager.LogWarning(ZERO_STATE_INTERPOLATION_MESSAGE);
            else if (_stateOrder == ReplicateStateOrder.Appended && _stateInterpolation < MINIMUM_APPENDED_INTERPOLATION_RECOMMENDATION)
                _networkManager.LogWarning(LESS_THAN_MINIMUM_APPENDED_MESSAGE);
            else if (_stateOrder == ReplicateStateOrder.Inserted && _stateInterpolation < MINIMUM_INSERTED_INTERPOLATION_RECOMMENDATION)
                _networkManager.LogWarning(LESS_THAN_MINIMUM_INSERTED_MESSAGE);
        }

        /// <summary>
        /// Changes subscription to TimeManager.
        /// </summary>
        private void SubscribeToTimeManager(bool subscribe, bool asServer)
        {
            if (asServer)
                return;
            if (_networkManager == null)
                return;

            if (subscribe == _clientSubscribedToTimeManager)
                return;

            _clientSubscribedToTimeManager = subscribe;

            if (subscribe)
                _networkManager.TimeManager.OnLateUpdate += TimeManager_OnLateUpdate;
            else
                _networkManager.TimeManager.OnLateUpdate -= TimeManager_OnLateUpdate;
        }

        /// <summary>
        /// Called when late update fires on the TimeManager.
        /// </summary>
        private void TimeManager_OnLateUpdate()
        {
            /* Do not throttle nor count frames if scene manager has performed actions recently. */
            if (_networkManager.SceneManager.HasProcessedScenesRecently(timeFrame: 2f)) 
            {
                _clientReconcileThrottler.ResetState();
                return;
            }
            
            if (_reduceReconcilesWithFramerate)
                _clientReconcileThrottler.AddFrame(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Returns client or server state tick for the current reconcile.
        /// </summary>
        /// <param name = "clientTick">True to return client state tick, false for servers.</param>
        /// <returns></returns>
        public uint GetReconcileStateTick(bool clientTick) => clientTick ? ClientStateTick : ServerStateTick;

        /// <summary>
        /// Reconciles to received states.
        /// </summary>
        internal void ReconcileToStates()
        {
            if (!_networkManager.IsClientStarted)
                return;

            // Creates a local state update if one is not available in reconcile states.
            //   CreateLocalStateUpdate();

            // If there are no states then guestimate the next state.
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
                int maxIterations = _reconcileStates.Count > stateInterpolation + 1 ? 2 : 1;
                // At most 2 iterations.
                if (iterations > maxIterations)
                    return;

                StatePacket sp;
                if (!ConditionsMet(_reconcileStates.Peek()))
                    return;
                else
                    sp = _reconcileStates.Dequeue();

                // Condition met. See if the next one matches condition, if so drop current.
                // Returns if a state has it's conditions met.
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
                    uint varianceAllowance = IsAppendedStateOrder ? (uint)2 : (uint)0;
                    uint serverTickDifferenceRequirement = varianceAllowance + stateInterpolation;

                    bool serverPass = spChecked.ServerTick < estimatedLastRemoteTick - serverTickDifferenceRequirement;
                    bool clientPass = spChecked.ClientTick < localTick - stateInterpolation;

                    return serverPass && clientPass;
                }

                bool dropReconcile = false;
                uint clientTick = sp.ClientTick;
                uint serverTick = sp.ServerTick;

                //Check to throttle reconciles.
                if (_reduceReconcilesWithFramerate && _clientReconcileThrottler.TryReconcile(_minimumClientReconcileFramerate))
                    dropReconcile = true;

                if (!dropReconcile)
                {
                    IsReconciling = true;
                    _lastStatePacketTick.Update(clientTick, serverTick);

                    ClientStateTick = clientTick;
                    /* This is the tick which the reconcile is for.
                     * Since reconciles are performed after replicate, if
                     * the replicate was on tick 100 then this reconcile is the state
                     * on tick 100, after the replicate is performed. */
                    ServerStateTick = serverTick;

                    // Have the reader get processed.
                    foreach (StatePacket.IncomingData item in sp.Datas)
                    {
                        // // If data isn't set skip it. This can be true if a locally generated state packet.
                        // if (item.Data.Array == null)
                        //     continue;

                        PooledReader reader = ReaderPool.Retrieve(item.Data, _networkManager, Reader.DataSource.Server);
                        _networkManager.ClientManager.ParseReader(reader, item.Channel);
                        ReaderPool.Store(reader);
                    }

                    bool timeManagerPhysics = tm.PhysicsMode == PhysicsMode.TimeManager;
                    float tickDelta = (float)tm.TickDelta * _networkManager.TimeManager.GetPhysicsTimeScale();

                    OnPreReconcile?.Invoke(ClientStateTick, ServerStateTick);
                    OnReconcile?.Invoke(ClientStateTick, ServerStateTick);

                    if (timeManagerPhysics)
                    {
                        OnPrePhysicsTransformSync?.Invoke(ClientStateTick, ServerStateTick);
                        Physics.SyncTransforms();
                        Physics2D.SyncTransforms();
                        OnPostPhysicsTransformSync?.Invoke(ClientStateTick, ServerStateTick);
                    }

                    OnPostReconcileSyncTransforms?.Invoke(ClientStateTick, ServerStateTick);
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
                        if (timeManagerPhysics && tickDelta > 0f)
                        {
                            Physics.Simulate(tickDelta);
                            Physics2D.Simulate(tickDelta);
                        }
                        OnPostReplicateReplay?.Invoke(ClientReplayTick, ServerReplayTick);
                        ClientReplayTick++;
                        ServerReplayTick++;
                    }

                    OnPostReconcile?.Invoke(ClientStateTick, ServerStateTick);

                    // ClientStateTick = TimeManager.UNSET_TICK;
                    // ServerStateTick = TimeManager.UNSET_TICK;
                    ClientReplayTick = TimeManager.UNSET_TICK;
                    ServerReplayTick = TimeManager.UNSET_TICK;
                    IsReconciling = false;
                }

                DisposeOfStatePacket(sp);
            }
        }

        /// <summary>
        /// Gets the reconcile tick to use when generating a local reconcile.
        /// </summary>
        /// <returns></returns>
        internal uint GetCreateReconcileTick(bool isOwner)
        {
            uint localTick = _networkManager.TimeManager.LocalTick;

            // Client uses current localTick if owner.
            if (isOwner)
                return localTick;

            // ClientStateTick has never been set, might happen when just connecting. Cannot get tick.
            if (ClientStateTick == TimeManager.UNSET_TICK)
                return TimeManager.UNSET_TICK;

            long tickDifference = (long)(localTick - ClientStateTick);

            // Should not be possible given state tick is always behind.
            if (tickDifference < 0)
                tickDifference = 0;

            return ServerStateTick + (uint)tickDifference;
        }

        /// <summary>
        /// Sends written states for clients.
        /// </summary>
        internal void SendStateUpdate()
        {
            byte stateInterpolation = StateInterpolation;
            TransportManager tm = _networkManager.TransportManager;

            int headersWritten = 0;

            foreach (NetworkConnection nc in _networkManager.ServerManager.Clients.Values)
            {
                uint lastReplicateTick;
                // If client has performed a replicate recently.
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
                    uint interpolationDifference = (uint)stateInterpolation * 2;
                    if (ncLocalTick < interpolationDifference)
                        ncLocalTick = 0;

                    lastReplicateTick = ncLocalTick;
                }

                foreach (PooledWriter writer in nc.PredictionStateWriters)
                {
                    headersWritten++;

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
                    int dataLength = segment.Count - STATE_HEADER_RESERVE_LENGTH;
                    // Write length.
                    writer.WriteInt32Unpacked(dataLength);
                    // Channel is defaulted to unreliable.
                    Channel channel = Channel.Unreliable;
                    // If a single state exceeds MTU it must be sent on reliable. This is extremely unlikely.
                    _networkManager.TransportManager.CheckSetReliableChannel(segment.Count, ref channel);
                    tm.SendToClient((byte)channel, segment, nc, splitLargeMessages: true);
                }

                nc.StorePredictionStateWriters();
            }

            #if DEVELOPMENT && !UNITY_SERVER
            if (_networkTrafficStatistics != null)
            {
                int written = STATE_HEADER_RESERVE_LENGTH * headersWritten;
                _networkTrafficStatistics.AddOutboundPacketIdData(PacketId.StateUpdate, string.Empty, written, gameObject: null, asServer: true);
            }
            #endif
        }

        /// <summary>
        /// Parses a received state update.
        /// </summary>
        internal void ParseStateUpdate(PooledReader reader, Channel channel)
        {
            uint lastRemoteTick = _networkManager.TimeManager.LastPacketTick.LastRemoteTick;
            // If server or state is older than another received state.
            if (_networkManager.IsServerStarted || lastRemoteTick < _lastOrderedReadReconcileTick)
            {
                /* If the server is receiving a state update it can
                 * simply discard the data since the server will never
                 * need to reset states. This can occur on the clientHost
                 * side. */
                reader.ReadTickUnpacked();
                int payloadLength = reader.ReadInt32Unpacked();
                reader.Skip(payloadLength);
            }
            else
            {
                _lastOrderedReadReconcileTick = lastRemoteTick;

                RemoveExcessiveStates();

                // LocalTick of this client the state is for.
                uint clientTick = reader.ReadTickUnpacked();
                // Length of packet.
                int payloadLength = reader.ReadInt32Unpacked();
                // Read data into array.
                byte[] arr = ByteArrayPool.Retrieve(payloadLength);
                reader.ReadUInt8Array(ref arr, payloadLength);
                // Make segment and store into states.
                ArraySegment<byte> segment = new(arr, 0, payloadLength);

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

            #if DEVELOPMENT && !UNITY_SERVER
            if (_networkTrafficStatistics != null)
                _networkTrafficStatistics.AddInboundPacketIdData(PacketId.StateUpdate, string.Empty, STATE_HEADER_RESERVE_LENGTH, gameObject: null, asServer: false);
            #endif
        }
        //
        // /// <summary>
        // /// Creates a local statePacket with no data other than ticks.
        // /// </summary>
        // internal void CreateLocalStateUpdate()
        // {
        //     // Only to be called when there are no reconcile states available.
        //     if (_reconcileStates.Count > 0)
        //         return;
        //     if (_networkManager.IsServerStarted)
        //         return;
        //     // Not yet received first state, cannot apply tick.
        //     if (_lastStatePacketTick.IsUnset)
        //         return;
        //
        //     _lastStatePacketTick.AddTick(1);
        //
        //     /* Update last read as well. If we've made it this far we won't be caring about states before this
        //      * even if they come in late. */
        //     _lastOrderedReadReconcileTick = _lastStatePacketTick.Server;
        //
        //     StatePacket sp = ResettableObjectCaches<StatePacket>.Retrieve();
        //     // Channel does not matter; it's only used to determine how data is parsed, data we don't have.
        //     sp.Update(default, _lastStatePacketTick.Client, _lastStatePacketTick.Server, Channel.Unreliable);
        //     _reconcileStates.Enqueue(sp);
        // }

        /// <summary>
        /// Removes excessively stored state packets.
        /// </summary>
        private void RemoveExcessiveStates()
        {
            /* There should never really be more than queuedInputs so set
             * a limit a little beyond to prevent reconciles from building up.
             * This is more of a last result if something went terribly
             * wrong with the network. */
            int adjustedStateInterpolation = StateInterpolation * 4 + 2;
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
            ValidateClampInterpolation();
        }

        #endif
    }
}