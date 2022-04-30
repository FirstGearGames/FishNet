using FishNet.Documenting;
using FishNet.Object;
using FishNet.Transporting;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Prediction
{

    [AddComponentMenu("")]
    [APIExclude]
    public class PredictedRigidbody : PredictedRigidbodyBase
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
        /// Rigidbody to predict.
        /// </summary>
        [SerializeField, HideInInspector]
        private Rigidbody _rigidbody;
        #endregion

        #region Private.
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

        #endregion

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Update()
        {
            base.Update();
        }

        protected override void TimeManager_OnPostTick()
        {
            base.TimeManager_OnPostTick();

            if (CanPredict())
                PredictVelocity(gameObject.scene.GetPhysicsScene());
        }

        /// <summary>
        /// Called before reconcile begins.
        /// </summary>
        protected override void TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            base.TimeManager_OnPreReconcile(obj);

            if (!CanPredict())
                return;

            _physicsScene = gameObject.scene.GetPhysicsScene();
            if (_physicsScene == obj.gameObject.scene.GetPhysicsScene())
            {
                base.SetPreviousStates();
                ResetRigidbodyToData();
            }
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        protected override void TimeManager_OnPostReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d)
        {
            base.TimeManager_OnPostReplicateReplay(ps, ps2d);

            if (base.CanPredict())
                PredictVelocity(ps);
        }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        protected override void TimeManager_OnPostReconcile(NetworkBehaviour obj)
        {
            base.TimeManager_OnPostReconcile(obj);

            if (!CanPredict())
                return;

            if (_physicsScene == gameObject.scene.GetPhysicsScene())
            {
                base.SetTransformMoveRates();
                base.ResetToTransformPrevious();
            }
        }


        /// <summary>
        /// Resets the rigidbody to last known data.
        /// </summary>
        private void ResetRigidbodyToData()
        {
            if (_receivedRigidbodyState == null)
                return;

            //Update transform and rigidbody.
            _rigidbody.transform.position = _receivedRigidbodyState.Value.Position;
            _rigidbody.transform.rotation = _receivedRigidbodyState.Value.Rotation;
            _rigidbody.velocity = _receivedRigidbodyState.Value.Velocity;
            _rigidbody.angularVelocity = _receivedRigidbodyState.Value.AngularVelocity;
            //Set prediction defaults.
            _velocityBaseline = null;
            _angularVelocityBaseline = null;
            _lastVelocity = _rigidbody.velocity;
            _lastAngularVelocity = _rigidbody.angularVelocity;
        }

        /// <summary>
        /// Sets the next predicted velocity on the rigidbody.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PredictVelocity(PhysicsScene ps)
        {
            if (base.PredictionRatio <= 0f)
                return;
            if (ps != _physicsScene)
                return;

            Vector3 result;
            if (base.PredictVector3Velocity(ref _velocityBaseline, ref _lastVelocity, _rigidbody.velocity, out result))
                _rigidbody.velocity = result;
            if (base.PredictVector3Velocity(ref _angularVelocityBaseline, ref _lastAngularVelocity, _rigidbody.angularVelocity, out result))
                _rigidbody.angularVelocity = result;

            _lastVelocity = _rigidbody.velocity;
            _lastAngularVelocity = _rigidbody.angularVelocity;
        }


        /// <summary>
        /// Sends current states of this object to client.
        /// </summary>
        protected override void SendRigidbodyState()
        {
            RigidbodyState state = new RigidbodyState
            {
                Position = _rigidbody.transform.position,
                Rotation = _rigidbody.transform.rotation,
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

            base.SetPreviousStates();

            _receivedRigidbodyState = state;
            ResetRigidbodyToData();

            base.SetTransformMoveRates();
            base.ResetToTransformPrevious();
        }


        /// <summary>
        /// Sets Rigidbody to value.
        /// </summary>
        /// <param name="value"></param>
        internal override void SetRigidbody(Rigidbody value) => _rigidbody = value;
        internal override void SetRigidbody(Rigidbody2D rb2d) { }
    }

}