using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Utility;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    public partial class PredictedObject : NetworkBehaviour
    {
        #region All.
        #region Private.
        /// <summary>
        /// Pauser for rigidbodies when they cannot be rolled back.
        /// </summary>
        private RigidbodyPauser _rigidbodyPauser = new RigidbodyPauser();
        /// <summary>
        /// Next tick to resend data when resend type is set to interval.
        /// </summary>
        private uint _nextIntervalResend;
        /// <summary>
        /// Number of resends remaining when the object has not changed.
        /// </summary>
        private ushort _resendsRemaining;
        /// <summary>
        /// True if object was changed previous tick.
        /// </summary>
        private bool _previouslyChanged;
        /// <summary>
        /// Animators found on the graphical object.
        /// </summary>
        private Animator[] _graphicalAnimators;
        /// <summary>
        /// True if GraphicalAniamtors have been intialized.
        /// </summary>
        private bool _animatorsInitialized;
        /// <summary>
        /// Tick on the last received state.
        /// </summary>
        private uint _lastStateLocalTick;
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Rigidbodies_OnSpawnServer(NetworkConnection c)
        {
            if (!IsRigidbodyPrediction)
                return;
            if (c == base.Owner)
                return;

            if (_predictionType == PredictionType.Rigidbody)
                SendRigidbodyState(base.TimeManager.LocalTick, c, true);
            else
                SendRigidbody2DState(base.TimeManager.LocalTick, c, true);
        }

        /// <summary>
        /// Called when the client starts.
        /// </summary>
        private void Rigidbodies_OnStartClient()
        {
            //Store up to 1 second of states.
            int capacity = base.TimeManager.TickRate;
            /* Only need to check one collection capacity since they both will be the same.
             * If capacity does not line up then re-initialize. */
            if (capacity != _rigidbodyStates.Capacity)
            {
                _rigidbodyStates.Initialize(capacity);
                _rigidbody2dStates.Initialize(capacity);
            }
        }

        /// <summary>
        /// Called on client when ownership changes for this object.
        /// </summary>
        /// <param name="prevOwner"></param>
        private void Rigidbodies_OnOwnershipClient(NetworkConnection prevOwner)
        {
            if (!IsRigidbodyPrediction)
                return;
            //If owner no need to fix for animators.
            if (base.IsOwner)
                return;
            //Would have already fixed if animators are set.
            if (_animatorsInitialized)
                return;

            _animatorsInitialized = true;
            _graphicalAnimators = _graphicalObject.GetComponentsInChildren<Animator>(true);

            if (_graphicalAnimators.Length > 0)
            {
                for (int i = 0; i < _graphicalAnimators.Length; i++)
                    _graphicalAnimators[i].keepAnimatorControllerStateOnDisable = true;

                /* True if at least one animator is on the graphical root. 
                * Unity gets components in order so it's safe to assume
                 * 0 would be the topmost animator. This has to be done
                 * to prevent animation jitter when pausing the rbs. */
                if (_graphicalAnimators[0].transform == _graphicalObject)
                {
                    Transform graphicalHolder = new GameObject().transform;
                    graphicalHolder.name = "GraphicalObjectHolder";
                    graphicalHolder.SetParent(transform);
                    //ref _graphicalInstantiatedOffsetPosition, ref _graphicalInstantiatedOffsetRotation);
                    graphicalHolder.localPosition = _graphicalInstantiatedOffsetPosition;
                    graphicalHolder.localRotation = _graphicalInstantiatedOffsetRotation;
                    graphicalHolder.localScale = _graphicalObject.localScale;
                    _graphicalObject.SetParent(graphicalHolder);
                    _graphicalObject.localPosition = Vector3.zero;
                    _graphicalObject.localRotation = Quaternion.identity;
                    _graphicalObject.localScale = Vector3.one;
                    SetGraphicalObject(graphicalHolder);
                }
            }
        }

        /// <summary>
        /// Called after a tick occurs; physics would have simulated if using PhysicsMode.TimeManager.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPostTick()
        {
            if (!IsRigidbodyPrediction)
                return;
            if (base.IsServer)
                return;

            bool is2D = (_predictionType == PredictionType.Rigidbody2D);
            uint localTick = base.TimeManager.LocalTick;

            /* Can check either one. They may not be initialized yet if host. */
            if (_rigidbodyStates.Initialized)
            {
                if (!is2D)
                    _rigidbodyStates.Add(new RigidbodyState(_rigidbody, localTick));
                else
                    _rigidbody2dStates.Add(new Rigidbody2DState(_rigidbody2d, localTick));
            }

            if (CanPredict())
            {
                if (!is2D)
                    PredictVelocity(gameObject.scene.GetPhysicsScene());
                else
                    PredictVelocity(gameObject.scene.GetPhysicsScene2D());
            }
        }

        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPreReconcile(NetworkBehaviour nb)
        {
            /* Exit if owner because the owner should
             * only be using CSP to rollback. */
            if (base.IsOwner)
                return;
            if (nb.gameObject == gameObject)
                return;
            if (!IsRigidbodyPrediction)
                return;

            bool is2D = (_predictionType == PredictionType.Rigidbody2D);
            uint lastNbTick = nb.GetLastReconcileTick();
            int stateIndex = GetCachedStateIndex(lastNbTick, is2D);
            /* If running again on the same reconcile or state is for a different
             * tick then do make RBs kinematic. Resetting to a different state
             * could cause a desync and there's no reason to run the same
             * tick twice. */
            if (stateIndex == -1)
            {
                _spectatorSmoother?.SetLocalReconcileTick(-1);
                _rigidbodyPauser.Pause();
            }
            //If state was found then reset to it.
            else
            {
                _spectatorSmoother?.SetLocalReconcileTick(lastNbTick);
                if (is2D)
                {
                    _rigidbody2dStates.RemoveRange(true, stateIndex);
                    ResetRigidbody2DToData(_rigidbody2dStates[0]);
                }
                else
                {
                    _rigidbodyStates.RemoveRange(true, stateIndex);
                    ResetRigidbodyToData(_rigidbodyStates[0]);
                }
            }
        }

        /// <summary>
        /// Called after performing a reconcile on NetworkBehaviour.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPostReconcile(NetworkBehaviour nb)
        {
            _rigidbodyPauser.Unpause();
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void Rigidbodies_PredictionManager_OnPreReplicateReplay(uint tick, PhysicsScene ps, PhysicsScene2D ps2d)
        {
            if (!CanPredict())
                return;

            if (_predictionType == PredictionType.Rigidbody)
                PredictVelocity(ps);
            else if (_predictionType == PredictionType.Rigidbody2D)
                PredictVelocity(ps2d);
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void Rigidbodies_PredictionManager_OnPostReplicateReplay(uint tick, PhysicsScene ps, PhysicsScene2D ps2d)
        {
            if (!CanPredict())
                return;
            if (_rigidbodyPauser.Paused)
                return;

            if (_predictionType == PredictionType.Rigidbody)
            {
                int index = GetCachedStateIndex(tick, false);
                if (index != -1)
                    _rigidbodyStates[index] = new RigidbodyState(_rigidbody, tick);
            }
            if (_predictionType == PredictionType.Rigidbody2D)
            {
                int index = GetCachedStateIndex(tick, true);
                if (index != -1)
                    _rigidbody2dStates[index] = new Rigidbody2DState(_rigidbody2d, tick);
            }
        }



        /// <summary>
        /// Sends the rigidbodies state to Observers of a NetworkBehaviour.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendRigidbodyState(NetworkBehaviour nb)
        {
            NetworkConnection owner = nb.Owner;
            if (!owner.IsActive)
                return;
            NetworkManager nm = nb.NetworkManager;
            if (nm == null)
                return;

            uint tick = nb.GetLastReplicateTick();
            TrySendRigidbodyState(nb, tick);
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

            bool hasChanged = base.TransformMayChange();
            if (!hasChanged)
            {
                //Not changed but was previous tick. Reset resends.
                if (_previouslyChanged)
                    _resendsRemaining = base.TimeManager.TickRate;

                uint currentTick = base.TimeManager.Tick;
                //Resends remain.
                if (_resendsRemaining > 0)
                {
                    _resendsRemaining--;
                    //If now 0 then update next send interval.
                    if (_resendsRemaining == 0)
                        UpdateNextIntervalResend();
                }
                //No more resends.
                else
                {
                    //No resend interval.
                    if (_resendType == ResendType.Disabled)
                        return;
                    //Interval not yet met.
                    if (currentTick < _nextIntervalResend)
                        return;

                    UpdateNextIntervalResend();
                }

                //Updates the next tick when a resend should occur.
                void UpdateNextIntervalResend()
                {
                    _nextIntervalResend = (currentTick + _resendInterval);
                }

            }
            _previouslyChanged = hasChanged;

            if (_predictionType == PredictionType.Rigidbody)
                SendRigidbodyState(tick, nbOwner, false);
            else
                SendRigidbody2DState(tick, nbOwner, false);
        }

        /// <summary>
        /// Gets a cached state index in actual array position.
        /// </summary>
        /// <returns></returns>
        private int GetCachedStateIndex(uint tick, bool is2d)
        {
            int count;
            uint firstTick;
            //3d.
            if (!is2d)
            {
                count = _rigidbodyStates.Count;
                if (count == 0)
                    return -1;
                firstTick = _rigidbodyStates[0].LocalTick;
            }
            //2d.
            else
            {
                count = _rigidbody2dStates.Count;
                if (count == 0)
                    return -1;
                firstTick = _rigidbody2dStates[0].LocalTick;
            }

            //First tick is higher than current, no match is possibloe.
            if (firstTick > tick)
                return -1;

            long difference = (tick - firstTick);
            //Desired tick would be out of bounds. This should never happen.
            if (difference >= count)
                return -1;

            return (int)difference;
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
                        Vector3 changeMultiplied = (velocity - lastVelocity) * _maintainedVelocity;
                        //Retaining velocity.
                        if (_maintainedVelocity > 0f)
                        {
                            result = (velocity + changeMultiplied);
                        }
                        //Reducing velocity.
                        else
                        {
                            result = (velocity + changeMultiplied);
                            /* When reducing velocity make sure the direction
                             * did not change. When this occurs it means the velocity
                             * was reduced into the opposite direction. To prevent
                             * this from happening just zero out velocity instead. */
                            if (velocity.normalized != result.normalized)
                                result = Vector3.zero;
                        }
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
                        float changeMultiplied = (velocity - lastVelocity) * _maintainedVelocity;
                        //Retaining velocity.
                        if (_maintainedVelocity > 0f)
                        {
                            result = (velocity + changeMultiplied);
                        }
                        //Reducing velocity.
                        else
                        {
                            result = (velocity + changeMultiplied);
                            /* When reducing velocity make sure the direction
                             * did not change. When this occurs it means the velocity
                             * was reduced into the opposite direction. To prevent
                             * this from happening just zero out velocity instead. */
                            if (Mathf.Abs(velocity) != Mathf.Abs(result))
                                result = 0f;
                        }
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
        /// Past RigidbodyStates.
        /// </summary>
        private RingBuffer<RigidbodyState> _rigidbodyStates = new RingBuffer<RigidbodyState>();
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

        private void ResetRigidbodyToData(RigidbodyState state)
        {
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
            if (_maintainedVelocity == 0f)
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
        private void SendRigidbodyState(uint reconcileTick, NetworkConnection conn, bool applyImmediately)
        {
            if (conn == base.Owner)
                return;

            RigidbodyState state = new RigidbodyState(_rigidbody, reconcileTick);
            if (applyImmediately)
                state.Velocity = Vector3.forward * 20f;

            TargetSendRigidbodyState(conn, state, applyImmediately);
        }

        /// <summary>
        /// Sends transform and rigidbody state to spectators.
        /// </summary>
        [TargetRpc(ValidateTarget = false)]
        private void TargetSendRigidbodyState(NetworkConnection c, RigidbodyState state, bool applyImmediately, Channel channel = Channel.Unreliable)
        {
            if (!CanPredict())
                return;

            uint localTick = state.LocalTick;
            if (!applyImmediately && !CanProcessReceivedState(localTick))
                    return;

            int index = GetCachedStateIndex(localTick, false);
            /* Index will always be found so long as the tick is not
             * extremely old. The buffer only holds 1000ms worth of snapshots. */
            if (index != -1)
                _rigidbodyStates[index] = state;

            if (applyImmediately)
                ResetRigidbodyToData(state);
        }
        #endregion

        #region Rigidbody2D.
        #region Private.
        /// <summary>
        /// Past RigidbodyStates.
        /// </summary>
        private RingBuffer<Rigidbody2DState> _rigidbody2dStates = new RingBuffer<Rigidbody2DState>();
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
        #endregion


        /// <summary>
        /// Resets the Rigidbody2D to last received data.
        /// </summary>
        private void ResetRigidbody2DToData(Rigidbody2DState state)
        {
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
            if (_maintainedVelocity == 0f)
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
        private void SendRigidbody2DState(uint reconcileTick, NetworkConnection conn, bool applyImmediately)
        {
            Rigidbody2DState state = new Rigidbody2DState(_rigidbody2d, reconcileTick);
            TargetSendRigidbody2DState(conn, state, applyImmediately);
        }

        /// <summary>
        /// Sends transform and rigidbody state to spectators.
        /// </summary>
        [TargetRpc(ValidateTarget = false)]
        private void TargetSendRigidbody2DState(NetworkConnection c, Rigidbody2DState state, bool applyImmediately, Channel channel = Channel.Unreliable)
        {
            if (!CanPredict())
                return;
            uint localTick = state.LocalTick;
            if (!applyImmediately && !CanProcessReceivedState(localTick))
                return;

            int index = GetCachedStateIndex(localTick, true);
            /* Index will always be found so long as the tick is not
             * extremely old. The buffer only holds 1000ms worth of snapshots. */

            if (index != -1)
                _rigidbody2dStates[index] = state;

            if (applyImmediately)
                ResetRigidbody2DToData(state);
        }

        /// <summary>
        /// Returns if a received state can be processed based on it's tick.
        /// </summary>
        /// <param name="stateTick"></param>
        /// <returns></returns>
        private bool CanProcessReceivedState(uint stateTick)
        {
            //Older than another received value.
            if (stateTick <= _lastStateLocalTick)
                return false;
            _lastStateLocalTick = stateTick;

            return true;
        }
        #endregion

    }


}