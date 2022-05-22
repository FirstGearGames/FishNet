using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Extension;
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
    public sealed class TimeManager : MonoBehaviour
    {
        //#region Types.
        //public enum BufferPurgeType
        //{
        //    /// <summary>
        //    /// Run an additional input per tick when buffered inputs are higher than normal.
        //    /// This prevents clients from sending excessive inputs but may briefly disrupt clients synchronization if their timing is drastically off.
        //    /// Use this option for more secure prediction.
        //    /// </summary>
        //    Discard = 0,
        //    /// <summary>
        //    /// Run an additional input per tick when buffered inputs are higher than normal.
        //    /// This is useful for keeping the client synchronized with the server by processing inputs that would normally be discarded.
        //    /// However, by running extra buffered inputs the client has a better opportunity to cheat.
        //    /// </summary>
        //    Run = 1
        //}
        //#endregion

        #region Public.
        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        public event Action<NetworkBehaviour> OnPreReconcile;
        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        public event Action<NetworkBehaviour> OnPostReconcile;
        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        public event Action<PhysicsScene, PhysicsScene2D> OnPreReplicateReplay;
        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        public event Action<PhysicsScene, PhysicsScene2D> OnPostReplicateReplay;
        /// <summary>
        /// Called right before a tick occurs, as well before data is read.
        /// </summary>
        public event Action OnPreTick;
        /// <summary>
        /// Called when a tick occurs.
        /// </summary>
        public event Action OnTick;
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
        /// RoundTripTime in milliseconds.
        /// </summary>
        public long RoundTripTime { get; private set; }
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
        /// Percentage of how much into next tick the time is.
        /// </summary>
        public byte TickPercent
        {
            get
            {
                if (_networkManager == null)
                    return 0;

                double delta = (_networkManager.IsServer) ? TickDelta : _adjustedTickDelta;
                double percent = (_elapsedTickTime / delta) * 100;
                return (byte)Mathf.Clamp((float)percent, 0, 100);
            }
        }
        /// <summary>
        /// DeltaTime for TickRate.
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
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of buffered inputs which will be accepted from client before old inputs are discarded.")]
        [Range(1, 100)]
        [SerializeField]
        private byte _maximumBufferedInputs = 15;
        /// <summary>
        /// Maximum number of buffered inputs which will be accepted from client before old inputs are discarded.
        /// </summary>
        public byte MaximumBufferedInputs => _maximumBufferedInputs;
        #endregion

        #region Private.
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
        /// Time elapsed after ticks. This is extra time beyond the simulation rate.
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

        /// <summary>
        /// Ticks that have passed on client since the last time server sent an UpdateTicksBroadcast.
        /// </summary>
        private uint _clientTicks = 0;
        /// <summary>
        /// Last Tick the server sent out UpdateTicksBroadcast.
        /// </summary>
        private uint _lastUpdateTicks = 0;

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

            IncreaseTick();
            OnUpdate?.Invoke();
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
        internal void InitializeOnceInternal(NetworkManager networkManager)
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
            if (obj.ConnectionState != LocalConnectionStates.Started)
            {
                _pingStopwatch.Stop();
                ClientUptime = 0f;
                LocalTick = 0;
                //Also reset Tick if not running as host.
                if (!_networkManager.IsServer)
                    Tick = 0;
            }
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
        /// Invokes OnPre/PostReconcile events.
        /// Internal use.
        /// </summary>
        [APIExclude] //codegen make internal and then public in codegen.
        public void InvokeOnReconcile(NetworkBehaviour nb, bool before)
        {
            if (before)
                OnPreReconcile?.Invoke(nb);
            else
                OnPostReconcile?.Invoke(nb);
        }

        /// <summary>
        /// Invokes OnReplicateReplay.
        /// Internal use.
        /// </summary>
        [APIExclude] //codegen make internal and then public in codegen.
        public void InvokeOnReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d, bool before)
        {
            if (before)
                OnPreReplicateReplay?.Invoke(ps, ps2d);
            else
                OnPostReplicateReplay?.Invoke(ps, ps2d);
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
                    {
                        if (_networkManager.CanLog(LoggingType.Error))
                            Debug.LogError($"There are multiple TimeManagers instantiated which are using manual physics. Manual physics with multiple TimeManagers is not supported.");
                    }
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
                writer.WriteUInt16((ushort)PacketId.PingPong);
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
                writer.WriteUInt16((ushort)PacketId.PingPong);
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

            double timePerSimulation = (_networkManager.IsServer) ? TickDelta : _adjustedTickDelta;
            double time = Time.deltaTime;
            _elapsedTickTime += time;
            FrameTicked = (_elapsedTickTime >= timePerSimulation);

            while (_elapsedTickTime >= timePerSimulation)
            {
                _elapsedTickTime -= timePerSimulation;

                OnPreTick?.Invoke();
                /* This has to be called inside the loop because
                 * OnPreTick promises data hasn't been read yet.
                 * Therefor iterate must occur after OnPreTick.
                 * Iteration will only run once per frame. */
                TryIterateData(true);

                OnTick?.Invoke();

                if (PhysicsMode == PhysicsMode.TimeManager)
                {
                    float tick = (float)TickDelta;
                    Physics.Simulate(tick);
                    Physics2D.Simulate(tick);
                }

                OnPostTick?.Invoke();

                /* If isClient this is the
                 * last tick during this loop. */
                if (isClient && (_elapsedTickTime < timePerSimulation))
                    TrySendPing(LocalTick + 1);

                if (_networkManager.IsServer)
                    SendTimingAdjustment();

                //Send out data.
                TryIterateData(false);

                if (_networkManager.IsClient)
                    _clientTicks++;

                Tick++;
                LocalTick++;
            }

        }

        #region TicksToTime float. 
        /// <summary>
        /// Converts current ticks to time.
        /// </summary>
        /// <param name="useLocalTick">True to use the LocalTick, false to use Tick.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float TicksToTime(bool useLocalTick = true)
        {
            if (useLocalTick)
                return TicksToTime(LocalTick);
            else
                return TicksToTime(Tick);
        }
        /// <summary>
        /// Converts a number ticks to time.
        /// </summary>
        /// <param name="ticks">Ticks to convert.</param>
        /// <returns></returns>
        public float TicksToTime(uint ticks)
        {
            return (float)(TickDelta * ticks);
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
                return TicksToTimeDouble(currentTick - previousTick);                
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
                    double difference = TicksToTimeDouble(previousTick - currentTick);
                    return (difference * -1d);
                }
            }
        }
        /// <summary>
        /// Converts time passed from currentTick to previous. Value will be negative if previousTick is larger than currentTick.
        /// </summary>
        /// <param name="currentTick">The current tick.</param>
        /// <param name="previousTick">The previous tick.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float TicksToTime(uint currentTick, uint previousTick)
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

            return (float)(result * multiplier);
        }
        #endregion//Remove on 2022/06/01 in favor of AllowStacking.

        #region TicksToTimeDouble. //Remove on 2022/06/01 and change TicksToTime to return double.
        /// <summary>
        /// Converts current ticks to time.
        /// </summary>
        /// <param name="useLocalTick">True to use the LocalTick, false to use Tick.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double TicksToTimeDouble(bool useLocalTick = true)
        {
            if (useLocalTick)
                return TicksToTimeDouble(LocalTick);
            else
                return TicksToTimeDouble(Tick);
        }
        /// <summary>
        /// Converts a number ticks to time.
        /// </summary>
        /// <param name="ticks">Ticks to convert.</param>
        /// <returns></returns>
        public double TicksToTimeDouble(uint ticks)
        {
            return (TickDelta * ticks);
        }
        /// <summary>
        /// Converts time passed from currentTick to previous. Value will be negative if previousTick is larger than currentTick.
        /// </summary>
        /// <param name="currentTick">The current tick.</param>
        /// <param name="previousTick">The previous tick.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double TicksToTimeDouble(uint currentTick, uint previousTick)
        {
            double multiplier;
            double result;
            if (currentTick >= previousTick)
            {
                multiplier = 1f;
                result = TicksToTimeDouble(currentTick - previousTick);
            }
            else
            {
                multiplier = -1f;
                result = TicksToTimeDouble(previousTick - currentTick);
            }

            return (result * multiplier);
        }
        #endregion 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>
        /// Converts time to ticks.
        /// </summary>
        /// <param name="time">Time to convert.</param>
        /// <returns></returns>
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
        /// Tries to iterate incoming or outgoing data.
        /// </summary>
        /// <param name="incoming">True to iterate incoming.</param>
        /// <param name="isTick">True if call is occuring during a tick.</param>
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

                /* This will be true if to iterate first first
                 * resulting in the first TransportManager.Iterate
                 * being called for server, and the second for client. */
                //bool a = (_networkManager.IncomingIterationOrder == NetworkManager.HostIterationOrder.ServerFirst);
                //_networkManager.TransportManager.IterateIncoming(a);
                //_networkManager.TransportManager.IterateIncoming(!a);
                _networkManager.TransportManager.IterateIncoming(true);
                _networkManager.TransportManager.IterateIncoming(false);
            }
            else
            {
                //bool a = (_networkManager.OutgoingIterationOrder == NetworkManager.HostIterationOrder.ServerFirst);
                //_networkManager.TransportManager.IterateOutgoing(a);
                //_networkManager.TransportManager.IterateOutgoing(!a);
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
        /// Called on client when server sends StepChange.
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

            uint expected = (uint)(TickRate * 2);
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