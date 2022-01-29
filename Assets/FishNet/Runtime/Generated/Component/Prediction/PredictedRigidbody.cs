using FishNet.Documenting;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    /// <summary>
    /// //TODO THIS IS A WORK IN PROGRESS.
    /// </summary>
    [APIExclude]
    public class PredictedRigidbody : NetworkBehaviour
    {
        #region Type.
        public struct RigidbodyState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// How often to synchronize values from server to clients.
        /// </summary>
        [Tooltip("How often to synchronize values from server to clients.")]
        [SerializeField]
        private float _sendInterval = 1f;
        /// <summary>
        /// How much to predict movement. A Value of 1f will result in this object moving at the same rate as it's last known value. A value of 0f will disable the prediction.
        /// </summary>
        [Tooltip("How much to predict movement. A Value of 1f will result in this object moving at the same rate as it's last known value. A value of 0f will disable the prediction.")]
        [Range(0f, 1f)]
        //[SerializeField]
        private float _predictionRatio = 0.9f;
        #endregion

        #region Private.
        /// <summary>
        /// Rigidbodies to predict.
        /// </summary>
        private Rigidbody _rigidbody;
        /// <summary>
        /// Last SpectatorMotorState received from the server.
        /// </summary>
        private RigidbodyState? _receivedRigidbodyState;
        /// <summary>
        /// Velocity from previous simulation.
        /// </summary>
        private Vector3 _lastVelocity;
        /// <summary>
        /// Angular velocity from previous simulation.
        /// </summary>
        private Vector3 _lastAngularVelocity;
        /// <summary>
        /// Baseline for velocity magnitude.
        /// </summary>
        private float? _velocityBaseline;
        /// <summary>
        /// Baseline for angular velocity magnitude.
        /// </summary>
        private float? _angularVelocityBaseline;
        /// <summary>
        /// PhysicsScene for this object when OnPreReconcile is called.
        /// </summary>
        private PhysicsScene _physicsScene;
        /// <summary>
        /// Next tick to send data.
        /// </summary>
        private uint _nextSendTick;
        /// <summary>
        /// Number of ticks prediction occurred since last replay.
        /// </summary>
        private uint _predictedTicks;
        #endregion

        private void Awake()
        {
            InitializeOnce();
        }

        private void OnEnable()
        {
            InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        private void OnDisable()
        {
            if (base.TimeManager != null)
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            base.TimeManager.OnPreReconcile += TimeManager_OnPreReconcile;
            base.TimeManager.OnPostReplicateReplay += TimeManager_OnPostReplicateReplay;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            base.TimeManager.OnPreReconcile -= TimeManager_OnPreReconcile;
            base.TimeManager.OnPostReplicateReplay -= TimeManager_OnPostReplicateReplay;

        }

        private void TimeManager_OnPostTick()
        {
            if (base.IsServer)
            {

                if (base.TimeManager.LocalTick >= _nextSendTick || base.TransformMayChange())
                {
                    uint ticksRequired = base.TimeManager.TimeToTicks(_sendInterval, TickRounding.RoundUp);
                    _nextSendTick += ticksRequired;
                    SendRigidbodyState();
                }
            }

            if (CanPredict())
            {
                //_predictedTicks++;
                PredictVelocity(gameObject.scene.GetPhysicsScene());
            }
        }


        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        private void InitializeOnce()
        {
            _predictionRatio = 1f;
            _rigidbody = GetComponent<Rigidbody>();
        }


        private void TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            if (!CanPredict())
                return;

            _physicsScene = gameObject.scene.GetPhysicsScene();
            if (_physicsScene == obj.gameObject.scene.GetPhysicsScene())
                ResetRigidbodyToData();
        }

        /// <summary>
        /// Returns if prediction can be used on this rigidbody.
        /// </summary>
        /// <returns></returns>
        private bool CanPredict()
        {
            if (base.IsServer || base.IsOwner || _predictionRatio <= 0f)
                return false;

            return true;
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void TimeManager_OnPostReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d)
        {
            if (CanPredict())
                PredictVelocity(ps);
        }

        /// <summary>
        /// Resets the rigidbody to last known data.
        /// </summary>
        private void ResetRigidbodyToData()
        {
            if (_receivedRigidbodyState == null)
                return;

            //Update transform and rigidbody.
            transform.position = _receivedRigidbodyState.Value.Position;
            transform.rotation = _receivedRigidbodyState.Value.Rotation;
            _rigidbody.velocity = _receivedRigidbodyState.Value.Velocity;
            _rigidbody.angularVelocity = _receivedRigidbodyState.Value.AngularVelocity;
            //Set prediction defaults.
            _velocityBaseline = null;
            _angularVelocityBaseline = null;
            _lastVelocity = _rigidbody.velocity;
            _lastAngularVelocity = _rigidbody.angularVelocity;
        }

        /// <summary>
        /// Tries to predict velocity.
        /// </summary>
        private void PredictVelocity(PhysicsScene ps)
        {
            if (ps != _physicsScene)
                return;

            PredictVelocity(ref _velocityBaseline, ref _lastVelocity, _rigidbody.velocity, false);
            PredictVelocity(ref _angularVelocityBaseline, ref _lastAngularVelocity, _rigidbody.angularVelocity, true);

            /// <summary>
            /// Tries to predict velocity.
            /// </summary>
            void PredictVelocity(ref float? velocityBaseline, ref Vector3 lastVelocity, Vector3 velocity, bool angular)
            {
                float velocityDifference;
                float directionDifference;

                /* Velocity. */
                directionDifference = (velocityBaseline != null) ?
                    Vector3.SqrMagnitude(lastVelocity.normalized - velocity.normalized) :
                    0f;
                //If direction has changed too much then reset the baseline.
                if (directionDifference > 0.01f)
                {
                    velocityBaseline = null;
                }
                //Direction hasn't changed enough to reset baseline.
                else
                {
                    //Difference in velocity since last simulation.
                    velocityDifference = Vector3.Magnitude(lastVelocity - velocity);
                    //If there is no baseline.
                    if (velocityBaseline == null)
                    {
                        if (velocityDifference > 0)
                            velocityBaseline = velocityDifference;
                    }
                    //If there is a baseline.
                    else
                    {
                        //If the difference exceeds the baseline by 10% then reset baseline so another will be calculated.
                        if (velocityDifference > (velocityBaseline.Value * 1.1f) || velocityDifference < (velocityBaseline.Value * 0.9f))
                        {
                            velocityBaseline = null;
                        }
                        //Velocity difference is close enough to the baseline to where it doesn't need to be reset, so use prediction.
                        else
                        {
                            if (!angular)
                                _rigidbody.velocity = Vector3.Lerp(velocity, lastVelocity, _predictionRatio);
                            else
                                _rigidbody.angularVelocity = Vector3.Lerp(velocity, lastVelocity, _predictionRatio);
                        }
                    }
                }
            }

            _lastVelocity = _rigidbody.velocity;
            _lastAngularVelocity = _rigidbody.angularVelocity;
        }


        /// <summary>
        /// Sends current states of this object to client.
        /// </summary>
        private void SendRigidbodyState()
        {
            RigidbodyState state = new RigidbodyState
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = _rigidbody.velocity,
                AngularVelocity = _rigidbody.angularVelocity
            };

            ObserversSendRigidbodyState(state);
        }

        /// <summary>
        /// Sends transform and rigidbody state to spectators.
        /// </summary>
        /// <param name="state"></param>
        [ObserversRpc(IncludeOwner = false, BufferLast = true)]
        private void ObserversSendRigidbodyState(RigidbodyState state, Channel channel = Channel.Unreliable)
        {
            if (!CanPredict())
                return;

            _receivedRigidbodyState = state;
            ResetRigidbodyToData();
            PhysicsScene ps = gameObject.scene.GetPhysicsScene();
            for (int i = 0; i < _predictedTicks; i++)
                PredictVelocity(ps);
            _predictedTicks = 0;
        }

    }

}