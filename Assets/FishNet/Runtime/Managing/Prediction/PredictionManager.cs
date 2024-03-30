using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static FishNet.Managing.Predicting.PredictionManager;
using UnityScene = UnityEngine.SceneManagement.Scene;


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
#if !PREDICTION_V2
        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        public event Action<NetworkBehaviour> OnPreReconcile;
        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        public event Action<NetworkBehaviour> OnPostReconcile;
#else
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
        
#endif
#if !PREDICTION_V2
        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        public event Action<uint, PhysicsScene, PhysicsScene2D> OnPreReplicateReplay;
        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        public event Action<uint, PhysicsScene, PhysicsScene2D> OnPostReplicateReplay;
#else
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
#endif
#if !PREDICTION_V2
        /// <summary>
        /// Called before the server sends a reconcile.
        /// </summary>
        public event Action<NetworkBehaviour> OnPreServerReconcile;
        /// <summary>
        /// Called after the server sends a reconcile.
        /// </summary>
        public event Action<NetworkBehaviour> OnPostServerReconcile;
        /// <summary>
        /// Last tick any object reconciled.
        /// </summary>
        public uint LastReconcileTick { get; internal set; }
        /// <summary>
        /// Last tick any object replicated.
        /// </summary>
        public uint LastReplicateTick { get; internal set; }
        /// <summary>
        /// True if rigidbodies are being predicted.
        /// </summary>
        internal bool UsingRigidbodies => (_rigidbodies.Count > 0);
        /// <summary>
        /// True if prediction is replaying.
        /// </summary>
        public bool IsReplaying() => (_replayingScenes.Count > 0); 
        /// <summary>
        /// Returns if scene is replaying.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public bool IsReplaying(UnityScene scene) => _replayingScenes.Contains(scene);
#else
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
#endif
#if !PREDICTION_V2

#endif
        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Number of inputs to keep in queue for server and clients. " +
            "Higher values will increase the likeliness of continous user created data to arrive successfully. " +
            "Lower values will increase processing rate of received replicates. +" +
            "This value cannot be higher than MaximumServerReplicates.")]
        [Range(0, 15)]
        [SerializeField]
        private byte _queuedInputs = 1;
#if PREDICTION_V2
        /// <summary>
        /// Number of inputs to keep in queue for server and clients.
        /// Higher values will increase the likeliness of continous user created data to arrive successfully.
        /// Lower values will increase processing rate of received replicates.
        /// This value cannot be higher than MaximumServerReplicates.
        /// </summary>
        //TODO: this is 0 until the rework on it is completed. 
        public byte QueuedInputs => 0;// _queuedInputs;
#else
        /// <summary>
        /// Number of inputs to keep in queue should the server miss receiving an input update from the client.
        /// Higher values will increase the likeliness of the server always having input from the client while lower values will allow the client input to run on the server faster.
        /// This value cannot be higher than MaximumServerReplicates.
        /// </summary>
        public ushort QueuedInputs => (ushort)(_queuedInputs + 1);
#endif
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
#if !PREDICTION_V2
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of excessive replicates which can be consumed per tick. Consumption count will scale up to this value automatically.")]
        [SerializeField]
        private byte _maximumConsumeCount = 4;
        /// <summary>
        /// Maximum number of excessive replicates which can be consumed per tick. Consumption count will scale up to this value automatically.
        /// </summary>
        internal byte MaximumReplicateConsumeCount => _maximumConsumeCount;
#endif
        /// <summary>
        /// Clients should store no more than 2 seconds worth of replicates.
        /// </summary>
        internal ushort MaximumClientReplicates => (ushort)(_networkManager.TimeManager.TickRate * 5);
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of past inputs which may send.")]
        [Range(MINIMUM_PAST_INPUTS, MAXIMUM_PAST_INPUTS)]
        [SerializeField]
        private byte _redundancyCount = 2;
        /// <summary>
        /// Maximum number of past inputs which may send and resend redundancy.
        /// </summary>
        internal byte RedundancyCount => _redundancyCount;
        /// <summary>
        /// True to allow clients to use predicted spawning. While true, each NetworkObject prefab you wish to predicted spawn must be marked as to allow this feature.
        /// </summary>
        internal bool GetAllowPredictedSpawning() => _allowPredictedSpawning;
        [Tooltip("True to allow clients to use predicted spawning and despawning. While true, each NetworkObject prefab you wish to predicted spawn must be marked as to allow this feature.")]
        [SerializeField]
        private bool _allowPredictedSpawning = false;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of Ids to reserve on clients for predicted spawning. Higher values will allow clients to send more predicted spawns per second but may reduce availability of ObjectIds with high player counts.")]
        [Range(1, 100)]
        [SerializeField]
        private byte _reservedObjectIds = 15;
        /// <summary>
        /// Maximum number of Ids to reserve on clients for predicted spawning. Higher values will allow clients to send more predicted spawns per second but may reduce availability of ObjectIds with high player counts.
        /// </summary>
        /// <returns></returns>
        internal byte GetReservedObjectIds() => _reservedObjectIds;
        #endregion

        #region Private.
#if !PREDICTION_V2
        /// <summary>
        /// Number of active predicted rigidbodies.
        /// </summary>
        [System.NonSerialized]
        private HashSet<UnityEngine.Component> _rigidbodies = new HashSet<UnityEngine.Component>();
        /// <summary>
        /// Cache to remove null entries from _rigidbodies.
        /// </summary>
        [System.NonSerialized]
        private HashSet<UnityEngine.Component> _componentCache = new HashSet<UnityEngine.Component>();
        /// <summary>
        /// Scenes which are currently replaying prediction.
        /// </summary>
        private HashSet<UnityScene> _replayingScenes = new HashSet<UnityScene>(new SceneHandleEqualityComparer());
#else
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
        /// Last ordered tick read for a reconcile state.
        /// </summary>
        private uint _lastOrderedReadReconcileTick;
#endif
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

#if !PREDICTION_V2
        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= SceneManager_sceneUnloaded;
        }

        internal void InitializeOnce(NetworkManager manager)
        {
            _networkManager = manager;
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        }
#endif

#if PREDICTION_V2
        internal void InitializeOnce(NetworkManager manager)
        {
            _networkManager = manager;
            ClampQueuedInputs();
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            if (obj.ConnectionState != LocalConnectionState.Started)
                _replayingScenes.Clear();
        }
#else
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            _droppedReconcilesCount = 0;

        }
#endif


        /// <summary>
        /// Called before and after server sends a reconcile.
        /// </summary>
        /// <param name="before">True if before the reconcile is sent.</param>
        internal void InvokeServerReconcile(NetworkBehaviour caller, bool before)
        {
#if !PREDICTION_V2
            if (before)
                OnPreServerReconcile?.Invoke(caller);
            else
                OnPostServerReconcile?.Invoke(caller);
#endif
        }

#if !PREDICTION_V2
        /// <summary>
        /// Increases Rigidbodies count by 1.
        /// </summary>
        [APIExclude]
        public void AddRigidbodyCount(UnityEngine.Component c)
        {
            _rigidbodies.Add(c);
        }

        /// <summary>
        /// Dencreases Rigidbodies count by 1.
        /// </summary>
        [APIExclude]
        public void RemoveRigidbodyCount(UnityEngine.Component c)
        {
            bool removed = _rigidbodies.Remove(c);
            /* If remove failed the rigidbodies may need to be rebuild.
             * This might happen when an object is destroyed as
             * the referenced is passed. Could be any number of things
             * but it seems to occur frequently enough in Unity,
             * especially when testing in editor.
             * 
             * This operation is not ideal in the hot path but
             * the odds of it happening are pretty slim and
             * it ensures stability against user error. */
            if (!removed)
            {
                //Cannt remove null entries from a hashset so have to rebuild.
                _componentCache.Clear();
                foreach (UnityEngine.Component item in _rigidbodies)
                {
                    if (item != null)
                        _componentCache.Add(item);
                }

                //Apply to rigidbodies.
                _rigidbodies.Clear();
                foreach (UnityEngine.Component item in _componentCache)
                    _rigidbodies.Add(item);
            }
        }

        /// <summary>
        /// Invokes OnPre/PostReconcile events.
        /// Internal use.
        /// </summary>
        [APIExclude]
        [MakePublic]
        internal void InvokeOnReconcile(NetworkBehaviour nb, bool before)
        {
            nb.IsBehaviourReconciling = before;
            if (before)
                OnPreReconcile?.Invoke(nb);
            else
                OnPostReconcile?.Invoke(nb);
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Invokes OnReplicateReplay.
        /// Internal use.
        /// </summary>
        [APIExclude]
        internal void InvokeOnReplicateReplay(UnityScene scene, uint tick, PhysicsScene ps, PhysicsScene2D ps2d, bool before)
        {
            if (before)
            {
                _replayingScenes.Add(scene);
                OnPreReplicateReplay?.Invoke(tick, ps, ps2d);
            }
            else
            {
                _replayingScenes.Remove(scene);
                OnPostReplicateReplay?.Invoke(tick, ps, ps2d);
            }
        }

        /// <summary>
        /// Called when a scene unloads.
        /// </summary>
        /// <param name="arg0"></param>
        private void SceneManager_sceneUnloaded(UnityScene s)
        {
            _replayingScenes.Remove(s);
        }
#endif

#if PREDICTION_V2
        /// <summary>
        /// Amount to reserve for the header of a state update.
        /// 2 PacketId.
        /// 4 Last replicate tick run for connection.
        /// 4 Length unpacked.
        /// </summary>
        internal const int STATE_HEADER_RESERVE_COUNT = 10;

        /// <summary>
        /// Clamps queued inputs to a valid value.
        /// </summary>
        private void ClampQueuedInputs()
        {
            ushort startingValue = _queuedInputs;
            //Check for setting if dropping.
            if (_dropExcessiveReplicates && _queuedInputs > _maximumServerReplicates)
                _queuedInputs = (byte)(_maximumServerReplicates - 1);

            //If changed.
            if (_queuedInputs != startingValue)
                _networkManager.Log($"QueuedInputs has been set to {_queuedInputs}.");
        }

        public struct StatePacket
        {
            public ArraySegment<byte> Data;
            public uint ClientTick;
            public uint ServerTick;
            public bool IsValid => (Data.Array != null);

            public StatePacket(ArraySegment<byte> data, uint clientTick, uint serverTick)
            {
                Data = data;
                ServerTick = serverTick;
                ClientTick = clientTick;
            }

            public void ResetState()
            {
                if (!IsValid)
                    return;

                ByteArrayPool.Store(Data.Array);
                Data = default;
            }
        }

        internal float SlowDownTime;
        private int _timesOver = 0;
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
            //NOTESSTART
            /* Don't run a reconcile unless it's possible for ticks queued
             * that tick to be run already. Otherwise you are not replaying inputs
             * at all, just snapping to corrections. This means states which arrive late or out of order
             * will be ignored since they're before the reconcile, which means important actions
             * could have gone missed.
             * 
             * A system which synchronized all current states rather than what's only needed to correct
             * the inputs would likely solve this. */
            //NOTESEND

            /* Only use the latest reconcile which passes the conditions to run.
             * This will drop any excessive reconciles which built up from latency. */
            StatePacket sp = default;
            /* If here then 'peeked' has met conditions.
             * Check if the next state also meets, if so then
             * skip ahead to the next state. */
            while (_reconcileStates.Count > 0)
            {
                //If next matches then set peeked to new.
                if (ConditionsMet(_reconcileStates.Peek()))
                {
                    //Since this is being replaced, reset state first.
                    if (sp.IsValid)
                        sp.ResetState();
                    sp = _reconcileStates.Dequeue();
                    break;
                }
                /* Conditions are not met on the next one, exit loop.
                 * This will use the latest peeked. */
                else
                {
                    break;
                }

                //Condition met. See if the next one matches condition, if so drop current.
                //Returns if a state has it's conditions met.
                bool ConditionsMet(StatePacket spChecked)
                {
                    return (spChecked.ServerTick <= (estimatedLastRemoteTick - QueuedInputs - RedundancyCount - 1) && spChecked.ClientTick < (localTick - QueuedInputs));
                }
            }
            //If state is not valid then it was never set, thus condition is not met.
            if (!sp.IsValid)
                return;

            //StatePacket sp = _reconcileStates.Dequeue();
            PooledReader reader = ReaderPool.Retrieve(sp.Data, _networkManager, Reader.DataSource.Server);

            bool dropReconcile = false;

            uint clientTick = sp.ClientTick;
            uint serverTick = sp.ServerTick;
            uint ticksDifference = (localTick - clientTick);
            //Target ticks are based on QueuedInputs, redundancy count, and latency. An extra bit is added as a buffer for variance.
            uint varianceAllowance = tm.TimeToTicks(0.2f, TickRounding.RoundUp);
            uint targetTicks = (varianceAllowance + (uint)QueuedInputs + (uint)RedundancyCount + tm.TimeToTicks((double)((double)tm.RoundTripTime / 1000d), TickRounding.RoundDown));
            long ticksOverTarget = (long)ticksDifference - (long)targetTicks;
            //ReduceClientTiming = (ticksOverTarget > 0);
            /* If the reconcile is behind more ticks than hoped then slow
             * down the client simulation so it ticks very slightly
             * slower allowing fewer replays. This typically is only required after
             * the player encounters a sudden ping drop, such as a spike in latency,
             * then ping returns to norrmal.  */
            if (ticksOverTarget > 0)
            {
                _timesOver++;
                if (_timesOver >= tm.TimeToTicks(0.5d))
                {
                    SlowDownTime = Time.unscaledTime;
                    ReduceClientTiming = true;
                    _timesOver = 3;
                }
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
            }
            //No reason to believe client is struggling, allow reconcile.
            else
            {
                _timesOver--;
                if (_timesOver < 0)
                {
                    ReduceClientTiming = false;
                    _timesOver = 0;
                }
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
                _networkManager.ClientManager.ParseReader(reader, Channel.Reliable);

                bool timeManagerPhysics = (tm.PhysicsMode == PhysicsMode.TimeManager);
                float tickDelta = (float)tm.TickDelta;

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
                ClientReplayTick = ClientStateTick;
                ServerReplayTick = ServerStateTick;

                int replays = 0;
                /* Only replay up to this tick excluding queuedInputs.
                 * This will prevent the client from replaying into
                 * it's authorative/owned inputs which have not run
                 * yet.
                 * 
                 * An additional value is subtracted to prevent
                 * client from running 1 local tick into the future
                 * since the OnTick has not run yet. */
                while (ClientReplayTick < localTick - 1)
                {
                    replays++;
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

            sp.ResetState();
            ReaderPool.Store(reader);
        }
        /// <summary>
        /// Sends written states for clients.
        /// </summary>
        internal void SendStateUpdate()
        {
            TransportManager tm = _networkManager.TransportManager;
            foreach (NetworkConnection nc in _networkManager.ServerManager.Clients.Values)
            {
                uint lastReplicateTick;
                //If client has performed a replicate.
                if (!nc.ReplicateTick.IsUnset)
                {
                    /* If it's been longer than queued inputs since
                     * server has received a replicate then
                     * use estimated value. Otherwise use LastRemoteTick. */
                    if (nc.ReplicateTick.LocalTickDifference(_networkManager.TimeManager) > QueuedInputs)
                        lastReplicateTick = nc.ReplicateTick.Value();
                    else
                        lastReplicateTick = nc.ReplicateTick.LastRemoteTick;
                }
                /* If not then use what is estimated to be the clients
                 * current tick along with desired prediction queue count.
                 * This should be just about the same as if the client used replicate,
                 * but even if it's not it doesn't matter because the client
                 * isn't replicating himself, just reconciling and replaying other objects. */
                else
                {
                    lastReplicateTick = (nc.PacketTick.Value() + QueuedInputs);
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
                    writer.WritePacketId(PacketId.StateUpdate);
                    writer.WriteTickUnpacked(lastReplicateTick);
                    /* Send the full length of the writer excluding
                     * the reserve count of the header. The header reserve
                     * count will always be the same so that can be parsed
                     * off immediately upon receiving. */
                    int dataLength = (segment.Count - STATE_HEADER_RESERVE_COUNT);
                    //Write length.
                    writer.WriteInt32(dataLength, AutoPackType.Unpacked);
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
        internal void ParseStateUpdate(PooledReader reader)
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
                int length = reader.ReadInt32(AutoPackType.Unpacked);
                reader.Skip(length);
            }
            else
            {
                _lastOrderedReadReconcileTick = lastRemoteTick;

                /* There should never really be more than queuedInputs so set
                 * a limit a little beyond to prevent reconciles from building up. 
                 * This is more of a last result if something went terribly
                 * wrong with the network. */
                int maxAllowedStates = Mathf.Max(QueuedInputs * 4, 4);
                while (_reconcileStates.Count > maxAllowedStates)
                {
                    StatePacket sp = _reconcileStates.Dequeue();
                    sp.ResetState();
                }

                //LocalTick of this client the state is for.
                uint clientTick = reader.ReadTickUnpacked();
                //Length of packet.
                int length = reader.ReadInt32(AutoPackType.Unpacked);
                //Read data into array.
                byte[] arr = ByteArrayPool.Retrieve(length);
                reader.ReadBytes(ref arr, length);
                //Make segment and store into states.
                ArraySegment<byte> segment = new ArraySegment<byte>(arr, 0, length);
                _reconcileStates.Enqueue(new StatePacket(segment, clientTick, lastRemoteTick));
            }
        }

#endif

#if PREDICTION_V2
#if UNITY_EDITOR
        private void OnValidate()
        {
            ClampQueuedInputs();
        }
#endif
#endif
    }

}