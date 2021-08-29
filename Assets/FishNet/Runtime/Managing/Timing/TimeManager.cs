using FishNet.Connection;
using FishNet.Managing.Timing.Broadcast;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Maximum number of excessive input sent from client before entries are dropped. Client is expected to send roughly one input per server tick.
        /// </summary>
        [Tooltip("Maximum number of excessive input sent from client before entries are dropped. Client is expected to send roughly one input per server tick.")]
        [Range(1, 255)]
        [SerializeField]
        private byte _maximumBufferedInputs = 10;
        /// <summary>
        /// Maximum number of excessive input sent from client before entries are dropped. Client is expected to send roughly one input per server tick.
        /// This is exposed until automatic state control is implemented.
        /// </summary>
        public byte MaximumBufferedInputs => _maximumBufferedInputs;
        /// <summary>
        /// How many ticks to wait between each step change. Lower this value for more precise tick synchronization between client and server at the cost of more bandwidth.
        /// </summary>
        [Tooltip("How many ticks to wait between each step change. Lower this value for more precise tick synchronization between client and server at the cost of more bandwidth.")]
        [Range(0, 255)]
        [SerializeField]
        private byte _simulationSyncInterval = 5;
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
        /// Broadcast cache sent to server to increase a clients buffer.
        /// </summary>
        private BufferIncreaseBroadcast _bufferIncrease = new BufferIncreaseBroadcast();
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
        /// Number of ticks left until a BufferIncreaseBroadcast is sent to server.
        /// </summary>
        private byte _ticksUntilBufferIncrease = 0;
        /// <summary>
        /// ClientTickData cache to prevent garbage allocation.
        /// </summary>
        private Stack<ClientTickData> _tickDataCache = new Stack<ClientTickData>();
        #endregion

        #region Const.
        /// <summary>
        /// Maximum percentage timing may vary from SimulationInterval for clients.
        /// </summary>
        private const float CLIENT_TIMING_PERCENT_RANGE = 0.35f;
        /// <summary>
        /// Percentage of SimulationRate to change AdjustedSimulationInterval per step.
        /// </summary>
        private const double STEP_PERCENT = 0.01d;
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
                _ticksUntilBufferIncrease = _simulationSyncInterval;

                _clientTimingRange = new double[]
                {
                    TickDelta * (1f - CLIENT_TIMING_PERCENT_RANGE),
                    TickDelta * (1f + CLIENT_TIMING_PERCENT_RANGE)
                };

                _networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
                _networkManager.ServerManager.RegisterBroadcast<BufferIncreaseBroadcast>(OnBufferIncrease, true);
                _networkManager.ClientManager.RegisterBroadcast<StepChangeBroadcast>(OnStepChange);
            }
        }



        /// <summary>
        /// Increases the based on simulation rate.
        /// </summary>
        private void IncreaseTick()
        {
            double timePerSimulation = (_networkManager.IsServer) ? TickDelta : _adjustedTickDelta;
            _elapsedTime += (_stopwatch.ElapsedMilliseconds / 1000d);

            bool ticked = (_elapsedTime >= timePerSimulation);
            while (_elapsedTime >= timePerSimulation)
            {
                OnPreTick?.Invoke(Tick);

                /* Iterate incoming before invoking OnTick.
                 * OnTick should be used by users to create
                 * logic based on read data. */
                _networkManager.TransportManager.IterateIncoming(true);
                _networkManager.TransportManager.IterateIncoming(false);

                OnTick?.Invoke(Tick);
                if (!_automaticPhysics)
                {
                    Physics.Simulate((float)TickDelta);
                    Physics2D.Simulate((float)TickDelta);
                }

                OnPostTick?.Invoke(Tick);
                _elapsedTime -= timePerSimulation;

                Tick++;
            }

            if (ticked && _useClientSidePrediction)
            {
                SendBufferIncrease();
                SendStepChanges();
            }

            _stopwatch.Restart();
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
            OnFixedUpdate?.Invoke();
        }

        /// <summary>
        /// Called when Update ticks. This is called before any other script.
        /// </summary>
        internal void TickUpdate()
        {
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
        /// Sends buffer increase to server.
        /// </summary>
        private void SendBufferIncrease()
        {
            if (_networkManager != null && !_networkManager.IsClient)
                return;

            _ticksUntilBufferIncrease--;
            //Only send when enough ticks have passed.
            if (_ticksUntilBufferIncrease > 0)
                return;
            _ticksUntilBufferIncrease = _simulationSyncInterval;

            _networkManager.ClientManager.Broadcast(_bufferIncrease, Channel.Unreliable);
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
                ctd.SendTicksRemaining = _simulationSyncInterval;

                /* If value is 1 or less then increase
                 * clients send rate. Ideally there will be two in queue
                 * before processing; this is where the - 2 comes from.
                 * 1 in queue would result in -1 step, where 0 in queue
                 * would result in -2 step. Lesser steps speed up client
                 * send rate more, while higher steps slow it down. */
                byte buffered = ctd.Buffered;

                sbyte step;
                if (buffered <= 1)
                    step = (sbyte)(buffered - 2);
                /* In all other scenarios value is either perfect
                 * or over. Over means the client is sending too fast.
                 * Where ideal is two buffered the code below will return
                 * a change of 0 if at two value. Otherwise it will return
                 * +1 for every value over. */
                else
                    step = (sbyte)(buffered);

                buffered = (byte)Math.Max(buffered - 1, 0);
                ctd.Buffered = buffered;

                //Wasteful to send a step if it's 0.
                if (step != 0)
                {
                    _clientStepChange.Step = step;
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
        /// Called on server when client sends BufferIncrease.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="stc"></param>
        private void OnBufferIncrease(NetworkConnection conn, BufferIncreaseBroadcast stc)
        {
            ClientTickData ctd;
            if (_bufferedClientInputs.TryGetValue(conn, out ctd))
            {
                byte buffered = ctd.Buffered;
                buffered = (byte)Math.Min(buffered + 1, MaximumBufferedInputs);
                ctd.Buffered = buffered;
            }
            else
            {
                if (_tickDataCache.Count == 0)
                    ctd = new ClientTickData();
                else
                    ctd = _tickDataCache.Pop();

                ctd.Buffered = 1;
                ctd.SendTicksRemaining = _simulationSyncInterval;
                _bufferedClientInputs[conn] = ctd;
            }
        }

        /// <summary>
        /// Adds onto AdjustedFixedDeltaTime.
        /// </summary>
        /// <param name="steps"></param>
        private void AddTiming(sbyte steps)
        {
            if (steps == 0)
                return;

            double change = (steps * (STEP_PERCENT * _tickRate));

            _adjustedTickDelta = (steps > 0) ?
                Math.Min(_adjustedTickDelta + change, _clientTimingRange[1]) :
                Math.Max(_adjustedTickDelta + change, _clientTimingRange[0]);
        }

    }

}