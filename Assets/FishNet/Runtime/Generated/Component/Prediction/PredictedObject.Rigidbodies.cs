using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    public partial class PredictedObject : NetworkBehaviour
    {
        #region All.
        #region Internal.
        /// <summary>
        /// Number of instantiated PredictedObjects that are configured for rigidbodies.
        /// </summary>
        [APIExclude]
        [CodegenMakePublic] //To internal.
        public static int InstantiatedRigidbodyCountInternal { get; private set; }

        #endregion

        #region Private.
        /// <summary>
        /// Pauser for rigidbodies when they cannot be rolled back.
        /// </summary>
        private RigidbodyPauser _rigidbodyPauser = new RigidbodyPauser();
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Rigidbodies_OnSpawnServer(NetworkConnection c)
        {
            if (!IsRigidbodyPrediction)
                return;
            if (c == base.Owner)
                return;

            if (_predictionType == PredictionType.Rigidbody)
                SendRigidbodyState(base.TimeManager.LocalTick, c);
            else
                SendRigidbody2DState(base.TimeManager.LocalTick, c);
        }

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

            //if (base.IsServer)
            //{
            //    uint tick = base.TimeManager.Tick;
            //    if (tick >= _nextSendTick)
            //    {
            //        uint ticksRequired = base.TimeManager.TimeToTicks(SEND_INTERVAL, TickRounding.RoundUp);
            //        _nextSendTick = tick + ticksRequired;

            //        if (!is2D)
            //            SendRigidbodyState();
            //        else
            //            SendRigidbody2DState();
            //    }
            //}
        }

        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPreReconcile(NetworkBehaviour nb)
        {
            if (nb.gameObject == gameObject)
                return;
            if (!IsRigidbodyPrediction)
                return;

            bool is2D = (_predictionType == PredictionType.Rigidbody2D);
            uint lastStateTick = (is2D) ? _receivedRigidbody2DState.LastReplicateTick : _receivedRigidbodyState.LastReplicateTick;
            uint lastNbTick = nb.GetLastReconcileTick();

            /* If running again on the same reconcile or state is for a different
             * tick then do make RBs kinematic. Resetting to a different state
             * could cause a desync and there's no reason to run the same
             * tick twice. */
            if (lastStateTick != lastNbTick || lastStateTick == _lastResetTick)
            {
                _rigidbodyPauser.ChangeKinematic(true);
            }
            //If possible to perhaps reset.
            else
            {
                _lastResetTick = lastStateTick;
                /* If the reconciling nb won't change then
                 * there is no reason to rollback. */
                //if (!nb.TransformMayChange())
                //{
                //    _rigidbodyPauser.ChangeKinematic(true);
                //}
                //Need to reset / rollback.
                //else
                //{
                if (is2D)
                    ResetRigidbody2DToData();
                else
                    ResetRigidbodyToData();
                //}
            }
        }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPostReconcile(NetworkBehaviour nb)
        {
            _rigidbodyPauser.ChangeKinematic(false);
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPreReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d)
        {
            if (!CanPredict())
                return;

            if (_predictionType == PredictionType.Rigidbody)
                PredictVelocity(ps);
            else if (_predictionType == PredictionType.Rigidbody2D)
                PredictVelocity(ps2d);
        }


        /// <summary>
        /// Sends rigidbody state before reconciling for a network behaviour.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SendRigidbodyStatesInternal(NetworkBehaviour nb)
        {
            NetworkConnection owner = nb.Owner;
            if (!owner.IsActive)
                return;
            NetworkManager nm = nb.NetworkManager;
            if (nm == null)
                return;

            //Tell all predictedobjects for the networkmanager to try and send states.
            if (_predictedObjects.TryGetValue(nm, out List<PredictedObject> collection))
            {
                uint tick = nb.GetLastReplicateTick();
                int count = collection.Count;
                for (int i = 0; i < count; i++)
                    collection[i].TrySendRigidbodyState(nb, tick);
            }
        }

        /// <summary>
        /// Send current state to a connection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TrySendRigidbodyState(NetworkBehaviour nb, uint tick)
        {
            if (!IsRigidbodyPrediction)
                return;
            NetworkConnection nbOwner = nb.Owner;
            //No need to send to self.
            if (nbOwner == base.Owner)
                return;
            /* Not an observer. SendTargetRpc normally
             * already checks this when ValidateTarget
             * is true but we want to save perf by exiting
             * early before checks and serialization when
             * we know the conn is not an observer. */
            if (!base.Observers.Contains(nbOwner))
                return;

            //Only send if transform may change.
            if (!base.TransformMayChange())
                return;

            if (_predictionType == PredictionType.Rigidbody)
                SendRigidbodyState(tick, nbOwner);
            else
                SendRigidbody2DState(tick, nbOwner);
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
        /// The last received Rigidbody2D state.
        /// </summary>
        private RigidbodyState _receivedRigidbodyState;
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

        private void ResetRigidbodyToData()
        {
            RigidbodyState state = _receivedRigidbodyState;
            //Update transform and rigidbody.
            _rigidbody.transform.position = state.Position;
            _rigidbody.transform.rotation = state.Rotation;
            bool isKinematic = state.IsKinematic;
            _rigidbody.isKinematic = isKinematic;
            if (!isKinematic)
            {
                _rigidbody.velocity = state.Velocity;
                _rigidbody.angularVelocity = state.AngularVelocity;
            }

            /* Do not need to sync transforms because it's done internally by the reconcile method.
             * That is, so long as this is called using OnPreReconcile. */

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
        private void SendRigidbodyState(uint reconcileTick, NetworkConnection conn)
        {
            if (conn == base.Owner)
                return;

            RigidbodyState state = new RigidbodyState(_rigidbody, reconcileTick);
            TargetSendRigidbodyState(conn, state, false);
        }

        /// <summary>
        /// Sends transform and rigidbody state to spectators.
        /// </summary>
        [TargetRpc(ValidateTarget = false)]
        private void TargetSendRigidbodyState(NetworkConnection c, RigidbodyState state, bool applyImmediately, Channel channel = Channel.Unreliable)
        {
            if (!CanPredict())
                return;

            _receivedRigidbodyState = state;
            if (applyImmediately)
            {
                ResetRigidbodyToData();
                Physics.SyncTransforms();
            }
        }
        #endregion

        #region Rigidbody2D.
        #region Private.
        /// <summary>
        /// The last received Rigidbody2D state.
        /// </summary>
        private Rigidbody2DState _receivedRigidbody2DState;
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
        /// <summary>
        /// The last tick rigidbodies were reset.
        /// </summary>
        private long _lastResetTick = -1;
        #endregion


        /// <summary>
        /// Resets the Rigidbody2D to last received data.
        /// </summary>
        private void ResetRigidbody2DToData()
        {
            Rigidbody2DState state = _receivedRigidbody2DState;
            //Update transform and rigidbody.
            _rigidbody2d.transform.position = state.Position;
            _rigidbody2d.transform.rotation = state.Rotation;
            bool simulated = state.Simulated;
            _rigidbody2d.simulated = simulated;
            _rigidbody2d.isKinematic = !simulated;
            if (simulated)
            {
                _rigidbody2d.velocity = state.Velocity;
                _rigidbody2d.angularVelocity = state.AngularVelocity;
            }

            /* Do not need to sync transforms because it's done internally by the reconcile method.
             * That is, so long as this is called using OnPreReconcile. */

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
        /// Sends current Rigidbody2D state to a connection.
        /// </summary>
        private void SendRigidbody2DState(uint reconcileTick, NetworkConnection conn)
        {
            Rigidbody2DState state = new Rigidbody2DState(_rigidbody2d, reconcileTick);
            TargetSendRigidbody2DState(conn, state, false);
        }

        /// <summary>
        /// Sends transform and rigidbody state to spectators.
        /// </summary>
        [TargetRpc(ValidateTarget = false)]
        private void TargetSendRigidbody2DState(NetworkConnection c, Rigidbody2DState state, bool applyImmediately, Channel channel = Channel.Unreliable)
        {
            if (!CanPredict())
                return;

            _receivedRigidbody2DState = state;
            if (applyImmediately)
            {
                ResetRigidbody2DToData();
                Physics2D.SyncTransforms();
            }
        }
        #endregion

    }


}