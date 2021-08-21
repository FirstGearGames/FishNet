using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Managing.Timing
{
    [DisallowMultipleComponent]
    public class TimeManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called right before a tick occurs.
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
        /// Current network tick converted to time.
        /// </summary>
        public double Time => TicksToTime(Tick);
        /// <summary>
        /// Current network tick.
        /// </summary>
        public uint Tick { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// True to disable auto physics simulation and simulate physics after each tick.
        /// </summary>
        [Tooltip("True to disable auto physics simulation and simulate physics after each tick.")]
        [SerializeField]
        private bool _manuallySimulatePhysics = false;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How many times per second the server will simulate; simulation rate is used for state control.")]
        [SerializeField]
        private ushort _simulationRate = 9999;
        /// <summary>
        /// How many times per second the server will simulate; simulation rate is used for state control.
        /// </summary>
        public ushort SimulationRate => _simulationRate;
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
        #endregion

        private void Awake()
        {
            AddNetworkLoops();
            _stopwatch.Restart();
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        internal void FirstInitialize(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            if (_manuallySimulatePhysics)
            {
                Physics.autoSimulation = false;
#if !UNITY_2020_2_OR_NEWER
                Physics2D.autoSimulation = false;
#else
                Physics2D.simulationMode = SimulationMode2D.Script;
#endif           
            }
        }

        /// <summary>
        /// Increases the based on simulation rate.
        /// </summary>
        private void IncreaseTick()
        {
            double timePerSimulation = 1d / SimulationRate;
            _elapsedTime += (_stopwatch.ElapsedMilliseconds / 1000d);

            while (_elapsedTime >= timePerSimulation)
            {
                OnPreTick?.Invoke(Tick);

                /* Iterate incoming before invoking OnTick.
                 * OnTick should be used by users to create
                 * logic based on read data. */
                _networkManager.TransportManager.IterateIncoming(true);
                _networkManager.TransportManager.IterateIncoming(false);

                Tick++;
                OnTick?.Invoke(Tick);

                if (_manuallySimulatePhysics)
                {
                    Physics.Simulate((float)timePerSimulation);
                    Physics2D.Simulate((float)timePerSimulation);
                }

                OnPostTick?.Invoke(Tick);
                _elapsedTime -= timePerSimulation;
            }

            _stopwatch.Restart();
        }

        /// <summary>
        /// Converts uint ticks to time.
        /// </summary>
        /// <param name="ticks"></param>
        /// <returns></returns>
        public float TicksToTime(uint ticks)
        {
            double timePerSimulation = 1d / SimulationRate;
            return (float)(timePerSimulation * ticks);
        }
        /// <summary>
        /// Converts float time to ticks.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public uint TimeToTicks(float time)
        {
            return (uint)Mathf.RoundToInt(time / (1f / SimulationRate));
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

    }

}