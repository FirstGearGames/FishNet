using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Object.Prediction
{
    /// <summary>
    /// This class is under regular development and it's API may change at any time.
    /// </summary>
    public sealed class ChildTransformTickSmoother : IResettable
    {
        #region Types.
        [Preserve]
        private struct TickTransformProperties
        {
            public uint Tick;
            public TransformProperties Properties;

            public TickTransformProperties(uint tick, Transform t)
            {
                Tick = tick;
                Properties = new TransformProperties(t.localPosition, t.localRotation, t.localScale);
            }
            public TickTransformProperties(uint tick, Transform t, Vector3 localScale)
            {
                Tick = tick;
                Properties = new TransformProperties(t.localPosition, t.localRotation, localScale);
            }
            public TickTransformProperties(uint tick, TransformProperties tp)
            {
                Tick = tick;
                Properties = tp;
            }
        }
        #endregion

        #region Private.
        /// <summary>
        /// Object to smooth.
        /// </summary>
        private Transform _graphicalObject;
        /// <summary>
        /// When not MoveRatesCls.UNSET_VALUE the graphical object will teleport into it's next position if the move distance exceeds this value.
        /// </summary>
        private float _teleportThreshold;
        /// <summary>
        /// How quickly to move towards goal values.
        /// </summary>
        private MoveRates _moveRates = new MoveRates(MoveRatesCls.UNSET_VALUE);
        /// <summary>
        /// True if a pretick occurred since last postTick.
        /// </summary>
        private bool _preTicked;
        /// <summary>
        /// World offset values of the graphical from the NetworkObject during initialization.
        /// </summary>
        private TransformProperties _gfxInitializedOffsetValues;
        /// <summary>
        /// World values of the graphical after it's been aligned to initialized values in PreTick.
        /// </summary>
        private TransformProperties _gfxPreSimulateWorldValues;
        /// <summary>
        /// TickDelta on the TimeManager.
        /// </summary>
        private float _tickDelta;
        /// <summary>
        /// How many ticks to interpolate over when not using adaptive.
        /// </summary>
        private byte _ownerInterpolation;
        /// <summary>
        /// Current interpolation, regardless of if using adaptive or not.
        /// </summary>
        private byte _interpolation;
        /// <summary>
        /// NetworkObject this is for.
        /// </summary>
        private NetworkObject _networkObject;
        /// <summary>
        /// Value to multiply movement by. This is used to reduce or increase the rate the movement buffer is consumed.
        /// </summary>
        private float _movementMultiplier = 1f;
        /// <summary>
        /// TransformProperties to move towards.
        /// </summary>
        private BasicQueue<TickTransformProperties> _transformProperties;
        /// <summary>
        /// Which properties to smooth.
        /// </summary>
        private TransformPropertiesFlag _ownerSmoothedProperties;
        /// <summary>
        /// Which properties to smooth.
        /// </summary>
        private TransformPropertiesFlag _spectatorSmoothedProperties;
        /// <summary>
        /// Updates the smoothedProperties value.
        /// </summary>
        /// <param name="value">New value.</param>
        /// <param name="forSpectator">True if updating values for the spectator, false if updating for owner.</param>
        public void SetSmoothedProperties(TransformPropertiesFlag value, bool forSpectator)
        {
            if (forSpectator)
                _spectatorSmoothedProperties = value;
            else
                _ownerSmoothedProperties = value;
        }
        /// <summary>
        /// Amount of adaptive interpolation to use.
        /// </summary>
        private AdaptiveInterpolationType _adaptiveInterpolation = AdaptiveInterpolationType.VeryLow;
        /// <summary>
        /// Updates the adaptiveInterpolation value.
        /// </summary>
        /// <param name="adaptiveInterpolation">New value.</param>
        public void SetAdaptiveInterpolation(AdaptiveInterpolationType adaptiveInterpolation)
        {
            _adaptiveInterpolation = adaptiveInterpolation;
        }
        /// <summary>
        /// Set interpolation to use for spectated objects if adaptiveInterpolation is off.
        /// </summary>
        private byte _spectatorInterpolation;
        /// <summary>
        /// Sets the spectator interpolation value.
        /// </summary>
        /// <param name="value">New value.</param>
        /// <param name="disableAdaptiveInterpolation">True to also disable adaptive interpolation to use this new value.</param>
        public void SetSpectatorInterpolation(byte value, bool disableAdaptiveInterpolation = true)
        {
            _spectatorInterpolation = value;
            if (disableAdaptiveInterpolation)
                _adaptiveInterpolation = AdaptiveInterpolationType.Off;
        }
        /// <summary>
        /// Previous parent the graphical was attached to.
        /// </summary>
        private Transform _previousParent;
        /// <summary>
        /// True if to detach at runtime.
        /// </summary>
        private bool _detach;
        /// <summary>
        /// True if were an owner of the NetworkObject during PreTick.
        /// This is only used for performance gains.
        /// </summary>
        private bool _ownerOnPretick;
        /// <summary>
        /// True if adaptive interpolation should be used.
        /// </summary>
        private bool _useAdaptiveInterpolation => (!_ownerOnPretick && _adaptiveInterpolation != AdaptiveInterpolationType.Off);
        /// <summary>
        /// True if Initialized has been called and settings have not been reset.
        /// </summary>
        private bool _initialized;
        /// <summary>
        /// Last tick this was teleported on.
        /// </summary>
        private uint _teleportedTick = TimeManager.UNSET_TICK;
        /// <summary>
        /// Last local tick a reconcile callback was received.
        /// </summary>
        private uint _lastReconcileTick = TimeManager.UNSET_TICK;
        /// <summary>
        /// Ticks passed since the last reconcile.
        /// </summary>
        private uint _reconcileInterval = RECONCILE_INTERVAL_DEFAULT;
        #endregion

        #region Const.
        /// <summary>
        /// Default expected interval for reconciles.
        /// </summary>
        private const int RECONCILE_INTERVAL_DEFAULT = 1;
        /// <summary>
        /// Maximum allowed entries to be queued over the interpolation amount.
        /// </summary>
        private const int MAXIMUM_QUEUED_OVER_INTERPOLATION = 3;
        #endregion

        [Preserve]
        public ChildTransformTickSmoother() { }
        ~ChildTransformTickSmoother()
        {
            //This is a last resort for if something didnt deinitialize right.
            ResetState();
        }

        /// <summary>
        /// Initializes this smoother; should only be completed once.
        /// </summary>
        public void Initialize(NetworkObject nob, Transform graphicalObject, bool detach, float teleportDistance, float tickDelta, byte ownerInterpolation, TransformPropertiesFlag ownerSmoothedProperties, byte spectatorInterpolation, TransformPropertiesFlag specatorSmoothedProperties, AdaptiveInterpolationType adaptiveInterpolation)
        {
            ResetState();
            _detach = detach;
            _networkObject = nob;
            _transformProperties = CollectionCaches<TickTransformProperties>.RetrieveBasicQueue();
            _gfxInitializedOffsetValues = nob.transform.GetTransformOffsets(graphicalObject);
            _tickDelta = tickDelta;
            _graphicalObject = graphicalObject;
            _teleportThreshold = teleportDistance;
            _ownerInterpolation = ownerInterpolation;
            _spectatorInterpolation = spectatorInterpolation;
            _ownerSmoothedProperties = ownerSmoothedProperties;
            _spectatorSmoothedProperties = specatorSmoothedProperties;
            SetAdaptiveInterpolation(adaptiveInterpolation);
            UpdateInterpolation(0);
            _initialized = true;
        }

        /// <summary>
        /// Deinitializes this smoother resetting values.
        /// </summary>
        public void Deinitialize()
        {
            ResetState();
        }

        /// <summary>
        /// Updates interpolation based on localClient latency.
        /// </summary>
        private void UpdateInterpolation(uint clientStateTick)
        {
            if (_networkObject.IsServerStarted || _networkObject.IsOwner)
            {
                _interpolation = _ownerInterpolation;
            }
            else
            {
                if (_adaptiveInterpolation == AdaptiveInterpolationType.Off)
                {
                    _interpolation = _spectatorInterpolation;
                }
                else
                {
                    float interpolation;
                    TimeManager tm = _networkObject.TimeManager;
                    if (clientStateTick == 0)
                    {
                        //Not enough data to calculate; guestimate. This should only happen once.
                        float fRtt = (float)tm.RoundTripTime;
                        interpolation = (fRtt / 10f);
                    }
                    else
                    {
                        interpolation = (tm.LocalTick - clientStateTick);
                    }

                    interpolation *= GetInterpolationMultiplier();
                    interpolation = Mathf.Clamp(interpolation, 2f, (float)byte.MaxValue);
                    _interpolation = (byte)Mathf.CeilToInt(interpolation);

                    float GetInterpolationMultiplier()
                    {
                        switch (_adaptiveInterpolation)
                        {
                            case AdaptiveInterpolationType.ExtremelyLow:
                                return 0.2f;
                            case AdaptiveInterpolationType.VeryLow:
                                return 0.45f;
                            case AdaptiveInterpolationType.Low:
                                return 0.8f;
                            case AdaptiveInterpolationType.Moderate:
                                return 1.05f;
                            case AdaptiveInterpolationType.High:
                                return 1.25f;
                            case AdaptiveInterpolationType.VeryHigh:
                                return 1.5f;
                            //Make no changes for maximum.
                            default:
                                _networkObject.NetworkManager.LogError($"AdaptiveInterpolationType {_adaptiveInterpolation} is unhandled.");
                                return 1f;
                        }
                    }

                }
            }
        }

        internal void OnStartClient()
        {
            if (!_detach)
                return;

            _previousParent = _graphicalObject.parent;
            TransformProperties gfxWorldProperties = _graphicalObject.GetWorldProperties();
            _graphicalObject.SetParent(null);
            _graphicalObject.SetWorldProperties(gfxWorldProperties);
        }

        internal void OnStopClient()
        {
            if (!_detach || _previousParent == null || _graphicalObject == null)
                return;

            _graphicalObject.SetParent(_previousParent);
            _graphicalObject.SetWorldProperties(GetNetworkObjectWorldPropertiesWithOffset());
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        internal void Update()
        {
            if (!CanSmooth())
                return;

            if (UseAdaptiveMoveRates())
                AdaptiveMoveToTarget(Time.deltaTime);
            else
                BasicMoveToTarget(Time.deltaTime);
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPreTick.
        /// </summary>
        public void OnPreTick()
        {
            if (!CanSmooth())
                return;

            _preTicked = true;

            if (UseAdaptiveMoveRates())
                DiscardExcessiveTransformPropertiesQueue();
            else
                ClearTransformPropertiesQueue();
            //These only need to be set if still attached.
            if (!_detach)
                _gfxPreSimulateWorldValues = _graphicalObject.GetWorldProperties();
        }

        /// <summary>
        /// Called when the PredictionManager invokes OnPreReconcile.
        /// </summary>
        public void OnPreReconcile()
        {
            if (!_networkObject.IsObjectReconciling)
                return;

            uint clientStateTick = _networkObject.PredictionManager.ClientStateTick;
            _reconcileInterval = (clientStateTick - _lastReconcileTick);
            _lastReconcileTick = clientStateTick;

            UpdateInterpolation(clientStateTick);
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPostReplay.
        /// </summary>
        /// <param name="clientTick">Replay tick for the local client.</param>
        public void OnPostReplay(uint clientTick)
        {
            if (!UseAdaptiveMoveRates())
                return;
            if (_transformProperties.Count == 0)
                return;
            if (clientTick <= _teleportedTick)
                return;
            uint firstTick = _transformProperties.Peek().Tick;
            //Already in motion to first entry, or first entry passed tick.
            if (clientTick <= firstTick)
                return;

            ModifyTransformProperties(clientTick, firstTick);
        }

        /// <summary>
        /// Called when TimeManager invokes OnPostTick.
        /// </summary>
        /// <param name="clientTick">Local tick of the client.</param>
        public void OnPostTick(uint clientTick)
        {
            if (!CanSmooth())
                return;
            if (clientTick <= _teleportedTick)
                return;

            //If preticked then previous transform values are known.
            if (_preTicked)
            {
                if (UseAdaptiveMoveRates())
                    DiscardExcessiveTransformPropertiesQueue();
                else
                    ClearTransformPropertiesQueue();

                //Only needs to be put to pretick position if not detached.
                if (!_detach)
                    _graphicalObject.SetWorldProperties(_gfxPreSimulateWorldValues);

                AddTransformProperties(clientTick);
            }
            //If did not pretick then the only thing we can do is snap to instantiated values.
            else
            {
                //Only set to position if not to detach.
                if (!_detach)
                    _graphicalObject.SetWorldProperties(GetNetworkObjectWorldPropertiesWithOffset());
            }
        }

        /// <summary>
        /// Teleports the graphical to it's starting position and clears the internal movement queue.
        /// </summary>
        public void Teleport()
        {
            if (_networkObject == null)
                return;
            _teleportedTick = _networkObject.TimeManager.LocalTick;
            ClearTransformPropertiesQueue();
            TransformProperties startProperties = _networkObject.transform.GetWorldProperties();
            startProperties.Add(_gfxInitializedOffsetValues);
            _graphicalObject.SetWorldProperties(startProperties);
        }

        /// <summary>
        /// Clears the pending movement queue.
        /// </summary>
        private void ClearTransformPropertiesQueue()
        {
            _transformProperties.Clear();
            //Also unset move rates since there is no more queue.
            _moveRates = new MoveRates(MoveRatesCls.UNSET_VALUE);
        }

        /// <summary>
        /// Discards datas over interpolation limit from movement queue.
        /// </summary>
        private void DiscardExcessiveTransformPropertiesQueue()
        {
            if (!_useAdaptiveInterpolation)
            {
                _networkObject.NetworkManager.LogError($"This method should only be called when using adaptive interpolation.");
                return;
            }

            int propertiesCount = _transformProperties.Count;
            int dequeueCount = (propertiesCount - (_interpolation + MAXIMUM_QUEUED_OVER_INTERPOLATION));
            //If there are entries to dequeue.
            if (dequeueCount > 0)
            {
                TickTransformProperties tpp = default;
                for (int i = 0; i < dequeueCount; i++)
                    tpp = _transformProperties.Dequeue();

                SetMoveRates(tpp.Properties);
            }
        }

        /// <summary>
        /// Adds a new transform properties and sets move rates if needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddTransformProperties(uint tick)
        {
            TickTransformProperties tpp = new TickTransformProperties(tick, GetNetworkObjectWorldPropertiesWithOffset());
            _transformProperties.Enqueue(tpp);

            //If first entry then set move rates.
            if (_transformProperties.Count == 1)
            {
                TransformProperties gfxWorldProperties = _graphicalObject.GetWorldProperties();
                SetMoveRates(gfxWorldProperties);
            }
        }

        /// <summary>
        /// Modifies a transform property for a tick. This does not error check for empty collections.
        /// </summary>
        /// <param name="firstTick">First tick in the queue. If 0 this will be looked up.</param>
        private void ModifyTransformProperties(uint clientTick, uint firstTick)
        {
            uint tick = clientTick;
            /*Ticks will always be added incremental by 1 so it's safe to jump ahead the difference
            * of tick and firstTick. */
            int index = (int)(tick - firstTick);
            //Replace with new data.
            if (index < _transformProperties.Count)
            {
                if (tick != _transformProperties[index].Tick)
                {
                    //Should not be possible.
                }
                else
                {
                    _transformProperties[index] = new TickTransformProperties(tick, _networkObject.transform, _graphicalObject.localScale);
                }
            }
            else
            {
                //This should never happen.
            }
        }

        /// <summary>
        /// Returns TransformProperties of the NetworkObject with the graphicals world offset.
        /// </summary>
        /// <returns></returns>
        private TransformProperties GetNetworkObjectWorldPropertiesWithOffset() => _networkObject.transform.GetWorldProperties(_gfxInitializedOffsetValues);

        /// <summary>
        /// Returns if prediction can be used on this rigidbody.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            if (_graphicalObject == null)
                return false;

            return true;
        }

        /// <summary>
        /// Returns if to use adaptive move rates or not. 
        /// This is typically true when UseAdaptiveInterpolation is true, but some conditions can force basic move rates to be used.
        /// </summary>
        /// <returns></returns>
        private bool UseAdaptiveMoveRates()
        {
            if (!_useAdaptiveInterpolation)
                return false;
            if (_useAdaptiveInterpolation && _reconcileInterval == 1)
                return true;

            return false;
        }

        /// <summary>
        /// Updates move rates for adaptive or basic movement.
        /// </summary>
        /// <param name="prevValues"></param>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetMoveRates(TransformProperties prevValues)
        {
            if (UseAdaptiveMoveRates())
                SetAdaptiveMoveRates(prevValues);
            else
                SetBasicMoveRates(prevValues);
        }

        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        private void SetBasicMoveRates(TransformProperties prevValues)
        {
            int propertiesCount = _transformProperties.Count;
            if (propertiesCount == 0)
            {
                _moveRates = new MoveRates(MoveRatesCls.UNSET_VALUE);
                return;
            }

            TransformProperties nextValues = _transformProperties[propertiesCount - 1].Properties;

            /* If using adaptive interpolation then set the interpolation to half of
             * the clients ping. Otherwise use whatever interpolation was calculated. */
            byte interpolation = _interpolation;
            if (interpolation > 1 && _useAdaptiveInterpolation)
                interpolation /= 2;

            float duration = (_tickDelta * interpolation);
            /* If interpolation is 1 then add on a tiny amount
             * of more time to compensate for frame time, so that
             * the smoothing does not complete before the next tick,
             * as this would result in jitter. */
            if (interpolation == 1)
                duration += (1 / 55f);
            float teleportT = (_teleportThreshold * (float)interpolation);

            _moveRates = MoveRates.GetMoveRates(prevValues, nextValues, duration, teleportT);
            _moveRates.TimeRemaining = duration;
        }


        /// <summary>
        /// Sets new rates based on next entries in transformProperties queue, against a supplied TransformProperties.
        /// </summary>
        private void SetAdaptiveMoveRates(in TransformProperties prevValues)
        {
            if (_transformProperties.Count == 0)
            {
                _moveRates = new MoveRates(MoveRatesCls.UNSET_VALUE);
                return;
            }

            TransformProperties nextValues = _transformProperties.Peek().Properties;
            float duration = _tickDelta;
            float teleportT = _teleportThreshold;

            _moveRates = MoveRates.GetMoveRates(prevValues, nextValues, duration, teleportT);
            _moveRates.TimeRemaining = duration;

            SetMovementMultiplier();
        }

        private void SetMovementMultiplier()
        {
            /* If there's more in queue than interpolation then begin to move faster based on overage.
            * Move 5% faster for every overage. */
            int overInterpolation = (_transformProperties.Count - _interpolation);
            //If needs to be adjusted.
            if (overInterpolation != 0f)
            {
                _movementMultiplier += (0.015f * overInterpolation);
            }
            //If does not need to be adjusted.
            else
            {
                //If interpolation is 1 then slow down just barely to accomodate for frame delta variance.
                if (_interpolation == 1)
                    _movementMultiplier = 0.99f;
            }

            _movementMultiplier = Mathf.Clamp(_movementMultiplier, 0.95f, 1.05f);
        }


        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BasicMoveToTarget(float delta)
        {
            int tpCount = _transformProperties.Count;
            //No data.
            if (tpCount == 0)
                return;

            TickTransformProperties ttp = _transformProperties.Peek();
            TransformPropertiesFlag smoothedProperties = (_ownerOnPretick) ? _ownerSmoothedProperties : _spectatorSmoothedProperties;
            _moveRates.MoveWorldToTarget(_graphicalObject, ttp.Properties, smoothedProperties, delta);

            float tRemaining = _moveRates.TimeRemaining;
            //if TimeLeft is <= 0f then transform should be at goal.
            if (tRemaining <= 0f)
            {
                //If there are entries left then setup for the next.
                if (_transformProperties.Count > 0)
                {
                    SetBasicMoveRates(ttp.Properties);
                    //If delta is negative then call move again with abs.
                    if (tRemaining < 0f)
                        BasicMoveToTarget(Mathf.Abs(tRemaining));
                }
                //No remaining, set to snap.
                else
                {
                    ClearTransformPropertiesQueue();
                }
            }
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdaptiveMoveToTarget(float delta)
        {
            int tpCount = _transformProperties.Count;
            //No data.
            if (tpCount == 0)
                return;
            /* If buffer is considerably under goal then halt
             * movement. This will allow the buffer to grow. */
            if ((tpCount - _interpolation) < -4)
                return;

            TickTransformProperties ttp = _transformProperties.Peek();
            TransformPropertiesFlag smoothedProperties = (_ownerOnPretick) ? _ownerSmoothedProperties : _spectatorSmoothedProperties;
            _moveRates.MoveWorldToTarget(_graphicalObject, ttp.Properties, smoothedProperties, (delta * _movementMultiplier));

            float tRemaining = _moveRates.TimeRemaining;
            //if TimeLeft is <= 0f then transform is at goal. Grab a new goal if possible.
            if (tRemaining <= 0f)
            {
                //Dequeue current entry and if there's another call a move on it.
                _transformProperties.Dequeue();

                //If there are entries left then setup for the next.
                if (_transformProperties.Count > 0)
                {
                    SetAdaptiveMoveRates(ttp.Properties);
                    //If delta is negative then call move again with abs.
                    if (tRemaining < 0f)
                        AdaptiveMoveToTarget(Mathf.Abs(tRemaining));
                }
                //No remaining, set to snap.
                else
                {
                    ClearTransformPropertiesQueue();
                }
            }
        }

        public void ResetState()
        {
            if (!_initialized)
                return;

            if (_graphicalObject != null)
            {
                if (_networkObject != null)
                {
                    if (_detach)
                        _graphicalObject.SetParent(_networkObject.transform);
                    _graphicalObject.SetWorldProperties(GetNetworkObjectWorldPropertiesWithOffset());
                    _graphicalObject = null;
                }
                else if (_detach)
                {
                    MonoBehaviour.Destroy(_graphicalObject.gameObject);
                }
            }

            _networkObject = null;
            _teleportedTick = TimeManager.UNSET_TICK;
            _lastReconcileTick = TimeManager.UNSET_TICK;
            _reconcileInterval = RECONCILE_INTERVAL_DEFAULT;
            _movementMultiplier = 1f;
            CollectionCaches<TickTransformProperties>.StoreAndDefault(ref _transformProperties);
            _teleportThreshold = default;
            _moveRates = default;
            _preTicked = default;
            _gfxInitializedOffsetValues = default;
            _gfxPreSimulateWorldValues = default;
            _tickDelta = default;
            _interpolation = default;
        }

        public void InitializeState() { }
    }

}
