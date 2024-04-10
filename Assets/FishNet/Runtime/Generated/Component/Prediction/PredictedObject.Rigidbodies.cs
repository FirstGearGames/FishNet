using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Timing;
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
#if PREDICTION_1
        #region Types.
        [System.Serializable]
        public struct SmoothingData
        {
            /// <summary>
            /// Percentage of ping to use as interpolation. Higher values will result in more interpolation.
            /// </summary>
            [Tooltip("Percentage of ping to use as interpolation. Higher values will result in more interpolation.")]
            [Range(0.01f, 5f)]
            public float InterpolationPercent;
            /// <summary>
            /// Percentage of ping to use as interpolation when colliding with an object local client owns.
            /// This is used to speed up local interpolation when predicted objects collide with a player as well keep graphics closer to the objects root while colliding.
            /// </summary>
            [Tooltip("Percentage of ping to use as interpolation when colliding with an object local client owns." +
                "This is used to speed up local interpolation when predicted objects collide with a player as well keep graphics closer to the objects root while colliding.")]
            [Range(0.01f, 5f)]
            public float CollisionInterpolationPercent;
            /// <summary>
            /// How much per tick to decrease to collision interpolation when colliding with a local player object.
            /// Higher values will set interpolation to collision settings faster.
            /// </summary>
            [Tooltip("How much per tick to decrease to collision interpolation when colliding with a local player object. Higher values will set interpolation to collision settings faster.")]
            [Range(1, byte.MaxValue)]
            public byte InterpolationDecreaseStep;
            /// <summary>
            /// How much per tick to increase to normal interpolation when not colliding with a local player object.
            /// Higher values will set interpolation to normal settings faster.
            /// </summary>
            [Tooltip("How much per tick to increase to normal interpolation when not colliding with a local player object. Higher values will set interpolation to normal settings faster.")]
            [Range(1, byte.MaxValue)]
            public byte InterpolationIncreaseStep;
        }
        #endregion

        #region All.
        #region Internal.
        /// <summary>
        /// True if owner and implements prediction methods.
        /// </summary>
        internal bool IsPredictingOwner() => (base.IsOwner && _implementsPredictionMethods);
        #endregion
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
        /// <summary>
        /// True if a connection is owner and prediction methods are implemented.
        /// </summary>
        private bool _isPredictingOwner(NetworkConnection c) => (c == base.Owner && _implementsPredictionMethods);
        /// <summary>
        /// Current interpolation value.
        /// </summary>
        private long _currentSpectatorInterpolation;
        /// <summary>
        /// Target interpolation when collision is exited.
        /// </summary>
        private uint _targetSpectatorInterpolation;
        /// <summary>
        /// Target interpolation when collision is entered.
        /// </summary>
        private uint _targetCollisionSpectatorInterpolation;
        /// <summary>
        /// How much per tick to decrease to collision interpolation when colliding with a local player object.
        /// </summary>
        private byte _interpolationDecreaseStep;
        /// <summary>
        /// How much per tick to increase to normal interpolation when not colliding with a local player object.
        /// </summary>
        private byte _interpolationIncreaseStep;
        /// <summary>
        /// Last local tick that collision has stayed with local client objects.
        /// </summary>
        private uint _collisionStayedTick;
        /// <summary>
        /// Local client objects this object is currently colliding with.
        /// </summary>
        private HashSet<GameObject> _localClientCollidedObjects = new HashSet<GameObject>();
        /// <summary>
        /// True if spectator prediction is paused.
        /// </summary>
        private bool _spectatorPaused;
        ///// <summary>
        ///// Target number of ticks to ignore when replaying.
        ///// </summary>
        //private uint _ignoredTicks;
        #region Smoothing datas.
        private static SmoothingData _accurateSmoothingData = new SmoothingData()
        {
            InterpolationPercent = 0.5f,
            CollisionInterpolationPercent = 0.05f,
            InterpolationDecreaseStep = 1,
            InterpolationIncreaseStep = 2,
        };
        private static SmoothingData _mixedSmoothingData = new SmoothingData()
        {
            InterpolationPercent = 1f,
            CollisionInterpolationPercent = 0.1f,
            InterpolationDecreaseStep = 1,
            InterpolationIncreaseStep = 3,
        };
        private static SmoothingData _gradualSmoothingData = new SmoothingData()
        {
            InterpolationPercent = 1.5f,
            CollisionInterpolationPercent = 0.2f,
            InterpolationDecreaseStep = 1,
            InterpolationIncreaseStep = 5,
        };
        #endregion
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Rigidbodies_OnSpawnServer(NetworkConnection c)
        {
            if (!IsRigidbodyPrediction)
                return;
            if (c == base.Owner)
                return;
            if (c.IsLocalClient)
                return;

            uint tick = c.PacketTick.RemoteTick;
            if (_predictionType == PredictionType.Rigidbody)
                SendRigidbodyState(tick, c, true);
            else
                SendRigidbody2DState(tick, c, true);
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
                    _graphicalAnimators[i].keepAnimatorStateOnDisable = true;

                /* True if at least one animator is on the graphical root. 
                * Unity gets components in order so it's safe to assume
                 * 0 would be the topmost animator. This has to be done
                 * to prevent animation jitter when pausing the rbs. */
                if (_graphicalAnimators[0].transform == _graphicalObject)
                {
                    Transform graphicalHolder = new GameObject().transform;
                    graphicalHolder.name = "GraphicalObjectHolder";
                    graphicalHolder.SetParent(transform);
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
            if (base.IsServerStarted)
                return;

            bool is2D = (_predictionType == PredictionType.Rigidbody2D);
            TrySetCollisionExited(is2D);

            /* Can check either one. They may not be initialized yet if host. */
            if (_rigidbodyStates.Initialized)
            {
                if (_localTick == 0)
                    _localTick = base.TimeManager.LocalTick;

                if (!is2D)
                    _rigidbodyStates.Add(new RigidbodyState(_rigidbody, _localTick));
                else
                    _rigidbody2dStates.Add(new Rigidbody2DState(_rigidbody2d, _localTick));
            }

            if (CanPredict())
            {
                UpdateSpectatorSmoothing();
                if (!is2D)
                    PredictVelocity(gameObject.scene.GetPhysicsScene());
                else
                    PredictVelocity(gameObject.scene.GetPhysicsScene2D());
            }
        }

        /// <summary>
        /// Unsets collision values if collision was known to be entered but there are no longer any contact points.
        /// </summary>
        private void TrySetCollisionExited(bool is2d)
        {
            /* If this object is no longer
             * colliding with local client objects
             * then unset collision.
             * This is done here instead of using
             * OnCollisionExit because often collisionexit
             * will be missed due to ignored ticks. 
             * While not ignoring ticks is always an option
             * its not ideal because ignoring ticks helps
             * prevent over predicting. */
            if (_collisionStayedTick != 0 && (base.TimeManager.LocalTick != _collisionStayedTick))
                CollisionExited();
        }

        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        private void Rigidbodies_TimeManager_OnPreReconcile(NetworkBehaviour nb)
        {
            /* Exit if owner and implements prediction methods
             * because csp would be handled by prediction methods
             * rather than predicted object. */
            if (IsPredictingOwner())
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

            //if (_localTick - tick < _ignoredTicks)
            //    _rigidbodyPauser.Pause();

            if (_predictionType == PredictionType.Rigidbody)
            {
                _preReplicateReplayCacheIndex = GetCachedStateIndex(tick, false);
                if (_preReplicateReplayCacheIndex != -1)
                {
                    bool prevKinematic = _rigidbodyStates[_preReplicateReplayCacheIndex].IsKinematic;
                    _rigidbody.isKinematic = prevKinematic;
                }
                PredictVelocity(ps);
            }
            else if (_predictionType == PredictionType.Rigidbody2D)
            {
                _preReplicateReplayCacheIndex = GetCachedStateIndex(tick, true);
                if (_preReplicateReplayCacheIndex != -1)
                {
                    Rigidbody2DState state = _rigidbody2dStates[_preReplicateReplayCacheIndex];
                    _rigidbody2d.simulated = state.Simulated;
                    _rigidbody2d.isKinematic = state.IsKinematic;
                }
                PredictVelocity(ps2d);
            }
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
                int index = _preReplicateReplayCacheIndex;
                if (index != -1)
                {
                    bool prevKinematic = _rigidbodyStates[index].IsKinematic;
                    _rigidbodyStates[index] = new RigidbodyState(_rigidbody, prevKinematic, tick);
                }
            }
            if (_predictionType == PredictionType.Rigidbody2D)
            {
                int index = GetCachedStateIndex(tick, true);
                if (index != -1)
                {
                    bool prevSimulated = _rigidbody2dStates[index].Simulated;
                    _rigidbody2dStates[index] = new Rigidbody2DState(_rigidbody2d, prevSimulated, tick);
                }
            }
        }

        /// <summary>
        /// Pauses corrections as a spectator object.
        /// </summary>
        public void SetPauseSpectatorCorrections_Experimental(bool pause)
        {
            _spectatorPaused = pause;
            if (pause)
            {
                _rigidbodyStates.Clear();
                _rigidbody2dStates.Clear();
            }
        }

        /// <summary>
        /// Called when ping updates for the local client.
        /// </summary>
        private void Rigidbodies_OnRoundTripTimeUpdated(long ping)
        {
            /* Only update periodically when ping changes.
             * This is to prevent excessive interpolation
             * changes. */
            ulong difference = (ulong)Mathf.Abs(ping - _lastPing);
            //Allow update if ping jump is large enough.
            if (difference < 50)
            {
                uint tickInterval = base.TimeManager.TimeToTicks(5f, Managing.Timing.TickRounding.RoundUp);
                if (base.TimeManager.LocalTick - _lastPingUpdateTick < tickInterval)
                    return;
            }
            SetTargetSmoothing(ping, false);
        }
        /// <summary>
        /// Sets target smoothing values.
        /// </summary>
        /// <param name="setImmediately">True to set current values to targets immediately.</param>
        private void SetTargetSmoothing(long ping, bool setImmediately)
        {
            if (_spectatorSmoother == null)
                return;

            _lastPingUpdateTick = base.TimeManager.LocalTick;
            _lastPing = ping;
            SetValues();
            //Ignored ticks will be less for predicted spawner.
            //if (base.NetworkObject.PredictedSpawner.IsLocalClient)
            //    _ignoredTicks /= 2;

            //_igtt = _ignoredTicks;
            //_ignoredTicks = 0;
            //if (base.Owner.IsValid && (base.Owner != base.NetworkObject.PredictedSpawner))
            //{
            //    _ignoredTicks *= 4;
            //    if (gameObject.name.Contains("Bullet"))
            //        Debug.Log("Setting to " + _ignoredTicks);
            //}
            //if (base.Owner.IsValid && (base.Owner == base.NetworkObject.PredictedSpawner))// base.IsOwner)
            //    _ignoredTicks = 0;

            //_spectatorSmoother.SetIgnoredTicks(_ignoredTicks);

            //If to apply values to targets immediately.
            if (setImmediately)
            {
                _currentSpectatorInterpolation = (CollidingWithLocalClient()) ? _targetCollisionSpectatorInterpolation : _targetSpectatorInterpolation;
                _spectatorSmoother.SetInterpolation((uint)_currentSpectatorInterpolation);
            }

            //Sets ranges to use based on smoothing type.
            void SetValues()
            {
                SmoothingData data;
                if (_spectatorSmoothingType == SpectatorSmoothingType.Accuracy)
                    data = _accurateSmoothingData;
                else if (_spectatorSmoothingType == SpectatorSmoothingType.Mixed)
                    data = _mixedSmoothingData;
                else if (_spectatorSmoothingType == SpectatorSmoothingType.Gradual)
                    data = _gradualSmoothingData;
                else
                    data = _customSmoothingData;

                TimeManager tm = base.TimeManager;
                double interpolationTime = (ping / 1000d) * data.InterpolationPercent;
                _targetSpectatorInterpolation = tm.TimeToTicks(interpolationTime, TickRounding.RoundUp);
                double collisionInterpolationTime = (ping / 1000d) * data.CollisionInterpolationPercent;
                _targetCollisionSpectatorInterpolation = tm.TimeToTicks(collisionInterpolationTime, TickRounding.RoundUp);

                _interpolationDecreaseStep = data.InterpolationDecreaseStep;
                _interpolationIncreaseStep = data.InterpolationIncreaseStep;
            }
        }

        /// <summary>
        /// Returns if this object is colliding with any local client objects.
        /// </summary>
        /// <returns></returns>
        private bool CollidingWithLocalClient()
        {
            /* If it's been more than 1 tick since collision stayed
             * then do not consider as collided. */
            return (base.TimeManager.LocalTick - _collisionStayedTick) < 1;
        }

        private uint _igtt;
        /// <summary>
        /// Updates spectator smoothing values to move towards their targets.
        /// </summary>
        private void UpdateSpectatorSmoothing()
        {
            bool colliding = CollidingWithLocalClient();
            if (colliding)
                _currentSpectatorInterpolation -= _interpolationDecreaseStep;
            else
                _currentSpectatorInterpolation += _interpolationIncreaseStep;

            _currentSpectatorInterpolation = (long)Mathf.Clamp(_currentSpectatorInterpolation, _targetCollisionSpectatorInterpolation, _targetSpectatorInterpolation);
            _spectatorSmoother.SetInterpolation((uint)_currentSpectatorInterpolation);
        }

        /// <summary>
        /// Called when a collision occurs and the smoothing type must perform operations.
        /// </summary>
        private bool CollisionEnteredLocalClientObject(GameObject go)
        {
            if (go.TryGetComponent<NetworkObject>(out NetworkObject nob))
                return nob.Owner.IsLocalClient;

            //Fall through.
            return false;
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
            //No need to send to self unless doesnt implement prediction methods.
            if (_isPredictingOwner(nbOwner))
                return;
            //If clientHost.
            if (nbOwner.IsLocalClient)
                return;
            /* Not an observer. SendTargetRpc normally
             * already checks this when ValidateTarget
             * is true but we want to save perf by exiting
             * early before checks and serialization when
             * we know the conn is not an observer. */
            if (!base.Observers.Contains(nbOwner))
                return;

            bool hasChanged = base.PredictedTransformMayChange();
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
            if (base.IsServerStarted || IsPredictingOwner())
                return false;
            if (_spectatorPaused)
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

        private void OnCollisionEnter(Collision collision)
        {
            if (_predictionType != PredictionType.Rigidbody)
                return;

            GameObject go = collision.gameObject;
            if (CollisionEnteredLocalClientObject(go))
                CollisionEntered(go);
        }


        private void OnCollisionStay(Collision collision)
        {
            if (_predictionType != PredictionType.Rigidbody)
                return;

            if (_localClientCollidedObjects.Contains(collision.gameObject))
                _collisionStayedTick = base.TimeManager.LocalTick;
        }

        /// <summary>
        /// Resets the rigidbody to a state.
        /// </summary>
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
            //No need to send to owner if they implement prediction methods.
            if (_isPredictingOwner(conn))
                return;
            reconcileTick = (conn == base.NetworkObject.PredictedSpawner) ? conn.PacketTick.RemoteTick : reconcileTick;
            RigidbodyState state = new RigidbodyState(_rigidbody, reconcileTick);
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
            if (applyImmediately)
            {
                /* If PredictedSpawner is self then this client
                 * was the one to predicted spawn this object. When that is
                 * the case do not apply initial velocities, but so allow
                 * regular updates/corrections. */
                if (base.NetworkObject.PredictedSpawner.IsLocalClient)
                    return;
            }
            else
            {
                if (!CanProcessReceivedState(localTick))
                    return;
            }

            if (applyImmediately)
            {
                _rigidbodyStates.Clear();
                ResetRigidbodyToData(state);
            }
            else
            {
                int index = GetCachedStateIndex(localTick, false);
                if (index != -1)
                    _rigidbodyStates[index] = state;
                else
                    _rigidbodyStates.Add(state);
            }
        }
        #endregion

        #region Rigidbody2D.
        #region Private.
        /// <summary>
        /// Past RigidbodyStates.
        /// </summary>
        private RingBuffer<Rigidbody2DState> _rigidbody2dStates = new RingBuffer<Rigidbody2DState>();
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
        /// Last found cacheIndex during PreReplay.
        /// </summary>
        private int _preReplicateReplayCacheIndex;
        /// <summary>
        /// Last tick a ping update was received.
        /// </summary>
        private uint _lastPingUpdateTick;
        /// <summary>
        /// Last ping during a ping update.
        /// </summary>
        private long _lastPing;
        #endregion

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_predictionType != PredictionType.Rigidbody2D)
                return;

            GameObject go = collision.gameObject;
            if (CollisionEnteredLocalClientObject(go))
                CollisionEntered(go);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (_predictionType != PredictionType.Rigidbody2D)
                return;

            if (_localClientCollidedObjects.Contains(collision.gameObject))
                _collisionStayedTick = base.TimeManager.LocalTick;
        }

        /// <summary>
        /// Called when collision has entered a local clients object.
        /// </summary>
        private void CollisionEntered(GameObject go)
        {
            _collisionStayedTick = base.TimeManager.LocalTick;
            _localClientCollidedObjects.Add(go);
        }

        /// <summary>
        /// Called when collision has exited a local clients object.
        /// </summary>
        private void CollisionExited()
        {
            _localClientCollidedObjects.Clear();
            _collisionStayedTick = 0;
        }

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
            _rigidbody2d.isKinematic = state.IsKinematic;
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
            if (applyImmediately)
            {
                /* If PredictedSpawner is self then this client
                 * was the one to predicted spawn this object. When that is
                 * the case do not apply initial velocities, but so allow
                 * regular updates/corrections. */
                if (base.NetworkObject.PredictedSpawner.IsLocalClient)
                    return;
            }
            else
            {
                if (!CanProcessReceivedState(localTick))
                    return;
            }

            if (applyImmediately)
            {
                _rigidbody2dStates.Clear();
                ResetRigidbody2DToData(state);
            }
            else
            {
                int index = GetCachedStateIndex(localTick, true);
                if (index != -1)
                    _rigidbody2dStates[index] = state;
                else
                    _rigidbody2dStates.Add(state);
            }


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
#endif
    }


}