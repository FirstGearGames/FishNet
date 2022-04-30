using FishNet.Documenting;
using FishNet.Object;
using FishNet.Transporting;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    [AddComponentMenu("")]
    [APIExclude]
    public class PredictedRigidbody2D : PredictedRigidbodyBase
    {
        #region Type.
        public struct Rigidbody2DState
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public float AngularVelocity;
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// Rigidbody to predict.
        /// </summary>
        [SerializeField, HideInInspector]
        private Rigidbody2D _rigidbody2d;
        #endregion

        #region Private.
        /// <summary>
        /// Last SpectatorMotorState received from the server.
        /// </summary>
        private Rigidbody2DState? _receivedRigidbodyState;
        /// <summary>
        /// Velocity from previous simulation.
        /// </summary>
        private Vector3 _lastVelocity;
        /// <summary>
        /// Angular velocity from previous simulation.
        /// </summary>
        private float _lastAngularVelocity;
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
        private PhysicsScene2D _physicsScene2D;
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
                PredictVelocity(gameObject.scene.GetPhysicsScene2D());
        }

        /// <summary>
        /// Called before reconcile begins.
        /// </summary>
        protected override void TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            base.TimeManager_OnPreReconcile(obj);

            if (!CanPredict())
                return;

            _physicsScene2D = gameObject.scene.GetPhysicsScene2D();
            if (_physicsScene2D == obj.gameObject.scene.GetPhysicsScene2D())
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
                PredictVelocity(ps2d);
        }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        protected override void TimeManager_OnPostReconcile(NetworkBehaviour obj)
        {
            base.TimeManager_OnPostReconcile(obj);

            if (!CanPredict())
                return;

            if (_physicsScene2D == gameObject.scene.GetPhysicsScene2D())
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
            _rigidbody2d.transform.position = _receivedRigidbodyState.Value.Position;
            _rigidbody2d.transform.rotation = _receivedRigidbodyState.Value.Rotation;
            _rigidbody2d.velocity = _receivedRigidbodyState.Value.Velocity;
            _rigidbody2d.angularVelocity = _receivedRigidbodyState.Value.AngularVelocity;
            //Set prediction defaults.
            _velocityBaseline = null;
            _angularVelocityBaseline = null;
            _lastVelocity = _rigidbody2d.velocity;
            _lastAngularVelocity = _rigidbody2d.angularVelocity;
        }

        /// <summary>
        /// Sets the next predicted velocity on the rigidbody.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PredictVelocity(PhysicsScene2D ps)
        {
            if (base.PredictionRatio <= 0f)
                return;
            if (ps != _physicsScene2D)
                return;

            Vector3 v3Result;
            if (base.PredictVector3Velocity(ref _velocityBaseline, ref _lastVelocity, _rigidbody2d.velocity, out v3Result))
                _rigidbody2d.velocity = v3Result;
            float floatResult;
            if (base.PredictFloatVelocity(ref _angularVelocityBaseline, ref _lastAngularVelocity, _rigidbody2d.angularVelocity, out floatResult))
                _rigidbody2d.angularVelocity = floatResult;

            _lastVelocity = _rigidbody2d.velocity;
            _lastAngularVelocity = _rigidbody2d.angularVelocity;
        }


        /// <summary>
        /// Sends current states of this object to client.
        /// </summary>
        protected override void SendRigidbodyState()
        {
            Rigidbody2DState state = new Rigidbody2DState
            {
                Position = _rigidbody2d.transform.position,
                Rotation = _rigidbody2d.transform.rotation,
                Velocity = _rigidbody2d.velocity,
                AngularVelocity = _rigidbody2d.angularVelocity
            };

            ObserversSendRigidbody2DState(state);
        }

        /// <summary>
        /// Sends transform and rigidbody state to spectators.
        /// </summary>
        /// <param name="state"></param>
        [ObserversRpc(IncludeOwner = false, BufferLast = true)]
        private void ObserversSendRigidbody2DState(Rigidbody2DState state, Channel channel = Channel.Unreliable)
        {
            if (!CanPredict())
                return;

            base.SetPreviousStates();

            _receivedRigidbodyState = state;
            ResetRigidbodyToData();

            base.SetTransformMoveRates();
            base.ResetToTransformPrevious();
        }

        internal override void SetRigidbody(Rigidbody rb) { }
        /// <summary>
        /// Sets Rigidbody2d to value.
        /// </summary>
        /// <param name="value"></param>
        internal override void SetRigidbody(Rigidbody2D value) => _rigidbody2d = value;
    }

}