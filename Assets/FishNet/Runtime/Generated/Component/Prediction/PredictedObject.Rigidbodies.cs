using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Transporting;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    public partial class PredictedObject : NetworkBehaviour
    {
        #region All.

        /// <summary>
        /// Called after a tick occurs; physics would have simulated if using PhysicsMode.TimeManager.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPostTick()
        {
            if (!IsRigidbodyPrediction)
                return;

            bool is2D = (_predictionType == PredictionType.Rigidbody2D);

            if (CanPredict())
            {
                if (!is2D)
                    PredictVelocity(gameObject.scene.GetPhysicsScene());
                else
                    PredictVelocity(gameObject.scene.GetPhysicsScene2D());
            }

            if (base.IsServer)
            {
                uint localTick = base.TimeManager.LocalTick;
                if (localTick >= _nextSendTick || base.TransformMayChange())
                {
                    uint ticksRequired = base.TimeManager.TimeToTicks(SEND_INTERVAL, TickRounding.RoundUp);
                    _nextSendTick = localTick + ticksRequired;

                    if (!is2D)
                        SendRigidbodyState();
                    else
                        SendRigidbody2DState();
                }
            }
        }


        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPostReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d)
        {
            if (!CanPredict())
                return;

            if (_predictionType == PredictionType.Rigidbody)
                PredictVelocity(ps);
            else if (_predictionType == PredictionType.Rigidbody2D)
                PredictVelocity(ps2d);
        }

        /// <summary>
        /// Tries to predict velocity for a Vector3.
        /// </summary>
        protected bool PredictVector3Velocity(ref float? velocityBaseline, ref Vector3 lastVelocity, Vector3 velocity, out Vector3 result)
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
                        result = Vector3.Lerp(velocity, lastVelocity, _predictionRatio);
                        return true;
                    }
                }
            }

            //Fall through.
            result = Vector3.zero;
            return false;
        }


        /// <summary>
        /// Tries to predict velocity for a float.
        /// </summary>
        private bool PredictFloatVelocity(ref float? velocityBaseline, ref float lastVelocity, float velocity, out float result)
        {
            float velocityDifference;
            float directionDifference;

            /* Velocity. */
            directionDifference = (velocityBaseline != null) ? (velocity - lastVelocity) : 0f;

            //If direction has changed too much then reset the baseline.
            if (directionDifference > 0.01f)
            {
                velocityBaseline = null;
            }
            //Direction hasn't changed enough to reset baseline.
            else
            {
                //Difference in velocity since last simulation.
                velocityDifference = Mathf.Abs(lastVelocity - velocity);
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
                        result = Mathf.Lerp(velocity, lastVelocity, _predictionRatio);
                        return true;
                    }
                }
            }

            //Fall through.
            result = 0f;
            return false;
        }


        /// <summary>
        /// Returns if prediction can be used on this rigidbody.
        /// </summary>
        /// <returns></returns>
        private bool CanPredict()
        {
            if (!IsRigidbodyPrediction)
                return false;
            if (base.IsServer || base.IsOwner)
                return false;

            return true;
        }
        #endregion

        #region Rigidbody.

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
            bool isKinematic = _receivedRigidbodyState.Value.IsKinematic;
            _rigidbody.isKinematic = isKinematic;
            if (!isKinematic)
            {
                _rigidbody.velocity = _receivedRigidbodyState.Value.Velocity;
                _rigidbody.angularVelocity = _receivedRigidbodyState.Value.AngularVelocity;
            }
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
            if (_predictionRatio <= 0f)
                return;
            if (ps != _physicsScene)
                return;

            Vector3 result;
            if (PredictVector3Velocity(ref _velocityBaseline, ref _lastVelocity, _rigidbody.velocity, out result))
                _rigidbody.velocity = result;
            if (PredictVector3Velocity(ref _angularVelocityBaseline, ref _lastAngularVelocity, _rigidbody.angularVelocity, out result))
                _rigidbody.angularVelocity = result;

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
                Position = _rigidbody.transform.position,
                Rotation = _rigidbody.transform.rotation,
                IsKinematic = _rigidbody.isKinematic,
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

            SetPreviousTransformProperties();
            _receivedRigidbodyState = state;
            ResetRigidbodyToData();

            ResetToTransformPreviousProperties();
            SetTransformMoveRates();
        }
        #endregion

        #region Rigidbody2D.
        #region Private.
        /// <summary>
        /// Last SpectatorMotorState received from the server. 
        /// </summary>
        private Rigidbody2DState? _receivedRigidbody2DState;
        /// <summary>
        /// Velocity from previous simulation.
        /// </summary>
        private Vector3 _lastVelocity2D;
        /// <summary>
        /// Angular velocity from previous simulation.
        /// </summary>
        private float _lastAngularVelocity2D;
        /// <summary>
        /// Baseline for velocity magnitude.
        /// </summary>
        private float? _velocityBaseline2D;
        /// <summary>
        /// Baseline for angular velocity magnitude.
        /// </summary>
        private float? _angularVelocityBaseline2D;
        /// <summary>
        /// PhysicsScene for this object when OnPreReconcile is called.
        /// </summary>
        private PhysicsScene2D _physicsScene2D;
        #endregion


        /// <summary>
        /// Resets the rigidbody to last known data.
        /// </summary>
        private void ResetRigidbody2DToData()
        {
            if (_receivedRigidbody2DState == null)
                return;

            //Update transform and rigidbody.
            _rigidbody2d.transform.position = _receivedRigidbody2DState.Value.Position;
            _rigidbody2d.transform.rotation = _receivedRigidbody2DState.Value.Rotation;
            bool simulated = _receivedRigidbody2DState.Value.Simulated;
            _rigidbody2d.simulated = simulated;
            if (!simulated)
            {
                _rigidbody2d.velocity = _receivedRigidbody2DState.Value.Velocity;
                _rigidbody2d.angularVelocity = _receivedRigidbody2DState.Value.AngularVelocity;
            }
            //Set prediction defaults.
            _velocityBaseline2D = null;
            _angularVelocityBaseline2D = null;
            _lastVelocity2D = _rigidbody2d.velocity;
            _lastAngularVelocity2D = _rigidbody2d.angularVelocity;
        }

        /// <summary>
        /// Sets the next predicted velocity on the rigidbody.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PredictVelocity(PhysicsScene2D ps)
        {
            if (_predictionRatio <= 0f)
                return;
            if (ps != _physicsScene2D)
                return;

            Vector3 v3Result;
            if (PredictVector3Velocity(ref _velocityBaseline2D, ref _lastVelocity2D, _rigidbody2d.velocity, out v3Result))
                _rigidbody2d.velocity = v3Result;
            float floatResult;
            if (PredictFloatVelocity(ref _angularVelocityBaseline2D, ref _lastAngularVelocity2D, _rigidbody2d.angularVelocity, out floatResult))
                _rigidbody2d.angularVelocity = floatResult;

            _lastVelocity2D = _rigidbody2d.velocity;
            _lastAngularVelocity2D = _rigidbody2d.angularVelocity;
        }


        /// <summary>
        /// Sends current states of this object to client.
        /// </summary>
        private void SendRigidbody2DState()
        {
            Rigidbody2DState state = new Rigidbody2DState
            {
                Position = _rigidbody2d.transform.position,
                Rotation = _rigidbody2d.transform.rotation,
                Simulated = _rigidbody2d.simulated,
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

            SetPreviousTransformProperties();
            _receivedRigidbody2DState = state;
            ResetRigidbody2DToData();

            ResetToTransformPreviousProperties();
            SetTransformMoveRates();
        }
        #endregion

    }


}