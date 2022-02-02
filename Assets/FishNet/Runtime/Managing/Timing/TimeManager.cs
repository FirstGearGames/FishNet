using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Managing.Timing.Broadcast;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Extension;
using System;
using System.Collections.Generic;
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
        #region Types.        
        private class ClientTickData
        {
            public bool UpdatesPaused { get; private set; }
            public int Buffered;
            public ushort SendTicksRemaining;

            public ClientTickData(ushort adjustmentInterval)
            {
                Reset(adjustmentInterval);
            }

            public void Reset(ushort adjustmentInterval)
            {
                UpdatesPaused = false;
                Buffered = 0;
                SendTicksRemaining = adjustmentInterval;
            }
            public void Pause()
            {
                UpdatesPaused = true;
            }
        }
        #endregion

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
        public uint Tick { get; private set; }
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
        /// How long the local server has been connected.
        /// </summary>
        public float ServerUptime { get; private set; }
        /// <summary>
        /// How long the local client has been connected.
        /// </summary>
        public float ClientUptime { get; private set; }
        #endregion

        #region Serialized.
        [Header("Timing")]
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


        [Header("Prediction")]
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
        /// This is exposed until automatic state control is implemented.
        /// </summary>
        public byte MaximumBufferedInputs => _maximumBufferedInputs;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Number of inputs server prefers to have buffered from clients.")]
        [Range(1, 100)]
        [SerializeField]
        private byte _targetBufferedInputs = 2;
        /// <summary>
        /// Number of inputs server prefers to have buffered from clients.
        /// </summary>
        public byte TargetBufferedInputs
        {
            get => _targetBufferedInputs;
            private set => _targetBufferedInputs = value;
        }
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
        /// Broadcast cache sent to clients for a step change.
        /// </summary>
        private TimingAdjustmentBroadcast _timeAdjustment = new TimingAdjustmentBroadcast();
        /// <summary>
        /// Range which client timing may reside within.
        /// </summary>
        private double[] _clientTimingRange;
        /// <summary>
        /// How many inputs are buffered per client.
        /// </summary>
        private Dictionary<NetworkConnection, ClientTickData> _bufferedClientInputs = new Dictionary<NetworkConnection, ClientTickData>();
        /// <summary>
        /// ClientTickData cache to prevent garbage allocation.
        /// </summary>
        private Stack<ClientTickData> _tickDataCache = new Stack<ClientTickData>();
        /// <summary>
        /// How many ticks to wait for each timing update to clients.
        /// </summary>
        private ushort _timingAdjustmentInterval;
        /// <summary>
        /// Last frame an iteration occurred for incoming.
        /// </summary>
        private int _lastIncomingIterationFrame = -1;
        /// <summary>
        /// True if client received Pong since last ping.
        /// </summary>
        private bool _receivedPong = true;
        /// <summary>
        /// Last step change the client received. The value will be 0 when default, or during a step reset.
        /// </summary>
        private sbyte _lastStepsReceived;
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
        /// How quickly to move AdjustedDeltaTick to DeltaTick.
        /// </summary>
        private const double POSITIVE_STEPS_RECOVERY_RATE = 0.0001f;
        /// <summary>
        /// Ping interval in seconds.
        /// </summary>
        internal const float PING_INTERVAL = 1f;
        /// <summary>
        /// How many seconds between each timing adjustment from the server.
        /// </summary>
        private const byte ADJUST_TIMING_INTERVAL = 2;
        /// <summary>
        /// Number of steps to send which indicates client should reset their adjustedTickDelta to default.
        /// </summary>
        private const sbyte RESET_DELTA_STEPS = sbyte.MinValue;
        /// <summary>
        /// When steps to be sent to clients are equal to or higher than this value in either direction a reset steps will be sent.
        /// </summary>
        private const byte RESET_STEPS_THRESHOLD = 5;
        #endregion

#if UNITY_EDITOR
        private void OnDisable()
        {
            //If closing/stopping.
            if (ApplicationState.IsQuitting())
                _manualPhysics = 0;
            else if (PhysicsMode == PhysicsMode.TimeManager)
                _manualPhysics = Math.Max(0, _manualPhysics - 1);
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
            {
                ClientUptime += Time.deltaTime;
                if (_lastStepsReceived > 0)
                    _adjustedTickDelta = Mathf.MoveTowards((float)_adjustedTickDelta, (float)TickDelta, Time.deltaTime * (float)POSITIVE_STEPS_RECOVERY_RATE);
            }

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
        internal void InitializeOnce(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            SetInitialValues();
            _networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            _networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
            _networkManager.ServerManager.OnAuthenticationResult += ServerManager_OnAuthenticationResult;
            _networkManager.ClientManager.RegisterBroadcast<TimingAdjustmentBroadcast>(OnTimingAdjustmentBroadcast);
            _networkManager.ServerManager.RegisterBroadcast<AddBufferedBroadcast>(OnAddBufferedBroadcast);

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
            if (obj.ConnectionState != LocalConnectionStates.Started)
            {
                foreach (ClientTickData item in _bufferedClientInputs.Values)
                    _tickDataCache.Push(item);
                _bufferedClientInputs.Clear();
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
            SetAutomaticSimulation(PhysicsMode);
        }

        /// <summary>
        /// Updates automaticSimulation modes.
        /// </summary>
        /// <param name="automatic"></param>
        private void SetAutomaticSimulation(PhysicsMode mode)
        {
            string savedFixedTime = "SavedFixedTimeFN";
            //Do not automatically simulate.
            if (mode == PhysicsMode.TimeManager)
            {
#if UNITY_EDITOR
                //Preserve user tick rate.
                PlayerPrefs.SetFloat(savedFixedTime, Time.fixedDeltaTime);
                //Let the player know.
                if (Time.fixedDeltaTime != (float)TickDelta)
                    Debug.LogWarning("Time.fixedDeltaTime is being overriden with TickDelta");
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
#if UNITY_EDITOR
                float savedTime = PlayerPrefs.GetFloat(savedFixedTime, -1f);
                if (savedTime != -1f && Time.fixedDeltaTime != savedTime)
                {
                    Debug.LogWarning("Time.fixedDeltaTime has been set back to user values.");
                    Time.fixedDeltaTime = savedTime;
                }

                PlayerPrefs.DeleteKey(savedFixedTime);
#endif
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
        /// <param name="clientTIck"></param>
        internal void ModifyPing(uint clientTIck)
        {
            uint tickDifference = (LocalTick - clientTIck);
            _pingAverage.ComputeAverage(tickDifference);
            double averageInTime = (_pingAverage.Average * TickDelta * 1000);
            //Remove tickrate for a more accurate ping representation.
            averageInTime = Math.Max(0, averageInTime - (TickDelta * 1000d));
            RoundTripTime = (long)Math.Round(averageInTime);
            _receivedPong = true;
        }

        /// <summary>
        /// Sends a ping to the server.
        /// </summary>
        private void TrySendPing()
        {
            /* Set next ping time based on uptime.
            * Client should try to get their ping asap
            * once connecting but more casually after. 
            * If client did not receive a response to last
            * ping then wait longer. The server maybe didn't
            * respond because client is sending too fast. */
            long requiredTime = (_receivedPong) ?
                (long)(PING_INTERVAL * 1000) :
                (long)(PING_INTERVAL * 1500);

            _pingTicks++;
            uint requiredTicks = TimeToTicks(PING_INTERVAL);
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

            using (PooledWriter writer = WriterPool.GetWriter())
            {
                writer.WriteUInt16((ushort)PacketId.PingPong);
                writer.WriteUInt32(LocalTick, AutoPackType.Unpacked);
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
            double timePerSimulation = (_networkManager.IsServer) ? TickDelta : _adjustedTickDelta;
            double time = Time.deltaTime;
            _elapsedTickTime += time;

            bool ticked = (_elapsedTickTime >= timePerSimulation);
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
                //Send out data.
                TryIterateData(false);

                if (_networkManager.IsClient)
                    SendAddBuffered();
                if (_networkManager.IsServer)
                    SendTimingAdjustment();

                Tick++;
                LocalTick++;
            }

            //If ticked also try to send out data.
            if (ticked)
            {
                if (_networkManager.IsClient)
                    TrySendPing();
            }
        }

        /// <summary>
        /// Called when a client connection state changes with the server.
        /// </summary>
        private void ServerManager_OnRemoteConnectionState(NetworkConnection arg1, RemoteConnectionStateArgs arg2)
        {
            //If not started then remove from buffered inputs.
            if (arg2.ConnectionState != RemoteConnectionStates.Started)
            {
                if (_bufferedClientInputs.TryGetValue(arg1, out ClientTickData ctd))
                {
                    _tickDataCache.Push(ctd);
                    _bufferedClientInputs.Remove(arg1);
                }
            }
        }

        /// <summary>
        /// Called when a client authentication result occurs.
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="authenticated"></param>
        private void ServerManager_OnAuthenticationResult(NetworkConnection arg1, bool authenticated)
        {
            if (!authenticated)
                return;

            /* Start buffered at 0 and let client accumulate
             * buffered. Set ticks remaining to how many
             * ticks should pass for buffer to be at desired
             * amount. */
            ClientTickData ctd = AddToBuffered(arg1, 0);
            ctd.SendTicksRemaining = _timingAdjustmentInterval;
            SynchronizeTick(arg1);
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

                _lastIncomingIterationFrame = frameCount;
                _networkManager.TransportManager.IterateIncoming(false);
                _networkManager.TransportManager.IterateIncoming(true);
            }
            else
            {
                _networkManager.TransportManager.IterateOutgoing(true);
                _networkManager.TransportManager.IterateOutgoing(false);
            }
        }


        #region Timing adjusting.
        /// <summary>
        /// Sets number of inputs buffered for a connection.
        /// </summary>
        private ClientTickData AddToBuffered(NetworkConnection connection, int count = 1)
        {
            //Connection found.
            if (_bufferedClientInputs.TryGetValue(connection, out ClientTickData ctd))
            {
                if (ctd.UpdatesPaused)
                    ctd.Reset(_timingAdjustmentInterval);

                /* Make sure count won't exceed max value.
                 * If over max value don't even bother adding. */
                long next = (ctd.Buffered + count);
                if (next < int.MaxValue)
                    ctd.Buffered = (int)next;

                return ctd;
            }
            //Not found, make new entry.
            else
            {
                ClientTickData newCtd;
                if (_tickDataCache.Count == 0)
                {
                    newCtd = new ClientTickData(_timingAdjustmentInterval);
                }
                else
                {
                    newCtd = _tickDataCache.Pop();
                    newCtd.Reset(_timingAdjustmentInterval);
                }

                _bufferedClientInputs[connection] = newCtd;
                return newCtd;
            }
        }

        /// <summary>
        /// Called when the server receives an AddBufferedBroadcast.
        /// </summary>
        private void OnAddBufferedBroadcast(NetworkConnection connection, AddBufferedBroadcast msg)
        {
            AddToBuffered(connection, 1);
        }

        /// <summary>
        /// Sends an AddBuffered broadcast to the server.
        /// </summary>
        private void SendAddBuffered()
        {
            AddBufferedBroadcast ab = new AddBufferedBroadcast();
            _networkManager.ClientManager.Broadcast<AddBufferedBroadcast>(ab, Channel.Unreliable);
        }

        /// <summary>
        /// Sends a reliable timing adjustment to conn with no step change.
        /// </summary>
        /// <param name="conn"></param>
        private void SynchronizeTick(NetworkConnection conn)
        {
            _timeAdjustment.Tick = Tick;
            _timeAdjustment.Step = 0;
            _networkManager.ServerManager.Broadcast(conn, _timeAdjustment, true, Channel.Reliable);
        }

        /// <summary>
        /// Sends a TimingAdjustmentBroadcast to clients.
        /// </summary>
        private void SendTimingAdjustment()
        {
            /* set ticks remaining to interval.
             * when ticks remaining is 0 then
             * remove interval. if client is sending 1
             * per tick as expected then they should have sent exactly
             * interval by the time ticks remaining is 0. 
             * That is assuming all went well. */
            foreach (KeyValuePair<NetworkConnection, ClientTickData> item in _bufferedClientInputs)
            {
                ClientTickData ctd = item.Value;

                /* If client step updates are paused then
                 * do not proceed. */
                if (ctd.UpdatesPaused)
                    continue;

                ctd.SendTicksRemaining--;
                if (ctd.SendTicksRemaining > 0)
                    continue;

                ctd.SendTicksRemaining = _timingAdjustmentInterval;

                _timeAdjustment.Tick = Tick;

                /* If value is 1 or less then increase
                 * clients send rate. Ideally there will be two+ in queue
                 * before processing; this is where the - 2 comes from.
                 * 1 in queue would result in -1 step, where 0 in queue
                 * would result in -2 step. Lesser steps speed up client
                 * send rate more, while higher steps slow it down. */
                ctd.Buffered -= _timingAdjustmentInterval;
                sbyte steps;

                steps = (sbyte)(MathFN.ClampSByte(ctd.Buffered, (sbyte)-MaximumBufferedInputs, (sbyte)MaximumBufferedInputs) - TargetBufferedInputs);
                Channel channel;
                if (Mathf.Abs(steps) >= RESET_STEPS_THRESHOLD)
                {
                    channel = Channel.Reliable;
                    ctd.Pause();
                    steps = RESET_DELTA_STEPS;
                }
                else
                {
                    channel = Channel.Unreliable;
                }

                _timeAdjustment.Step = steps;
                _networkManager.ServerManager.Broadcast(item.Key, _timeAdjustment, true, channel);
            }
        }

        /// <summary>
        /// Called on client when server sends StepChange.
        /// </summary>
        /// <param name="ta"></param>
        private void OnTimingAdjustmentBroadcast(TimingAdjustmentBroadcast ta)
        {
            //Don't adjust timing on server.
            if (_networkManager.IsServer)
                return;

            //Add half of rtt onto tick.
            uint rttTicks = TimeToTicks((RoundTripTime / 2) / 1000f);
            Tick = ta.Tick + rttTicks;

            sbyte steps = ta.Step;
            _lastStepsReceived = steps;
            //No change required.
            if (steps == 0)
            {
            }
            //Reset.
            else if (steps == RESET_DELTA_STEPS)
            {
                _adjustedTickDelta = TickDelta;
                _lastStepsReceived = 0;
            }
            //Normal adjustment.
            else
            {
                double percent = (steps < 0) ? CLIENT_SPEEDUP_PERCENT : CLIENT_SLOWDOWN_PERCENT;
                double change = (steps * (percent * TickDelta));
                _adjustedTickDelta = MathFN.ClampDouble(_adjustedTickDelta + change, _clientTimingRange[0], _clientTimingRange[1]);
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
            //Update every x seconds.
            _timingAdjustmentInterval = (ushort)(TickRate * ADJUST_TIMING_INTERVAL);
            _clientTimingRange = new double[]
            {
                TickDelta * (1f - CLIENT_TIMING_PERCENT_RANGE),
                TickDelta * (1f + CLIENT_TIMING_PERCENT_RANGE)
            };
        }

        #region UNITY_EDITOR
        private void OnValidate()
        {
            TargetBufferedInputs = Math.Min(TargetBufferedInputs, _maximumBufferedInputs);
            SetInitialValues();
        }
        #endregion

    }

}