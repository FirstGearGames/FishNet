using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Extension;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using SystemStopwatch = System.Diagnostics.Stopwatch;
using UnityScene = UnityEngine.SceneManagement.Scene;

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
        public uint LastPacketTick { get; internal set; }
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
        internal byte PingInterval => _pingInterval;
        /// <summary>
        /// How often in seconds to update prediction timing. Lower values will result in marginally more accurate timings at the cost of bandwidth.
        /// </summary>        
        [Tooltip("How often in seconds to update prediction timing. Lower values will result in marginally more accurate timings at the cost of bandwidth.")]
        [Range(1, 15)]
        [SerializeField]
        private byte _timingInterval = 2;
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
        #endregion

        #region Const.
        /// <summary>
        /// Maximum percentage timing may vary from SimulationInterval for clients.
        /// </summary>
        private const float CLIENT_TIMING_PERCENT_RANGE = 0.5f;
        /// <summary>
        /// Percentage of TickDelta client will adjust when needing to speed up.
        /// </summary>
        private const double CLIENT_SPEEDUP_PERCENT = 0.003d;
        /// <summary>
        /// Percentage of TickDelta client will adjust when needing to slow down.
        /// </summary>
        private const double CLIENT_SLOWDOWN_PERCENT = 0.005d;
        /// <summary>
        /// When steps to be sent to clients are equal to or higher than this value in either direction a reset steps will be sent.
        /// </summary>
        private const byte RESET_STEPS_THRESHOLD = 5;
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
                OnPrePhysicsSimulation?.Invoke(Time.fixedDeltaTime);
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
                if (PhysicsMode == PhysicsMode.Unity && Time.inFixedTimeStep)
                    OnPostPhysicsSimulation?.Invoke(Time.fixedDeltaTime);
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
            Physics.autoSimulation = true;
#if !UNITY_2020_2_OR_NEWER
            Physics2D.autoSimulation = true;
#else
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
#endif

            float simulationTime = PlayerPrefs.GetFloat(SAVED_FIXED_TIME_TEXT, float.MinValue);
            if (simulationTime != float.MinValue)
                Time.fixedDeltaTime = simulationTime;
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
            {
                Physics.autoSimulation = false;
#if !UNITY_2020_2_OR_NEWER
                Physics2D.autoSimulation = false;
#else
                Physics2D.simulationMode = SimulationMode2D.Script;
#endif
            }
            //Automatically simulate.
            else
            {
                Physics.autoSimulation = true;
#if !UNITY_2020_2_OR_NEWER
                Physics2D.autoSimulation = true;
#else
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
#endif
            }
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
            using (PooledWriter writer = WriterPool.GetWriter())
            {
                writer.WritePacketId(PacketId.PingPong);
                writer.WriteUInt32(tick, AutoPackType.Unpacked);
                _networkManager.TransportManager.SendToServer((byte)Channel.Unreliable, writer.GetArraySegment());
            }
        }

        /// <summary>
        /// Sends a pong to a client.
        /// </summary>
        internal void SendPong(NetworkConnection conn, uint clientTick)
        {
            if (!conn.IsActive || !conn.Authenticated)
                return;

            using (PooledWriter writer = WriterPool.GetWriter())
            {
                writer.WritePacketId(PacketId.PingPong);
                writer.WriteUInt32(clientTick, AutoPackType.Unpacked);
                conn.SendToClient((byte)Channel.Unreliable, writer.GetArraySegment());
            }
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
                        _networkManager.ClientManager.SendLodUpdate(false);
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
            if (_networkManager == null)
                return default;

            double delta = (_networkManager.IsServer) ? TickDelta : _adjustedTickDelta;
            double percent = (_elapsedTickTime / delta) * 100d;
            return percent;
        }
        /// <summary>
        /// Returns a PreciseTick.
        /// </summary>
        /// <param name="tick">Tick to set within the returned PreciseTick.</param>
        /// <returns></returns>
        public PreciseTick GetPreciseTick(uint tick)
        {
            if (_networkManager == null)
                return default;

            double delta = (_networkManager.IsServer) ? TickDelta : _adjustedTickDelta;
            double percent = (_elapsedTickTime / delta) * 100;

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
            uint requiredTicks = TimeToTicks(_timingInterval);
            uint tick = Tick;
            if (tick - _lastUpdateTicks >= requiredTicks)
            {
                //Now send using a packetId.
                PooledWriter writer = WriterPool.GetWriter();
                writer.WritePacketId(PacketId.TimingUpdate);
                _networkManager.TransportManager.SendToClients((byte)Channel.Unreliable, writer.GetArraySegment());
                writer.Dispose();

                _lastUpdateTicks = tick;
            }
        }

        /// <summary>
        /// Called on client when server sends a timing update.
        /// </summary>
        /// <param name="ta"></param>
        internal void ParseTimingUpdate()
        {
            //Don't adjust timing on server.
            if (_networkManager.IsServer)
                return;

            //Add half of rtt onto tick.
            uint rttTicks = TimeToTicks((RoundTripTime / 2) / 1000f);
            Tick = LastPacketTick + rttTicks;
            uint expected = (uint)(TickRate * _timingInterval);
            long difference;
            //If ticking too fast.
            if (_clientTicks > expected)
                difference = (long)(_clientTicks - expected);
            //Not ticking fast enough.
            else
                difference = (long)((expected - _clientTicks) * -1);

            //If difference is unusually off then reset timings.
            if (Mathf.Abs(difference) >= RESET_STEPS_THRESHOLD)
            {
                _adjustedTickDelta = TickDelta;
            }
            else
            {
                sbyte steps = (sbyte)Mathf.Clamp(difference, sbyte.MinValue, sbyte.MaxValue);
                double percent = (steps < 0) ? CLIENT_SPEEDUP_PERCENT : CLIENT_SLOWDOWN_PERCENT;
                double change = (steps * (percent * TickDelta));

                _adjustedTickDelta = MathFN.ClampDouble(_adjustedTickDelta + change, _clientTimingRange[0], _clientTimingRange[1]);
            }

            _clientTicks = 0;
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