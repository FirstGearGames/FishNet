using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Extension;
using GameKit.Utilities;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using SystemStopwatch = System.Diagnostics.Stopwatch;

namespace FishNet.Managing.Timing
{

    /// <summary>
    /// Provides data and actions for network time and tick based systems.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/TimeManager")]
    public sealed partial class TimeManager : MonoBehaviour
    {
        #region Types.
        /// <summary>
        /// How networking timing is performed.
        /// </summary>
        private enum TimingType
        {
            /// <summary>
            /// Send and read data on tick.
            /// </summary>
            Tick = 0,
            /// <summary>
            /// Send and read data as soon as possible. This does not include built-in components, which will still run on tick.
            /// </summary>
            Variable = 1
        }
        private enum UpdateOrder : byte
        {
            BeforeTick = 0,
            AfterTick = 1,
        }
        #endregion

        #region Public.
        /// <summary>
        /// Called when the local clients ping is updated.
        /// </summary>
        public event Action<long> OnRoundTripTimeUpdated;
        /// <summary>
        /// Called right before a tick occurs, as well before data is read.
        /// </summary>
        public event Action OnPreTick;
        /// <summary>
        /// Called when a tick occurs.
        /// </summary>
        public event Action OnTick;
        /// <summary>
        /// When using TimeManager for physics timing, this is called immediately before physics simulation will occur for the tick.
        /// While using Unity for physics timing, this is called during FixedUpdate.
        /// This may be useful if you wish to run physics differently for stacked scenes.
        /// </summary>
        public event Action<float> OnPrePhysicsSimulation;
        /// <summary>
        /// When using TimeManager for physics timing, this is called immediately after the physics simulation has occured for the tick.
        /// While using Unity for physics timing, this is called during Update, only if a physics frame.
        /// This may be useful if you wish to run physics differently for stacked scenes.
        /// </summary>
        public event Action<float> OnPostPhysicsSimulation;
        /// <summary>
        /// Called after a tick occurs; physics would have simulated if using PhysicsMode.TimeManager.
        /// </summary>
        public event Action OnPostTick;
        /// <summary>
        /// Called when MonoBehaviours call Update.
        /// </summary>
        public event Action OnUpdate;
        /// <summary>
        /// Called when MonoBehaviours call LateUpdate.
        /// </summary>
        public event Action OnLateUpdate;
        /// <summary>
        /// Called when MonoBehaviours call FixedUpdate.
        /// </summary>
        public event Action OnFixedUpdate;
        /// <summary>
        /// RoundTripTime in milliseconds. This value includes latency from the tick rate.
        /// </summary>
        public long RoundTripTime { get; private set; }
        /// <summary>
        /// True if the number of frames per second are less than the number of expected ticks per second.
        /// </summary>
        internal bool LowFrameRate => ((Time.unscaledTime - _lastMultipleTicksTime) < 1f);
        /// <summary>
        /// Tick on the last received packet, be it from server or client.
        /// </summary>
        public uint LastPacketTick { get; private set; }
        /// <summary>
        /// Last packet tick which did not arrive out of order.
        /// </summary>
        internal uint LastOrderedPacketTick;
        /// <summary>
        /// Sets LastPacketTick and LastOrderedPacketTick.
        /// </summary>
        /// <param name="tick"></param>
        internal void SetLastPacketTick(uint tick)
        {
            if (tick > LastPacketTick)
                LastOrderedPacketTick = tick;

            LastPacketTick = tick;
        }
        /// <summary>
        /// Current approximate network tick as it is on server.
        /// When running as client only this is an approximation to what the server tick is.
        /// The value of this field may increase and decrease as timing adjusts.
        /// This value is reset upon disconnecting.
        /// Tick can be used to get the server time by using TicksToTime().
        /// Use LocalTick for values that only increase.
        /// </summary>
        public uint Tick { get; internal set; }
        /// <summary>
        /// A fixed deltaTime for TickRate.
        /// </summary>
        [HideInInspector]
        public double TickDelta { get; private set; }
        /// <summary>
        /// True if the TimeManager will or has ticked this frame.
        /// </summary>
        public bool FrameTicked { get; private set; }
        /// <summary>
        /// How long the local server has been connected.
        /// </summary>
        public float ServerUptime { get; private set; }
        /// <summary>
        /// How long the local client has been connected.
        /// </summary>
        public float ClientUptime { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// When to invoke OnUpdate and other Unity callbacks relayed by the TimeManager.
        /// </summary>
        [Tooltip("When to invoke OnUpdate and other Unity callbacks relayed by the TimeManager.")]
        [SerializeField]
        private UpdateOrder _updateOrder = UpdateOrder.BeforeTick;
        /// <summary>
        /// Timing for sending and receiving data.
        /// </summary>
        [Tooltip("Timing for sending and receiving data.")]
        [SerializeField]
        private TimingType _timingType = TimingType.Tick;
        /// <summary>
        /// While true clients may drop local ticks if their devices are unable to maintain the tick rate.
        /// This could result in a temporary desynchronization but will prevent the client falling further behind on ticks by repeatedly running the logic cycle multiple times per frame.
        /// </summary>
        [Tooltip("While true clients may drop local ticks if their devices are unable to maintain the tick rate. This could result in a temporary desynchronization but will prevent the client falling further behind on ticks by repeatedly running the logic cycle multiple times per frame.")]
        [SerializeField]
        private bool _allowTickDropping;
        /// <summary>
        /// Maximum number of ticks which may occur in a single frame before remainder are dropped for the frame.
        /// </summary>
        [Tooltip("Maximum number of ticks which may occur in a single frame before remainder are dropped for the frame.")]
        [Range(1, 25)]
        [SerializeField]
        private byte _maximumFrameTicks = 2;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How many times per second the server will simulate. This does not limit server frame rate.")]
        [Range(1, 240)]
        [SerializeField]
        private ushort _tickRate = 30;
        /// <summary>
        /// How many times per second the server will simulate. This does not limit server frame rate.
        /// </summary>
        public ushort TickRate { get => _tickRate; private set => _tickRate = value; }
        /// <summary>
        /// 
        /// </summary>        
        [Tooltip("How often in seconds to a connections ping. This is also responsible for approximating server tick. This value does not affect prediction.")]
        [Range(1, 15)]
        [SerializeField]
        private byte _pingInterval = 1;
        /// <summary>
        /// How often in seconds to a connections ping. This is also responsible for approximating server tick. This value does not affect prediction.
        /// </summary>
        public byte PingInterval => _pingInterval;
        ///// <summary>
        ///// How often in seconds to update prediction timing. Lower values will result in marginally more accurate timings at the cost of bandwidth.
        ///// </summary>        
        //[Tooltip("How often in seconds to update prediction timing. Lower values will result in marginally more accurate timings at the cost of bandwidth.")]
        //[Range(1, 15)]
        //[SerializeField]
        //private byte _timingInterval = 2;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How to perform physics.")]
        [SerializeField]
        private PhysicsMode _physicsMode = PhysicsMode.Unity;
        /// <summary>
        /// How to perform physics.
        /// </summary>
        public PhysicsMode PhysicsMode => _physicsMode;
        #endregion

        #region Private.
        /// <summary>
        /// Ticks that have passed on client since the last time server sent an UpdateTicksBroadcast.
        /// </summary>
        private uint _clientTicks = 0;
        /// <summary>
        /// Last Tick the server sent out UpdateTicksBroadcast.
        /// </summary>
        private uint _lastUpdateTicks = 0;
        /// <summary>
        /// 
        /// </summary>
        private uint _localTick;
        /// <summary>
        /// A tick that is not synchronized. This value will only increment. May be used for indexing or Ids with custom logic.
        /// When called on the server Tick is returned, otherwise LocalTick is returned.
        /// This value resets upon disconnecting.
        /// </summary>
        public uint LocalTick
        {
            get => (_networkManager.IsServer) ? Tick : _localTick;
            private set => _localTick = value;
        }
        /// <summary>
        /// Stopwatch used for pings.
        /// </summary>
        SystemStopwatch _pingStopwatch = new SystemStopwatch();
        /// <summary>
        /// Ticks passed since last ping.
        /// </summary>
        private uint _pingTicks;
        /// <summary>
        /// MovingAverage instance used to calculate mean ping.
        /// </summary>
        private MovingAverage _pingAverage = new MovingAverage(5);
        /// <summary>
        /// Accumulating frame time to determine when to increase tick.
        /// </summary>
        private double _elapsedTickTime;
        /// <summary>
        /// NetworkManager used with this.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Internal deltaTime for clients. Controlled by the server.
        /// </summary>
        private double _adjustedTickDelta;
        /// <summary>
        /// Range which client timing may reside within.
        /// </summary>
        private double[] _clientTimingRange;
        /// <summary>
        /// Last frame an iteration occurred for incoming.
        /// </summary>
        private int _lastIncomingIterationFrame = -1;
        /// <summary>
        /// True if client received Pong since last ping.
        /// </summary>
        private bool _receivedPong = true;
        /// <summary>
        /// Last unscaledTime multiple ticks occurred in a single frame.
        /// </summary>
        private float _lastMultipleTicksTime;
        /// <summary>
        /// Number of TimeManagers open which are using manual physics.
        /// </summary>
        private static uint _manualPhysics;
        /// <summary>
        /// Number of times the client had sent too fast in a row.
        /// </summary>
        private float _timingTooFastCount;
        /// <summary>
        /// True if FixedUpdate called this frame and using Unity physics mode.
        /// </summary>
        private bool _fixedUpdateTimeStep;
        #endregion

        #region Const.
        /// <summary>
        /// How often to send timing updates to clients.
        /// </summary>
        internal const float TIMING_INTERVAL = 1f;
        /// <summary>
        /// Value for a tick that is invalid.
        /// </summary>
        public const uint UNSET_TICK = 0;
        /// <summary>
        /// Maximum percentage timing may vary from TickDelta for clients.
        /// </summary>
        private const float CLIENT_TIMING_PERCENT_RANGE = 0.5f;
        /// <summary>
        /// Percentage of TickDelta client will adjust when needing to speed up.
        /// </summary>
        private const double CLIENT_SPEEDUP_VALUE = 0.035d;
        /// <summary>
        /// Percentage of TickDelta client will adjust when needing to slow down.
        /// </summary>
        private const double CLIENT_SLOWDOWN_VALUE = 0.02d;
        /// <summary>
        /// When steps to be sent to clients are equal to or higher than this value in either direction a reset steps will be sent.
        /// </summary>
        internal byte RESET_ADJUSTMENT_THRESHOLD => (byte)Mathf.Max(3, TickRate / 3);
        /// <summary>
        /// Playerprefs string to load and save user fixed time.
        /// </summary>
        private const string SAVED_FIXED_TIME_TEXT = "SavedFixedTimeFN";
        #endregion

#if UNITY_EDITOR
        private void OnDisable()
        {
            //If closing/stopping.
            if (ApplicationState.IsQuitting())
            {
                _manualPhysics = 0;
                UnsetSimulationSettings();
            }
            else if (PhysicsMode == PhysicsMode.TimeManager)
            {
                _manualPhysics = Math.Max(0, _manualPhysics - 1);
            }
        }
#endif

        /// <summary>
        /// Called when FixedUpdate ticks. This is called before any other script.
        /// </summary>
        internal void TickFixedUpdate()
        {
            OnFixedUpdate?.Invoke();
            /* Invoke onsimulation if using Unity time.
             * Otherwise let the tick cycling part invoke. */
            if (PhysicsMode == PhysicsMode.Unity)
            {
                /* If fixedUpdateTimeStep then that means
                 * FixedUpdate already called for this frame, which
                 * means a post physics should also be called.
                 * This can only happen if a FixedUpdate occurs
                 * multiple times per frame. */
                if (_fixedUpdateTimeStep)
                    OnPostPhysicsSimulation?.Invoke(Time.fixedDeltaTime);

                _fixedUpdateTimeStep = true;
                OnPrePhysicsSimulation?.Invoke(Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Called when Update ticks. This is called before any other script.
        /// </summary>
        internal void TickUpdate()
        {
            if (_networkManager.IsServer)
                ServerUptime += Time.deltaTime;
            if (_networkManager.IsClient)
                ClientUptime += Time.deltaTime;

            bool beforeTick = (_updateOrder == UpdateOrder.BeforeTick);
            if (beforeTick)
            {
                OnUpdate?.Invoke();
                MethodLogic();
            }
            else
            {
                MethodLogic();
                OnUpdate?.Invoke();
            }

            void MethodLogic()
            {
                IncreaseTick();
                /* Invoke onsimulation if using Unity time.
                * Otherwise let the tick cycling part invoke. */
                if (PhysicsMode == PhysicsMode.Unity && _fixedUpdateTimeStep)
                {
                    _fixedUpdateTimeStep = false;
                    OnPostPhysicsSimulation?.Invoke(Time.fixedDeltaTime);
                }
            }
        }

        /// <summary>
        /// Called when LateUpdate ticks. This is called after all other scripts.
        /// </summary>
        internal void TickLateUpdate()
        {
            OnLateUpdate?.Invoke();
        }


        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        internal void InitializeOnce_Internal(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            SetInitialValues();
            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;

            AddNetworkLoops();
        }

        /// <summary>
        /// Adds network loops to gameObject.
        /// </summary>
        private void AddNetworkLoops()
        {
            //Writer.
            if (!gameObject.TryGetComponent<NetworkWriterLoop>(out _))
                gameObject.AddComponent<NetworkWriterLoop>();
            //Reader.
            if (!gameObject.TryGetComponent<NetworkReaderLoop>(out _))
                gameObject.AddComponent<NetworkReaderLoop>();
        }


        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            if (obj.ConnectionState != LocalConnectionState.Started)
            {
                _pingStopwatch.Stop();
                ClientUptime = 0f;

                //Only reset ticks if also not server.
                if (!_networkManager.IsServer)
                {
                    LocalTick = 0;
                    Tick = 0;
                    SetTickRate(TickRate);
                    _timingTooFastCount = 0f;
                }
            }
            //Started.
            else
            {
                _pingStopwatch.Restart();
            }
        }

        /// <summary>
        /// Called after the local server connection state changes.
        /// </summary>
        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            //If no servers are running.
            if (!_networkManager.ServerManager.AnyServerStarted())
            {
                ServerUptime = 0f;
                Tick = 0;
            }
        }

        /// <summary>
        /// Sets values to use based on settings.
        /// </summary>
        private void SetInitialValues()
        {
            SetTickRate(TickRate);
            InitializePhysicsMode(PhysicsMode);
        }

        /// <summary>
        /// Sets simulation settings to Unity defaults.
        /// </summary>
        private void UnsetSimulationSettings()
        {
            SetAutomaticPhysicsSimulation(true);

            float simulationTime = PlayerPrefs.GetFloat(SAVED_FIXED_TIME_TEXT, float.MinValue);
            if (simulationTime != float.MinValue)
                Time.fixedDeltaTime = simulationTime;
        }

        /// <summary>
        /// Sets automatic physics simulation mode.
        /// </summary>
        /// <param name="automatic"></param>
        private void SetAutomaticPhysicsSimulation(bool automatic)
        {
#if UNITY_2022_1_OR_NEWER
            if (automatic)
            {
                Physics.simulationMode = SimulationMode.FixedUpdate;
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            }
            else
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics2D.simulationMode = SimulationMode2D.Script;
            }
#elif UNITY_2020_1_OR_NEWER
            Physics.autoSimulation = automatic;
            if (automatic)
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            else
                Physics2D.simulationMode = SimulationMode2D.Script;
#elif UNITY_2019_1_OR_NEWER
            Physics.autoSimulation = automatic;
            Physics2D.autoSimulation = automatic;
#endif
        }

        /// <summary>
        /// Initializes physics mode when starting.
        /// </summary>
        /// <param name="automatic"></param>
        private void InitializePhysicsMode(PhysicsMode mode)
        {
            //Disable.
            if (mode == PhysicsMode.Disabled)
            {
                SetPhysicsMode(mode);
            }
            //Do not automatically simulate.
            else if (mode == PhysicsMode.TimeManager)
            {
#if UNITY_EDITOR
                //Preserve user tick rate.
                PlayerPrefs.SetFloat(SAVED_FIXED_TIME_TEXT, Time.fixedDeltaTime);
                //Let the player know.
                if (Time.fixedDeltaTime != (float)TickDelta)
                    Debug.LogWarning("Time.fixedDeltaTime is being overriden with TimeManager.TickDelta");
#endif
                Time.fixedDeltaTime = (float)TickDelta;
                /* Only check this if network manager
                 * is not null. It would be null via
                 * OnValidate. */
                if (_networkManager != null)
                {
                    //If at least one time manager is already running manual physics.
                    if (_manualPhysics > 0)
                        _networkManager.LogError($"There are multiple TimeManagers instantiated which are using manual physics. Manual physics with multiple TimeManagers is not supported.");

                    _manualPhysics++;
                }

                SetPhysicsMode(mode);
            }
            //Automatically simulate.
            else
            {
#if UNITY_EDITOR
                float savedTime = PlayerPrefs.GetFloat(SAVED_FIXED_TIME_TEXT, float.MinValue);
                if (savedTime != float.MinValue && Time.fixedDeltaTime != savedTime)
                {
                    Debug.LogWarning("Time.fixedDeltaTime has been set back to user values.");
                    Time.fixedDeltaTime = savedTime;
                }

                PlayerPrefs.DeleteKey(SAVED_FIXED_TIME_TEXT);
#endif
                SetPhysicsMode(mode);
            }
        }

        /// <summary>
        /// Updates physics based on which physics mode to use.
        /// </summary>
        /// <param name="enabled"></param>
        public void SetPhysicsMode(PhysicsMode mode)
        {
            _physicsMode = mode;

            //Disable.
            if (mode == PhysicsMode.Disabled || mode == PhysicsMode.TimeManager)
                SetAutomaticPhysicsSimulation(false);
            //Automatically simulate.
            else
                SetAutomaticPhysicsSimulation(true);
        }

        #region PingPong.
        /// <summary>
        /// Modifies client ping based on LocalTick and clientTIck.
        /// </summary>
        /// <param name="clientTick"></param>
        internal void ModifyPing(uint clientTick)
        {
            uint tickDifference = (LocalTick - clientTick);
            _pingAverage.ComputeAverage(tickDifference);
            double averageInTime = (_pingAverage.Average * TickDelta * 1000);
            RoundTripTime = (long)Math.Round(averageInTime);
            _receivedPong = true;

            OnRoundTripTimeUpdated?.Invoke(RoundTripTime);
        }

        /// <summary>
        /// Sends a ping to the server.
        /// </summary>
        private void TrySendPing(uint? tickOverride = null)
        {
            byte pingInterval = PingInterval;

            /* How often client may send ping is based on if
             * the server responded to the last ping.
             * A response may not be received if the server
             * believes the client is pinging too fast, or if the 
             * client is having difficulties reaching the server. */
            long requiredTime = (pingInterval * 1000);
            float multiplier = (_receivedPong) ? 1f : 1.5f;

            requiredTime = (long)(requiredTime * multiplier);
            uint requiredTicks = TimeToTicks(pingInterval * multiplier);

            _pingTicks++;
            /* We cannot just consider time because ticks might run slower
             * from adjustments. We also cannot only consider ticks because
             * they might run faster from adjustments. Therefor require both
             * to have pass checks. */
            if (_pingTicks < requiredTicks || _pingStopwatch.ElapsedMilliseconds < requiredTime)
                return;

            _pingTicks = 0;
            _pingStopwatch.Restart();
            //Unset receivedPong, wait for new response.
            _receivedPong = false;

            uint tick = (tickOverride == null) ? LocalTick : tickOverride.Value;
            PooledWriter writer = WriterPool.Retrieve();
            writer.WritePacketId(PacketId.PingPong);
            writer.WriteTickUnpacked(tick);
            _networkManager.TransportManager.SendToServer((byte)Channel.Unreliable, writer.GetArraySegment());
            writer.Store();
        }

        /// <summary>
        /// Sends a pong to a client.
        /// </summary>
        internal void SendPong(NetworkConnection conn, uint clientTick)
        {
            if (!conn.IsActive || !conn.Authenticated)
                return;

            PooledWriter writer = WriterPool.Retrieve();
            writer.WritePacketId(PacketId.PingPong);
            writer.WriteTickUnpacked(clientTick);
            conn.SendToClient((byte)Channel.Unreliable, writer.GetArraySegment());
            writer.Store();
        }
        #endregion

        /// <summary>
        /// Increases the tick based on simulation rate.
        /// </summary>
        private void IncreaseTick()
        {
            bool isClient = _networkManager.IsClient;
            bool isServer = _networkManager.IsServer;

            double tickDelta = TickDelta;
            double timePerSimulation = (isServer) ? tickDelta : _adjustedTickDelta;
            if (timePerSimulation == 0d)
            {
                Debug.LogWarning($"Simulation delta cannot be 0. Network timing will not continue.");
                return;
            }

            double time = Time.unscaledDeltaTime;

            _elapsedTickTime += time;
            FrameTicked = (_elapsedTickTime >= timePerSimulation);

            //Number of ticks to occur this frame.
            int ticksCount = Mathf.FloorToInt((float)(_elapsedTickTime / timePerSimulation));
            if (ticksCount > 1)
                _lastMultipleTicksTime = Time.unscaledDeltaTime;

            if (_allowTickDropping && !_networkManager.IsServer)
            {
                //If ticks require dropping. Set exactly to maximum ticks.
                if (ticksCount > _maximumFrameTicks)
                    _elapsedTickTime = (timePerSimulation * (double)_maximumFrameTicks);
            }

            bool variableTiming = (_timingType == TimingType.Variable);
            bool frameTicked = FrameTicked;

            do
            {
                if (frameTicked)
                {
                    _elapsedTickTime -= timePerSimulation;
                    OnPreTick?.Invoke();
                }

                /* This has to be called inside the loop because
                 * OnPreTick promises data hasn't been read yet.
                 * Therefor iterate must occur after OnPreTick.
                 * Iteration will only run once per frame. */
                if (frameTicked || variableTiming)
                    TryIterateData(true);

                if (frameTicked)
                {
                    OnTick?.Invoke();

                    if (PhysicsMode == PhysicsMode.TimeManager)
                    {
                        float tick = (float)TickDelta;
                        OnPrePhysicsSimulation?.Invoke(tick);
                        Physics.Simulate(tick);
                        Physics2D.Simulate(tick);
                        OnPostPhysicsSimulation?.Invoke(tick);
                    }

                    OnPostTick?.Invoke();
                    /* If isClient this is the
                     * last tick during this loop. */
                    if (isClient && (_elapsedTickTime < timePerSimulation))
                    {
                        _networkManager.ClientManager.TrySendLodUpdate(LocalTick, false);
                        TrySendPing(LocalTick + 1);
                    }

                    if (_networkManager.IsServer)
                        SendTimingAdjustment();
                }

                //Send out data.
                if (frameTicked || variableTiming)
                    TryIterateData(false);

                if (frameTicked)
                {
                    if (_networkManager.IsClient)
                        _clientTicks++;

                    Tick++;
                    LocalTick++;

                    _networkManager.ObserverManager.CalculateLevelOfDetail(LocalTick);
                }
            } while (_elapsedTickTime >= timePerSimulation);
        }



        #region Tick conversions.
        /// <summary>
        /// Returns the percentage of how far the TimeManager is into the next tick.
        /// </summary>
        /// <returns></returns>
        public double GetTickPercent()
        {
            double percent = (_elapsedTickTime / TickDelta) * 100d;
            return percent;
        }
        /// <summary>
        /// Returns a PreciseTick.
        /// </summary>
        /// <param name="tick">Tick to set within the returned PreciseTick.</param>
        /// <returns></returns>
        public PreciseTick GetPreciseTick(uint tick)
        {
            double percent = (_elapsedTickTime / TickDelta) * 100;
            return new PreciseTick(tick, percent);
        }
        /// <summary>
        /// Returns a PreciseTick.
        /// </summary>
        /// <param name="tickType">Tick to use within PreciseTick.</param>
        /// <returns></returns>
        public PreciseTick GetPreciseTick(TickType tickType)
        {
            if (_networkManager == null)
                return default;

            if (tickType == TickType.Tick)
            {
                return GetPreciseTick(Tick);
            }
            else if (tickType == TickType.LocalTick)
            {
                return GetPreciseTick(LocalTick);
            }
            else if (tickType == TickType.LastPacketTick)
            {
                return GetPreciseTick(LastPacketTick);
            }
            else
            {
                _networkManager.LogError($"TickType {tickType.ToString()} is unhandled.");
                return default;
            }
        }


        /// <summary>
        /// Converts current ticks to time.
        /// </summary>
        /// <param name="tickType">TickType to compare against.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double TicksToTime(TickType tickType = TickType.LocalTick)
        {
            if (tickType == TickType.LocalTick)
            {
                return TicksToTime(LocalTick);
            }
            else if (tickType == TickType.Tick)
            {
                return TicksToTime(Tick);
            }
            else if (tickType == TickType.LastPacketTick)
            {
                return TicksToTime(LastPacketTick);
            }
            else
            {
                _networkManager.LogError($"TickType {tickType} is unhandled.");
                return 0d;
            }
        }

        /// <summary>
        /// Converts a PreciseTick to time.
        /// </summary>
        /// <param name="pt">PreciseTick to convert.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double TicksToTime(PreciseTick pt)
        {
            double tickTime = TicksToTime(pt.Tick);
            double percentTime = ((pt.Percent / 100) * TickDelta);
            return (tickTime + percentTime);
        }

        /// <summary>
        /// Converts a number ticks to time.
        /// </summary>
        /// <param name="ticks">Ticks to convert.</param>
        /// <returns></returns>
        public double TicksToTime(uint ticks)
        {
            return (TickDelta * (double)ticks);
        }

        /// <summary>
        /// Gets time passed from currentTick to previousTick.
        /// </summary>
        /// <param name="currentTick">The current tick.</param>
        /// <param name="previousTick">The previous tick.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double TimePassed(uint currentTick, uint previousTick)
        {
            double multiplier;
            double result;
            if (currentTick >= previousTick)
            {
                multiplier = 1f;
                result = TicksToTime(currentTick - previousTick);
            }
            else
            {
                multiplier = -1f;
                result = TicksToTime(previousTick - currentTick);
            }

            return (result * multiplier);
        }

        /// <summary>
        /// Gets time passed from Tick to preciseTick.
        /// </summary>
        /// <param name="preciseTick">PreciseTick value to compare against.</param>
        /// <param name="allowNegative">True to allow negative values. When false and value would be negative 0 is returned.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double TimePassed(PreciseTick preciseTick, bool allowNegative = false)
        {
            PreciseTick currentPt = GetPreciseTick(TickType.Tick);

            long tickDifference = (currentPt.Tick - preciseTick.Tick);
            double percentDifference = (currentPt.Percent - preciseTick.Percent);

            /* If tickDifference is less than 0 or tickDifference and percentDifference are 0 or less
             * then the result would be negative. */
            bool negativeValue = (tickDifference < 0 || (tickDifference <= 0 && percentDifference <= 0));

            if (!allowNegative && negativeValue)
                return 0d;

            double tickTime = TimePassed(preciseTick.Tick, true);
            double percent = (percentDifference / 100);
            double percentTime = (percent * TickDelta);

            return (tickTime + percentTime);
        }
        /// <summary>
        /// Gets time passed from Tick to previousTick.
        /// </summary>
        /// <param name="previousTick">The previous tick.</param>
        /// <param name="allowNegative">True to allow negative values. When false and value would be negative 0 is returned.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double TimePassed(uint previousTick, bool allowNegative = false)
        {
            uint currentTick = Tick;
            //Difference will be positive.
            if (currentTick >= previousTick)
            {
                return TicksToTime(currentTick - previousTick);
            }
            //Difference would be negative.
            else
            {
                if (!allowNegative)
                {
                    return 0d;
                }
                else
                {
                    double difference = TicksToTime(previousTick - currentTick);
                    return (difference * -1d);
                }
            }
        }

        /// <summary>
        /// Converts time to ticks.
        /// </summary>
        /// <param name="time">Time to convert.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint TimeToTicks(double time, TickRounding rounding = TickRounding.RoundNearest)
        {
            double result = (time / TickDelta);

            if (rounding == TickRounding.RoundNearest)
                return (uint)Math.Round(result);
            else if (rounding == TickRounding.RoundDown)
                return (uint)Math.Floor(result);
            else
                return (uint)Math.Ceiling(result);
        }

        /// <summary>
        /// Estimatedly converts a synchronized tick to what it would be for the local tick.
        /// </summary>
        /// <param name="tick">Synchronized tick to convert.</param>
        /// <returns></returns>
        public uint TickToLocalTick(uint tick)
        {
            //Server will always have local and tick aligned.
            if (_networkManager.IsServer)
                return tick;

            long difference = (Tick - tick);
            //If no ticks have passed then return current local tick.
            if (difference <= 0)
                return LocalTick;

            long result = (LocalTick - difference);
            if (result <= 0)
                result = 0;

            return (uint)result;
        }
        /// <summary>
        /// Estimatedly converts a local tick to what it would be for the synchronized tick.
        /// </summary>
        /// <param name="localTick">Local tick to convert.</param>
        /// <returns></returns>
        public uint LocalTickToTick(uint localTick)
        {
            //Server will always have local and tick aligned.
            if (_networkManager.IsServer)
                return localTick;

            long difference = (LocalTick - localTick);
            //If no ticks have passed then return current local tick.
            if (difference <= 0)
                return Tick;

            long result = (Tick - difference);
            if (result <= 0)
                result = 0;

            return (uint)result;

        }
        #endregion


        /// <summary>
        /// Tries to iterate incoming or outgoing data.
        /// </summary>
        /// <param name="incoming">True to iterate incoming.</param>
        private void TryIterateData(bool incoming)
        {
            if (incoming)
            {
                /* It's not possible for data to come in
                 * more than once per frame but there could
                 * be new data going out each tick, since
                 * movement is often based off the tick system.
                 * Because of this don't iterate incoming if
                 * it's the same frame but the outgoing
                 * may iterate multiple times per frame. */
                int frameCount = Time.frameCount;
                if (frameCount == _lastIncomingIterationFrame)
                    return;
                _lastIncomingIterationFrame = frameCount;

                _networkManager.TransportManager.IterateIncoming(true);
                _networkManager.TransportManager.IterateIncoming(false);
            }
            else
            {
                _networkManager.TransportManager.IterateOutgoing(true);
                _networkManager.TransportManager.IterateOutgoing(false);
            }
        }


        #region Timing adjusting.    
        /// <summary>
        /// Sends a TimingUpdate packet to clients.
        /// </summary>
        private void SendTimingAdjustment()
        {
            uint requiredTicks = TimeToTicks(TIMING_INTERVAL);
            uint tick = Tick;
            if (tick - _lastUpdateTicks >= requiredTicks)
            {
                //Now send using a packetId.
                PooledWriter writer = WriterPool.Retrieve();
                foreach (NetworkConnection item in _networkManager.ServerManager.Clients.Values)
                {
                    if (!item.Authenticated)
                        continue;

                    writer.Reset();
                    writer.WritePacketId(PacketId.TimingUpdate);
                    //Write the highest number of replicates the client had for the latest tick.
                    ushort highestQueueCount = item.GetAndResetAverageQueueCount();
                    writer.WriteUInt16(highestQueueCount);

                    item.SendToClient((byte)Channel.Unreliable, writer.GetArraySegment());
                }
                //writer.WritePacketId(PacketId.TimingUpdate);
                //_networkManager.TransportManager.SendToClients((byte)Channel.Unreliable, writer.GetArraySegment());
                writer.Store();

                _lastUpdateTicks = tick;
            }
        }

        private enum TimingUpdateChange : int
        {
            JustRight = 0,
            TooFast = 1,
            TooSlow = -1,
        }
        private TimingUpdateChange _timingUpdateChange = TimingUpdateChange.JustRight;
        private float _updateChangeMultiplier = 1f;

        /// <summary>
        /// Called on client when server sends a timing update.
        /// </summary>
        /// <param name="ta"></param>
        internal void ParseTimingUpdate(PooledReader reader)
        {
            ushort targetQueuedInputs = _networkManager.PredictionManager.QueuedInputs;
            /* The amount of inputs which are over or below
             * the targeted queued inputs. If over target the
             * difference will be positive. Negative values
             * are ignored because the client may not send
             * inputs if idle but values over the target
             * need to slow down the client. */
            ushort queuedInputs = reader.ReadUInt16();
            //Don't adjust timing on server.
            if (_networkManager.IsServer)
                return;

            UpdateTick();

            //If over target set to overage. Otherwise set to 0.
            ushort inputsOverTargetQueued = (queuedInputs > targetQueuedInputs) ? (ushort)(queuedInputs - targetQueuedInputs) : (ushort)0;
            //Number of ticks expected for the tick rate.
            uint expectedClientTicks = (uint)(TickRate * TIMING_INTERVAL);
            //Ticks iterated since last update.
            uint clientTicks = _clientTicks;
            //Reset client ticks.
            _clientTicks = 0;
            /* Multiplier to apply towards tickrate to
             * adjust for misaligned timing. */
            double adjustment;
            /* Number of ticks which exceed expected
             * ticks. If positive the client is sending too fast,
             * if negative too slow. If the value is 0, juuuust right. */
            long tickDifference;
            /* If queuedInputDifference is 0 then no replicates were
             * performed for the tick or there were not enough to meet
             * the target queue count. If that is the case then the client
             * could not be sending replicates, such as idle, or not
             * sending fast enough. We do not really know unless we sent
             * packets every tick to keep track but that's a bit wasteful.
             * When this occurs calculate based off local ticks vs expected. */
            if (inputsOverTargetQueued == 0)
            {
                /* If no replicates were in queue at the time of the update
                 * then base timing on local ticks. This can happen due to the
                 * idle/not replicating mentioned above. */
                if (queuedInputs == 0)
                    tickDifference = ((long)clientTicks - (long)expectedClientTicks);
                //If there were queued inputs then assume the client is behind target queue.
                else
                    tickDifference = -(targetQueuedInputs - queuedInputs);
            }
            //If the server confirmed client is sending too fast.
            else
            {
                tickDifference = inputsOverTargetQueued;
            }

            TimingUpdateChange timingUpdateChange;
            if (tickDifference == 0)
                timingUpdateChange = TimingUpdateChange.JustRight;
            else if (tickDifference > 0)
                timingUpdateChange = TimingUpdateChange.TooFast;
            else
                timingUpdateChange = TimingUpdateChange.TooSlow;

            const float updateChangeModifier = 0.1f;
            if (timingUpdateChange != _timingUpdateChange)
            {
                if (_updateChangeMultiplier > updateChangeModifier)
                    _updateChangeMultiplier -= updateChangeModifier;
            }
            else
            {
                if (_updateChangeMultiplier < 1)
                    _updateChangeMultiplier += (updateChangeModifier * 0.25f);
            }
            _timingUpdateChange = timingUpdateChange;

            float newTickDifference = ((float)tickDifference * _updateChangeMultiplier);
            tickDifference = (int)newTickDifference;

            //Debug.Log($"ChangeMultiplier {_updateChangeMultiplier}. ClientTicks {clientTicks}. Queued {queuedInputs}. Over target queued {inputsOverTargetQueued}. Difference {tickDifference}. TooFastCount {_timingTooFastCount}.");

            //If over the reset limitation then set difference to 0 forcing use of normal tickdelta.
            if (Mathf.Abs(tickDifference) >= RESET_ADJUSTMENT_THRESHOLD)
                tickDifference = 0;

            double multiplierValue = (tickDifference > 0) ? CLIENT_SLOWDOWN_VALUE : CLIENT_SPEEDUP_VALUE;
            adjustment = TickDelta * ((double)tickDifference * multiplierValue);

            //Set adjustedTickValue to contain adjustment.
            _adjustedTickDelta = TickDelta + adjustment;
            /* If client was sending too fast last update
             * then add more slowdown to the adjusted delta based on
             * number of times client was too fast. */

            _adjustedTickDelta += (TickDelta * (CLIENT_SLOWDOWN_VALUE * _timingTooFastCount));
            //Lerp between new and old adjusted value to blend them so the change isn't sudden.
            //Clamp adjusted tick delta so it cannot be unreasonably fast or slow.
            _adjustedTickDelta = Maths.ClampDouble(_adjustedTickDelta, _clientTimingRange[0], _clientTimingRange[1]);

            const float tooFastModifier = 0.5f;
            //Increase too fast count if needed.
            if (tickDifference > 0)
                _timingTooFastCount += tooFastModifier;
            //Otherwise reduce it by 1 but not below 0.
            else if (_timingTooFastCount >= tooFastModifier)
                _timingTooFastCount -= tooFastModifier;
            else
                _timingTooFastCount = 0f;

            //Updates synchronized tick.
            void UpdateTick()
            {
                uint rttTicks = TimeToTicks((RoundTripTime / 2) / 1000f);
                Tick = LastPacketTick + rttTicks;
            }
        }
        #endregion

        /// <summary>
        /// Sets the TickRate to use. This value is not synchronized, it must be set on client and server independently.
        /// </summary>
        /// <param name="value">New TickRate to use.</param>
        public void SetTickRate(ushort value)
        {
            TickRate = value;
            TickDelta = (1d / TickRate);
            _adjustedTickDelta = TickDelta;
            _clientTimingRange = new double[]
            {
                TickDelta * (1f - CLIENT_TIMING_PERCENT_RANGE),
                TickDelta * (1f + CLIENT_TIMING_PERCENT_RANGE)
            };
        }

        #region UNITY_EDITOR
        private void OnValidate()
        {
            SetInitialValues();
        }
        #endregion

    }

}