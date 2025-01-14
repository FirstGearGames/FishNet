using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

/* Note: the graphical object for this predicted NetworkObject is unset.
* This is because currently the NetworkObject only allows setting of one
 * graphical object, but there are three things that move independently.
 *
 * In version 4.5.8 there will be an option to support multiple graphical objects
 * and this demo will be updated. There are plans to release 4.5.8 with only this improvement
 * to get the update out fast as possible. */

namespace FishNet.Demo.Prediction.Rigidbodies
{
    public class RigidbodyPrediction : TickNetworkBehaviour
    {
        #region Types.
        public struct ReplicateData : IReplicateData
        {
            public ReplicateData(Vector2 input, bool fire)
            {
                Input = input;
                Fire = fire;

                _tick = 0;
            }

            /// <summary>
            /// Current movement directions held.
            /// </summary>
            public Vector2 Input;
            /// <summary>
            /// True to fire.
            /// </summary>
            public bool Fire;

            /// <summary>
            /// Tick is set at runtime. There is no need to manually assign this value.
            /// </summary>
            private uint _tick;

            public void Dispose() { }

            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public ReconcileData(PredictionRigidbody root, PredictionRigidbody frontWheel, PredictionRigidbody rearWheel, uint boostStartTick)
            {
                Root = root;
                FrontWheel = frontWheel;
                RearWheel = rearWheel;
                BoostStartTick = boostStartTick;

                _tick = 0;
            }

            /// <summary>
            /// PredictionRigidbody on the root.
            /// </summary>
            public PredictionRigidbody Root;
            /// <summary>
            /// PredictionRigidbody controlling the front wheel.
            /// </summary>
            public PredictionRigidbody FrontWheel;
            /// <summary>
            /// PredictionRigidbody controlling the rear wheel.
            /// </summary>
            public PredictionRigidbody RearWheel;
            /// <summary>
            /// Tick which the boost started.
            /// </summary>
            public uint BoostStartTick;

            /// <summary>
            /// Tick is set at runtime. There is no need to manually assign this value.
            /// </summary>
            private uint _tick;

            /* You do not need to dispose PredictionRigidbody when used with prediction.
             * These references will automatically use pooling to prevent garbage allocations! */
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        [SerializeField]
        private Rigidbody _frontWheelRigidbody;
        [SerializeField]
        private Rigidbody _rearWheelRigidbody;

        [SerializeField]
        private float _boostDuration = 1f;
        [SerializeField]
        private float _boostForce = 20f;

        [SerializeField]
        private float _moveRate = 4f;
        [SerializeField]
        private float _turnRate = 4f;

        /// <summary>
        /// Root of the vehicle.
        /// </summary>
        private PredictionRigidbody _root = new();
        /// <summary>
        /// Drives turning (front wheels).
        /// </summary>
        private PredictionRigidbody _frontWheel = new();
        /// <summary>
        /// Drives acceleration (rear wheels).
        /// </summary>
        private PredictionRigidbody _rearWheel = new();

        /// <summary>
        /// Tick which the boost started.
        /// </summary>
        private uint _boostStartTick = TimeManager.UNSET_TICK;
        /// <summary>
        /// Tick on the last replicate.
        /// </summary>
        private uint _lastReplicateTick;
        /// <summary>
        /// Next tick the controller is allowed to predicted fire.
        /// </summary>
        private uint _nextAllowedFireTick;
        /// <summary>
        /// Last data passed into replicate which was created.
        /// </summary>
        private ReplicateData _lastCreatedReplicateData = default;

        /// <summary>
        /// Allow firing every 50 ticks.
        /// </summary>
        private const uint FIRE_INTERVAL = 50;
        
        private void Awake()
        {
            _root.Initialize(GetComponent<Rigidbody>());
            _frontWheel.Initialize(_frontWheelRigidbody);
            _rearWheel.Initialize(_rearWheelRigidbody);
        }

        public override void OnStartNetwork()
        {
            //Rigidbodies need tick and postTick.
            base.SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildMoveData());
            CreateReconcile();
        }

        /// <summary>
        /// Returns replicate data to send as the controller.
        /// </summary>
        private ReplicateData BuildMoveData()
        {
            /* Only the controller needs to build move data.
             * This could be the server if the server if no owner, for example
             * such as AI, or the owner of the object. */
            if (!base.IsOwner) return default;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            //To keep things simple firing is done by holding left shift.
            bool fire = Input.GetKey(KeyCode.LeftShift);
            
            ReplicateData md = new(new(horizontal, vertical), fire);

            return md;
        }

        /// <summary>
        /// Creates a reconcile that is sent to clients.
        /// </summary>
        public override void CreateReconcile()
        {
            /* Both the server and client should create reconcile data.
             * The client will use their copy as a fallback if they do not
             * get data from the server, such as a dropped packet.
             *
             * The client will not reconcile unless it receives at least one
             * reconcile packet from the server for the tick. */

            /* You do not have to reconcile every tick if you wish to
             * save bandwidth/perf, or simply feel as though it's not needed
             * for your game type.
             *
             * Even when not reconciling every tick it's still recommended
             * to build the reconcile as client; this cost is very little.*/

            /* This is an example of only sending a reconcile occasionally
             * if the server. Simply uncomment the if statement below to
             * test this behavior. */
            // if (base.IsServerStarted)
            // {
            //     //Exit early if 10 ticks have not passed.
            //     if (base.TimeManager.LocalTick % 10 != 0) return;
            // }

            //Build the data using current information and call the reconcile method.
            ReconcileData rd = new(_root, _frontWheel, _rearWheel, _boostStartTick);
            PerformReconcile(rd);
        }

        [Replicate]
        private void PerformReplicate(ReplicateData rd, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            uint rdTick = rd.GetTick();
            _lastReplicateTick = rdTick;
            /* Since rigidbodies typically carry inertia you do not necessarily need
             * to predict in the future; rigidbodies will continue to move along
             * the same path anyway.
             *
             * You can still predict inputs a couple ticks like we did in the
             * CharacterController example, if you find doing so creates
             * better results. */

            Vector3 turningForce = new(rd.Input.x * _turnRate, 0f, 0f);
            Vector3 forwardForce = new(0f, 0f, rd.Input.y * _moveRate);
            /* If boostStartTick is not unset then a boost is started.
             *
             * Make sure that the current data tick is at least equal
             * to boost tick before adding boost.
             * This is done in the scenario a boost happened outside replay,
             * we don't want to boost during a replay before the boost started. */
            if (_boostStartTick != TimeManager.UNSET_TICK && rdTick <= _boostStartTick)
            {
                //Add boost to forward force.                
                forwardForce += new Vector3(0f, 0f, _boostForce);

                uint boostTimeToTicks = base.TimeManager.TimeToTicks(_boostDuration, TickRounding.RoundUp);
                //This is when boost will end.
                uint endTick = (_boostStartTick + boostTimeToTicks);

                //Unset boost if tick is met.
                if (rdTick >= endTick)
                    _boostStartTick = TimeManager.UNSET_TICK;
            }
            
            //Convert forwards based on root forward.
            Transform rootTransform = _root.Rigidbody.transform;
            turningForce = rootTransform.TransformDirection(turningForce);
            forwardForce = rootTransform.TransformDirection(forwardForce);
            
            //Flip turning if vehicle is also flipped.
            if (rootTransform.up.y <= -0.1f)
                turningForce *= -1f;
            
            /* Add turning and forward force.
             *
             * Notice that forces are NOT multiplied by
             * delta. Just like Unity physics, predictionRigidbodies
             * do not include delta in calculated forces. */
            _frontWheel.AddForce(turningForce);
            _rearWheel.AddForce(forwardForce);

            _root.Simulate();
            _frontWheel.Simulate();
            _rearWheel.Simulate();
        }

        [Reconcile]
        private void PerformReconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
        {
            /* Reconcile boosted start tick. Even though the NetworkTrigger will
             * invoke again if a replayed replicate pushes the vehicle through
             * the trigger, it will not if the vehicle is in the trigger before the reconcile,
             * as well after; since they never left, enter will not be called.*/
            _boostStartTick = rd.BoostStartTick;
            //Reconcile all the rigidbodies.
            _root.Reconcile(rd.Root);
            _frontWheel.Reconcile(rd.FrontWheel);
            _rearWheel.Reconcile(rd.RearWheel);
        }

        /// <summary>
        /// Sets boosted state for a number of ticks.
        /// </summary>
        public void SetBoosted()
        {
            /* Boost start is set to whatever tick was last replicated.
             * Replicate is called every tick, so if the controller hits
             * the collider during a replay the tick will be whatever replicate
             * is being replayed, if outside a replay it will be the current
             * tick.
             *
             * If owner or server the current tick would be localTick, otherwise
             * it will be server tick. */
            _boostStartTick = _lastReplicateTick;
        }
    }
}