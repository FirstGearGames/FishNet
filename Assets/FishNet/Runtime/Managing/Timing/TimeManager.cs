using FishNet.Connection;
using FishNet.Managing.Timing.Broadcast;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Timing
{
    [DisallowMultipleComponent]
    public class TimeManager : MonoBehaviour
    {
        #region Types.        
        private class ClientTickData
        {
            public byte Buffered;
            public byte SendTicksRemaining;

            public ClientTickData(byte buffered)
            {
                Reset(buffered);
            }

            public void Reset(byte buffered)
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
        /// Current network tick converted to time. This doesn't do anything. Don't use this.
        /// </summary>
        public double Time => TicksToTime(Tick);
        /// <summary>
        /// Current network tick. Not yet used.
        /// </summary>
        public uint Tick { get; private set; }
        /// <summary>
        /// DeltaTime for TickRate.
        /// </summary>
        public double TickDelta = 0;
        #endregion

        #region Serialized.
        /// <summary>
        /// True to let Unity run physics. False to let TimeManager run physics after each tick.
        /// </summary>
        [Tooltip("True to let Unity run physics. False to let TimeManager run physics after each tick.")]
        [SerializeField]
        private bool _automaticPhysics = true;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How many times per second the server will simulate")]
        [Range(1, 500)]
        [SerializeField]
        private ushort _tickRate = 500;
        /// <summary>
        /// True to enable support for client side prediction. Leaving this false when CSP is not needed will save a small amount of bandwidth and CPU.
        /// </summary>
        [Tooltip("True to enable support for client side prediction. Leaving this false when CSP is not needed will save a small amount of bandwidth and CPU.")]
        [SerializeField]
        private bool _useClientSidePrediction = false;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum number of buffered inputs which will be accepted from client before old inputs are discarded.")]
        [Range(1, 255)]
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
        [Range(1, 255)]
        [SerializeField]
        private byte _targetBufferedInputs = 3;
        /// <summary>
        /// True to enable more accurate tick synchronization between client and server at the cost of bandwidth.
        /// </summary>
        [Tooltip("True to enable more accurate tick synchronization between client and server at the cost of bandwidth.")]
        [SerializeField]
        private bool _aggressiveTiming = false;
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
        private StepChangeBroadcast _clientStepChange = new StepChangeBroadcast();
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
        /// Percentage of TickDelta client will adjust their timing per step.
        /// </summary>
        private double _clientStepPercent => (_aggressiveTiming) ? 0.0005d : 0.003d;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum percentage timing may vary from SimulationInterval for clients.
        /// </summary>
        private const float CLIENT_TIMING_PERCENT_RANGE = 0.3f;
        #endregion

        private void Awake()
        {
            AddNetworkLoops();
            _stopwatch.Restart();
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
        /// Sets number of inputs buffered for a connection. Will use whichever is higher between current value and bufferedCount.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="bufferedCount"></param>
        public void SetBuffered(NetworkConnection connection, byte bufferedCount)
        {
            //Connection found.
            if (_bufferedClientInputs.TryGetValue(connection, out ClientTickData ctd))
            {
                ctd.Buffered = Math.Max(ctd.Buffered, bufferedCount);
            }
            //Not found, make new entry.
            else
            {
                ClientTickData newCtd;
                if (_tickDataCache.Count == 0)
                {
                    newCtd = new ClientTickData(bufferedCount);
                }
                else
                {
                    newCtd = _tickDataCache.Pop();
                    newCtd.Reset(bufferedCount);
                }

                _bufferedClientInputs[connection] = newCtd;
            }
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        internal void FirstInitialize(NetworkManager networkManager)
        {
            TickDelta = (1d / _tickRate);
            _adjustedTickDelta = TickDelta;
            _networkManager = networkManager;

            if (!_automaticPhysics)
            {
                Physics.autoSimulation = false;
#if !UNITY_2020_2_OR_NEWER
                Physics2D.autoSimulation = false;
#else
                Physics2D.simulationMode = SimulationMode2D.Script;
#endif
            }

            if (_useClientSidePrediction)
            {
                _clientTimingRange = new double[]
                {
                    TickDelta * (1f - CLIENT_TIMING_PERCENT_RANGE),
                    TickDelta * (1f + CLIENT_TIMING_PERCENT_RANGE)
                };

                _networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
                _networkManager.ClientManager.RegisterBroadcast<StepChangeBroadcast>(OnStepChange);
            }
        }

        long _lastE;
        /// <summary>
        /// Increases the based on simulation rate.
        /// </summary>
        private void IncreaseTick()
        {
            double timePerSimulation = (_networkManager.IsServer) ? TickDelta : _adjustedTickDelta;
            long thisMs = _stopwatch.ElapsedMilliseconds;
            long frameMs = thisMs - _lastE;
            _lastE = thisMs;

            _elapsedTime += frameMs / 1000d;
            //_stopwatch.Restart();

            while (_elapsedTime >= timePerSimulation)
            {
                OnPreTick?.Invoke(Tick);

                /* Iterate incoming before invoking OnTick.
                 * OnTick should be used by users to create
                 * logic based on read data. */
                //_networkManager.TransportManager.IterateIncoming(true);
                //_networkManager.TransportManager.IterateIncoming(false);

                OnTick?.Invoke(Tick);
                if (!_automaticPhysics)
                {
                    Physics.Simulate((float)TickDelta);
                    Physics2D.Simulate((float)TickDelta);
                }

                OnPostTick?.Invoke(Tick);
                _elapsedTime -= timePerSimulation;

                if (_useClientSidePrediction)
                    SendStepChanges();

                Tick++;
            }
        }


        /// <summary>
        /// Called when a client state changes with the server.
        /// </summary>
        private void ServerManager_OnRemoteConnectionState(NetworkConnection arg1, RemoteConnectionStateArgs arg2)
        {
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
        /// Converts uint ticks to time.
        /// </summary>
        /// <param name="ticks"></param>
        /// <returns></returns>
        public float TicksToTime(uint ticks)
        {
            double timePerSimulation = 1d / _tickRate;
            return (float)(timePerSimulation * ticks);
        }
        /// <summary>
        /// Converts float time to ticks.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public uint TimeToTicks(float time)
        {
            return (uint)Mathf.RoundToInt(time / (float)TickDelta);
        }


        /// <summary>
        /// Called when FixedUpdate ticks. This is called before any other script.
        /// </summary>
        internal void TickFixedUpdate()
        {
            _networkManager.TransportManager.IterateIncoming(true);
            _networkManager.TransportManager.IterateIncoming(false);
            OnFixedUpdate?.Invoke();
        }

        /// <summary>
        /// Called when Update ticks. This is called before any other script.
        /// </summary>
        internal void TickUpdate()
        {
            if (!UnityEngine.Time.inFixedTimeStep)
            {
                _networkManager.TransportManager.IterateIncoming(true);
                _networkManager.TransportManager.IterateIncoming(false);
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
        /// Sends step changes to clients.
        /// </summary>
        private void SendStepChanges()
        {
            if (!_networkManager.IsServer)
                return;

            foreach (KeyValuePair<NetworkConnection, ClientTickData> item in _bufferedClientInputs)
            {
                ClientTickData ctd = item.Value;
                ctd.SendTicksRemaining--;

                if (ctd.SendTicksRemaining > 0)
                    continue;

                /* If using aggressive timing then
                 * send step every tick. */
                if (_aggressiveTiming)
                    ctd.SendTicksRemaining = 1;
                else
                    ctd.SendTicksRemaining = (byte)Math.Min((_tickRate / 7), 255);

                /* If value is 1 or less then increase
                 * clients send rate. Ideally there will be two in queue
                 * before processing; this is where the - 2 comes from.
                 * 1 in queue would result in -1 step, where 0 in queue
                 * would result in -2 step. Lesser steps speed up client
                 * send rate more, while higher steps slow it down. */
                byte buffered = ctd.Buffered;

                sbyte steps = (sbyte)(buffered - _targetBufferedInputs);
                buffered = (byte)Math.Max(buffered - 1, 0);
                ctd.Buffered = buffered;

                //Wasteful to send a step if it's 0.
                if (steps != 0)
                {
                    _clientStepChange.Step = steps;
                    _networkManager.ServerManager.Broadcast(item.Key, _clientStepChange, true, Channel.Unreliable);
                }
            }
        }


        /// <summary>
        /// Called on client when server sends StepChange.
        /// </summary>
        /// <param name="ctc"></param>
        private void OnStepChange(StepChangeBroadcast ctc)
        {
            if (_networkManager.IsServer)
                return;

            AddTiming(ctc.Step);
        }

        /// <summary>
        /// Adds onto AdjustedFixedDeltaTime.
        /// </summary>
        /// <param name="steps"></param>
        private void AddTiming(sbyte steps)
        {
            if (steps == 0)
                return;

            //Use lower step percent when stepping up.
            double change = (steps * (_clientStepPercent * TickDelta));
            _adjustedTickDelta += change;
            //clamp range.
            if (_adjustedTickDelta < _clientTimingRange[0])
                _adjustedTickDelta = _clientTimingRange[0];
            else if (_adjustedTickDelta > _clientTimingRange[1])
                _adjustedTickDelta = _clientTimingRange[1];
        }

        #region UNITY_EDITOR
        private void OnValidate()
        {
            _targetBufferedInputs = Math.Min(_targetBufferedInputs, _maximumBufferedInputs);
        }
        #endregion

    }

}