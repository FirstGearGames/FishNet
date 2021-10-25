using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Timing.Broadcast;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace FishNet.Managing.Timing
{
    /// <summary>
    /// Provides data and actions for network time and tick based systems.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeManager : MonoBehaviour
    {
        #region Types.        
        private class ClientTickData
        {
            public ushort Buffered;
            public ushort SendTicksRemaining;

            public ClientTickData(ushort buffered)
            {
                Reset(buffered);
            }

            public void Reset(ushort buffered)
            {
                Buffered = buffered;
                SendTicksRemaining = 1;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Called right before a tick occurs, as well before data is read.
        /// </summary>
        public event Action<uint> OnPreTick;
        /// <summary>
        /// Called when a tick occurs.
        /// </summary>
        public event Action<uint> OnTick;
        /// <summary>
        /// Called after a tick occurs; physics would have simulated if using ManuallySimulatePhysics.
        /// </summary>
        public event Action<uint> OnPostTick;
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
        /// Tick on the last received packet, be it from server or client.
        /// This value isn't used yet.
        /// </summary>
        public uint LastPacketTick { get; internal set; }
        /// <summary>
        /// Current network tick.
        /// When running as client only this is an approximation to what the server tick is.
        /// The value of this field may decrease if the client needs to adjust timing.
        /// Tick can be used to get the server time by using TicksToTime().
        /// </summary>
        public uint Tick { get; private set; }
        /// <summary>
        /// DeltaTime for TickRate.
        /// </summary>
        [HideInInspector]
        public double TickDelta { get; private set; } = 0d;
        #endregion

        #region Serialized.
        [Header("Timing")]
        /// <summary>
        /// True to only send and receive data on ticks. False to do so whenever data is available.
        /// </summary>
        [Tooltip("True to only send and receive data on ticks. False to do so whenever data is available.")]
        [SerializeField]
        private bool _transportOnTick = false;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How many times per second the server will simulate. This does not limit server frame rate.")]
        [Range(1, 340)]
        [SerializeField]
        private ushort _tickRate = 120;
        /// <summary>
        /// How many times per second the server will simulate. This does not limit server frame rate.
        /// </summary>
        public ushort TickRate => _tickRate;


        [Header("Prediction")]
        /// <summary>
        /// True to let Unity run physics. False to let TimeManager run physics after each tick.
        /// </summary>
        [Tooltip("True to let Unity run physics. False to let TimeManager run physics after each tick.")]
        [SerializeField]
        private bool _automaticPhysics = true;
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
        /// Number of inputs server prefers to have buffered from clients.
        /// </summary>
        [Tooltip("Number of inputs server prefers to have buffered from clients.")]
        [Range(1, 100)]
        [SerializeField]
        private byte _targetBufferedInputs = 2;
        #endregion

        #region Private.
        /// <summary>
        /// Stopwatch used to calculate ticks.
        /// </summary>
        System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        /// <summary>
        /// Time elapsed after ticks. This is extra time beyond the simulation rate.
        /// </summary>
        private double _elapsedTime = 0f;
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
        /// How often to update timing on clients.
        /// </summary>
        private ushort _timingAdjustmentInterval = 0;
        /// <summary>
        /// Last frame an iteration occurred for incoming.
        /// </summary>
        private int _lastIncomingIterationFrame = -1;
        /// <summary>
        /// Last frame an iteration occurred for outgoing.
        /// </summary>
        private int _lastOutgoingIterationFrame = -1;
        /// <summary>
        /// Number of TimeManagers open which are using manual physics.
        /// </summary>
        private static uint _manualPhysics = 0;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum percentage timing may vary from SimulationInterval for clients.
        /// </summary>
        private const float CLIENT_TIMING_PERCENT_RANGE = 0.3f;
        /// <summary>
        /// Percentage of TickDelta client will adjust their timing per step.
        /// </summary>
        private const double CLIENT_STEP_PERCENT = 0.02d;
        /// <summary>
        /// How quickly to move AdjustedDeltaTick to DeltaTick.
        /// </summary>
        private const double ADJUSTED_DELTA_RECOVERY_RATE = 0.002f;
        #endregion

        private void Awake()
        {
            AddNetworkLoops();
            _stopwatch.Restart();
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            //If exiting playmode unset instantiated.
            if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.isPlaying)
                _manualPhysics = 0;
            else if (!_automaticPhysics)
                _manualPhysics = Math.Max(0, _manualPhysics - 1);
        }
#endif

        /// <summary>
        /// Called when FixedUpdate ticks. This is called before any other script.
        /// </summary>
        internal void TickFixedUpdate()
        {
            TryIterate(true, false);
            OnFixedUpdate?.Invoke();
        }

        /// <summary>
        /// Called when Update ticks. This is called before any other script.
        /// </summary>
        internal void TickUpdate()
        {
            if (!Time.inFixedTimeStep)
                TryIterate(true, false);

            if (_networkManager.IsClient)
                _adjustedTickDelta = Mathf.MoveTowards((float)_adjustedTickDelta, (float)TickDelta, Time.deltaTime * (float)ADJUSTED_DELTA_RECOVERY_RATE);

            IncreaseTick();
            OnUpdate?.Invoke();
        }

        /// <summary>
        /// Called when LateUpdate ticks. This is called after all other scripts.
        /// </summary>
        internal void TickLateUpdate()
        {
            OnLateUpdate?.Invoke();
            /* Iterate outgoing after lateupdate
             * so data is always sent out
             * after everything processes. */
            TryIterate(false, false);
        }


        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        internal void InitializeOnce(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            SetInitialValues();
            _networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
            _networkManager.ServerManager.OnAuthenticationResult += ServerManager_OnAuthenticationResult;
            _networkManager.ClientManager.RegisterBroadcast<TimingAdjustmentBroadcast>(OnTimingAdjustmentBroadcast);
            _networkManager.ServerManager.RegisterBroadcast<AddBufferedBroadcast>(OnAddBufferedBroadcast);
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
        /// Sets values to use based on settings.
        /// </summary>
        private void SetInitialValues()
        {
            TickDelta = (1d / TickRate);
            _adjustedTickDelta = TickDelta;

            SetAutomaticSimulation(_automaticPhysics);

            _clientTimingRange = new double[]
            {
                TickDelta * (1f - CLIENT_TIMING_PERCENT_RANGE),
                TickDelta * (1f + CLIENT_TIMING_PERCENT_RANGE)
            };
        }

        /// <summary>
        /// Updates automaticSimulation modes.
        /// </summary>
        /// <param name="automatic"></param>
        private void SetAutomaticSimulation(bool automatic)
        {
            //Do not automatically simulate.
            if (!automatic)
            {
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
                Physics.autoSimulation = true;
#if !UNITY_2020_2_OR_NEWER
                Physics2D.autoSimulation = true;
#else
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
#endif
            }
        }
        /// <summary>
        /// Increases the based on simulation rate.
        /// </summary>
        private void IncreaseTick()
        {
            double timePerSimulation = (_networkManager.IsServer) ? TickDelta : _adjustedTickDelta;
            long frameMs = _stopwatch.ElapsedMilliseconds;

            _elapsedTime += frameMs / 1000d;
            _stopwatch.Restart();
            bool ticked = (_elapsedTime >= timePerSimulation);

            while (_elapsedTime >= timePerSimulation)
            {
                OnPreTick?.Invoke(Tick);
                /* This has to be called inside the loop because
                 * OnPreTick promises data hasn't been read yet.
                 * Therefor iterate must occur after OnPreTick.
                 * Iteration will only run once per frame. */
                TryIterate(true, true);
                OnTick?.Invoke(Tick);

                if (!_automaticPhysics)
                {
                    Physics.Simulate((float)TickDelta);
                    Physics2D.Simulate((float)TickDelta);
                }

                OnPostTick?.Invoke(Tick);
                _elapsedTime -= timePerSimulation;

                if (_networkManager.IsClient)
                    SendAddBuffered();
                if (_networkManager.IsServer)
                    SendTimingAdjustment();

                Tick++;
            }

            //If ticked also try to send out data.
            if (ticked)
                TryIterate(false, true);
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

            //AddToBuffered(arg1, _timingAdjustmentInterval);
            ClientTickData ctd = AddToBuffered(arg1, 0);
            ctd.SendTicksRemaining = _timingAdjustmentInterval;
            SynchronizeTick(arg1);
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
        /// Converts time to ticks.
        /// </summary>
        /// <param name="time">Time to convert.</param>
        /// <returns></returns>
        public uint TimeToTicks(float time, TickRounding rounding = TickRounding.RoundNearest)
        {
            double result = ((double)time / TickDelta);

            if (rounding == TickRounding.RoundNearest)
                return (uint)Math.Round(result);
            else if (rounding == TickRounding.RoundDown)
                return (uint)Math.Floor(result);
            else
                return (uint)Math.Ceiling(result);
        }

        /// <summary>
        /// Tries to iterate incoming data.
        /// </summary>
        /// <param name="incoming">True to iterate incoming.</param>
        /// <param name="isTick">True if call is occuring during a tick.</param>
        private void TryIterate(bool incoming, bool isTick)
        {
            /* If only iterating on ticks then data should
             * only be read or sent during a tick.
             * Otherwise, data will be handled immediately,
             * outside the tick loop. */
            if (isTick && !_transportOnTick)
                return;
            else if (!isTick && _transportOnTick)
                return;

            int frameCount = Time.frameCount;
            int lastTickFrame = (incoming) ? _lastIncomingIterationFrame : _lastOutgoingIterationFrame;
            //Already iterated this tick.
            if (frameCount == lastTickFrame)
                return;

            if (incoming)
            {
                _lastIncomingIterationFrame = frameCount;
                _networkManager.TransportManager.IterateIncoming(true);
                _networkManager.TransportManager.IterateIncoming(false);
            }
            else
            {
                _lastOutgoingIterationFrame = frameCount;
                _networkManager.TransportManager.IterateOutgoing(true);
                _networkManager.TransportManager.IterateOutgoing(false);
            }
        }


        #region Timing adjusting.
        /// <summary>
        /// Sets number of inputs buffered for a connection.
        /// </summary>
        private ClientTickData AddToBuffered(NetworkConnection connection, uint count = 1)
        {
            //Connection found.
            if (_bufferedClientInputs.TryGetValue(connection, out ClientTickData ctd))
            {
                //Cap value to ensure clients cannot cause an overflow attack.
                uint next = ctd.Buffered + count;
                if (next > ushort.MaxValue)
                    next = ushort.MaxValue;
                ctd.Buffered = (ushort)next;

                return ctd;
            }
            //Not found, make new entry.
            else
            {
                ClientTickData newCtd;
                if (_tickDataCache.Count == 0)
                {
                    newCtd = new ClientTickData((ushort)count);
                }
                else
                {
                    newCtd = _tickDataCache.Pop();
                    newCtd.Reset((ushort)count);
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
                ctd.SendTicksRemaining--;

                if (ctd.SendTicksRemaining > 0)
                    continue;

                ctd.SendTicksRemaining = _timingAdjustmentInterval;

                /* If value is 1 or less then increase
                 * clients send rate. Ideally there will be two+ in queue
                 * before processing; this is where the - 2 comes from.
                 * 1 in queue would result in -1 step, where 0 in queue
                 * would result in -2 step. Lesser steps speed up client
                 * send rate more, while higher steps slow it down. */
                int buffered = (ctd.Buffered - _timingAdjustmentInterval);
                sbyte steps = MathFN.ClampSByte((buffered - _targetBufferedInputs), -2, (sbyte)_maximumBufferedInputs);
                ctd.Buffered = 0;

                _timeAdjustment.Tick = Tick;
                _timeAdjustment.Step = steps;
                _networkManager.ServerManager.Broadcast(item.Key, _timeAdjustment, true, Channel.Unreliable);
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

            Tick = ta.Tick;
            sbyte steps = ta.Step;
            if (steps == 0)
                return;

            double change = (steps * (CLIENT_STEP_PERCENT * TickDelta));
            _adjustedTickDelta = MathFN.ClampDouble(_adjustedTickDelta + change, _clientTimingRange[0], _clientTimingRange[1]);
        }
        #endregion

        #region UNITY_EDITOR
        private void OnValidate()
        {
            _targetBufferedInputs = Math.Min(_targetBufferedInputs, _maximumBufferedInputs);
            SetInitialValues();
        }
        #endregion

    }

}