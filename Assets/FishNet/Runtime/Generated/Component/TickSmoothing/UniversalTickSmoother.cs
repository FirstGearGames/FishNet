using System;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Component.Transforming.Beta
{
    /// <summary>
    /// This class is under regular development and it's API may change at any time.
    /// </summary>
    public sealed class UniversalTickSmoother : IResettable
    {
        #region Types.
        [Preserve]
        private struct TickTransformProperties
        {
            public readonly uint Tick;
            public readonly TransformProperties Properties;

            public TickTransformProperties(uint tick, Transform t)
            {
                Tick = tick;
                Properties = new(t.localPosition, t.localRotation, t.localScale);
            }

            public TickTransformProperties(uint tick, Transform t, Vector3 localScale)
            {
                Tick = tick;
                Properties = new(t.localPosition, t.localRotation, localScale);
            }

            public TickTransformProperties(uint tick, TransformProperties tp)
            {
                Tick = tick;
                Properties = tp;
            }

            public TickTransformProperties(uint tick, TransformProperties tp, Vector3 localScale)
            {
                Tick = tick;
                tp.Scale = localScale;
                Properties = tp;
            }
        }
        #endregion

        #region Private.
        /// <summary>
        /// How quickly to move towards goal values.
        /// </summary>
        private MoveRates _moveRates = new(MoveRatesCls.UNSET_VALUE);
        /// <summary>
        /// True if a pretick occurred since last postTick.
        /// </summary>
        private bool _preTicked;
        /// <summary>
        /// World offset values of the graphical from the NetworkObject during initialization.
        /// </summary>
        private TransformProperties _graphicsInitializedOffsetValues;
        /// <summary>
        /// World values of the graphical after it's been aligned to initialized values in PreTick.
        /// </summary>
        private TransformProperties _gfxPreTickWorldValues;
        /// <summary>
        /// Cached value of adaptive interpolation value.
        /// </summary>
        private AdaptiveInterpolationType _cachedAdaptiveInterpolationValue;
        /// <summary>
        /// Cached value of flat interpolation value.
        /// </summary>
        private byte _cachedInterpolationValue;
        /// <summary>
        /// Cached properties to smooth of the graphical.
        /// </summary>
        private TransformPropertiesFlag _cachedSmoothedProperties;
        /// <summary>
        /// Squared distance target must travel to cause a teleport.
        /// </summary>
        private float _cachedTeleportThreshold;
        /// <summary>
        /// True if to detach on network start.
        /// </summary>
        private bool _detachOnStart;
        /// <summary>
        /// True to re-attach on network stop.
        /// </summary>
        private bool _attachOnStop;
        /// <summary>
        /// Transform the graphics shoulod follow.
        /// </summary>
        private Transform _targetTransform;
        /// <summary>
        /// Cached value of the object to smooth.
        /// </summary>
        private Transform _graphicalTransform;
        /// <summary>
        /// TimeManager tickDelta.
        /// </summary>
        private float _tickDelta;
        /// <summary>
        /// NetworkBehaviour this is initialized for. Value may be null.
        /// </summary>
        private NetworkBehaviour _initializingNetworkBehaviour;
        /// <summary>
        /// Value to multiply movement by. This is used to reduce or increase the rate the movement buffer is consumed.
        /// </summary>
        private float _movementMultiplier = 1f;
        /// <summary>
        /// TransformProperties to move towards.
        /// </summary>
        private BasicQueue<TickTransformProperties> _transformProperties;
        /// <summary>
        /// Previous parent the graphical was attached to.
        /// </summary>
        private Transform _previousParent;
        /// <summary>
        /// True if to smooth using owner settings, false for spectator settings.
        /// This is only used for performance gains.
        /// </summary>
        private bool _useOwnerSettings;
        /// <summary>
        /// Last tick this was teleported on.
        /// </summary>
        private uint _teleportedTick = TimeManager.UNSET_TICK;
        /// <summary>
        /// Frame which smoothing can start.
        /// </summary>
        private int _startFrame;
        /// <summary>
        /// Current interpolation value, be it a flat value or adaptive.
        /// </summary>
        private byte _realtimeInterpolation;
        /// <summary>
        /// Settings to use for owners.
        /// </summary>
        private MovementSettings _ownerMovementSettings;
        /// <summary>
        /// Settings to use for spectators.
        /// </summary>
        private MovementSettings _spectatorMovementSettings;
        /// <summary>
        /// True if initialized.
        /// </summary>
        private bool _initialized;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum allowed entries to be queued over the interpolation amount.
        /// </summary>
        private const int MAXIMUM_QUEUED_OVER_INTERPOLATION = 3;
        #endregion

        [Preserve]
        public UniversalTickSmoother() { }

        ~UniversalTickSmoother()
        {
            //This is a last resort for if something didnt deinitialize right.
            ResetState();
        }

        /// <summary>
        /// Updates the smoothedProperties value.
        /// </summary>
        /// <param name="value">New value.</param>
        public void SetSmoothedProperties(TransformPropertiesFlag value) => _ownerMovementSettings.SmoothedProperties = value;

        /// <summary>
        /// Updates the interpolationValue when not using adaptive interpolation. Calling this method will also disable adaptive interpolation.
        /// </summary>
        /// <param name="value"></param>
        public void SetInterpolationValue(byte value, bool forOwner) => SetInterpolationValue(value, forOwner, unsetAdaptiveInterpolation: true);

        /// <summary>
        /// Updates the interpolationValue when not using adaptive interpolation. Calling this method will also disable adaptive interpolation.
        /// </summary>
        private void SetInterpolationValue(byte value, bool forOwner, bool unsetAdaptiveInterpolation)
        {
            if (value < 1)
                value = 1;

            if (forOwner)
                _ownerMovementSettings.InterpolationValue = value;
            else
                _spectatorMovementSettings.InterpolationValue = value;

            if (unsetAdaptiveInterpolation)
                SetAdaptiveInterpolation(AdaptiveInterpolationType.Off, forOwner);
        }

        /// <summary>
        /// Updates the adaptiveInterpolation value.
        /// </summary>
        /// <param name="adaptiveInterpolation">New value.</param>
        public void SetAdaptiveInterpolation(AdaptiveInterpolationType value, bool forOwner)
        {
            if (forOwner)
                _ownerMovementSettings.AdaptiveInterpolationValue = value;
            else
                _spectatorMovementSettings.AdaptiveInterpolationValue = value;

            UpdateRealtimeInterpolation(TimeManager.UNSET_TICK);
        }

        public void Initialize(InitializationSettings initializationSettings, MovementSettings ownerSettings, MovementSettings spectatorSettings)
        {
            ResetState();

            Transform graphicalTransform = initializationSettings.GraphicalTransform;
            Transform targetTransform = initializationSettings.TargetTransform;

            if (!TransformsAreValid(graphicalTransform, targetTransform))
                return;

            _transformProperties = CollectionCaches<TickTransformProperties>.RetrieveBasicQueue();
            _ownerMovementSettings = ownerSettings;
            _spectatorMovementSettings = spectatorSettings;

            _initializingNetworkBehaviour = initializationSettings.InitializingNetworkBehaviour;
            _targetTransform = targetTransform;
            _graphicalTransform = graphicalTransform;
            _tickDelta = initializationSettings.TickDelta;
            _detachOnStart = initializationSettings.DetachOnStart;
            _attachOnStop = initializationSettings.AttachOnStop;

            SetCaches(GetUseOwnerSettings());

            //Use set method as it has sanity checks.
            SetInterpolationValue(_ownerMovementSettings.InterpolationValue, forOwner: true, unsetAdaptiveInterpolation: false);
            SetInterpolationValue(_spectatorMovementSettings.InterpolationValue, forOwner: true, unsetAdaptiveInterpolation: false);

            SetAdaptiveInterpolation(_ownerMovementSettings.AdaptiveInterpolationValue, forOwner: true);
            SetAdaptiveInterpolation(_spectatorMovementSettings.AdaptiveInterpolationValue, forOwner: false);

            _graphicsInitializedOffsetValues = _targetTransform.GetTransformOffsets(_graphicalTransform);

            _initialized = true;
        }

        /// <summary>
        /// Returns if configured transforms are valid.
        /// </summary>
        /// <returns></returns>
        private bool TransformsAreValid(Transform graphicalTransform, Transform targetTransform)
        {
            if (graphicalTransform == null)
            {
                NetworkManagerExtensions.LogError($"Graphical transform cannot be null.");
                return false;
            }
            if (targetTransform == null)
            {
                NetworkManagerExtensions.LogError($"Target transform on {graphicalTransform} cannot be null.");
                return false;
            }
            if (targetTransform == graphicalTransform)
            {
                NetworkManagerExtensions.LogError($"Target transform cannot be the same as graphical transform on {graphicalTransform}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if to use adaptive interpolation.
        /// </summary>
        /// <returns></returns>
        private bool GetUseAdaptiveInterpolation()
        {
            if (_cachedAdaptiveInterpolationValue == AdaptiveInterpolationType.Off || _initializingNetworkBehaviour == null || _initializingNetworkBehaviour.IsServerStarted)
                return false;

            return true;
        }

        /// <summary>
        /// Gets if to use owner values.
        /// </summary>
        /// <remarks>OwnerSettings can be used to read determine this as both owner and spectator settings will have the name InitializingNetworkBehaviour.</remarks>
        /// <returns></returns>
        private bool GetUseOwnerSettings() => (_initializingNetworkBehaviour == null) || _initializingNetworkBehaviour.IsOwner || !_initializingNetworkBehaviour.Owner.IsValid;

        /// <summary>
        /// Updates OwnerDuringPreTick value and caches if needed.
        /// </summary>
        private void SetUseOwnerSettings(bool value, bool force = false)
        {
            if (value == _useOwnerSettings && !force)
                return;

            _useOwnerSettings = value;

            SetCaches(value);
        }

        /// <summary>
        /// Updates OwnerDuringPreTick value and caches if needed.
        /// </summary>
        private void SetCaches(bool useOwnerSettings)
        {
            MovementSettings movementSettings = (useOwnerSettings) ? _ownerMovementSettings : _spectatorMovementSettings;

            _cachedSmoothedProperties = movementSettings.SmoothedProperties;
            _cachedAdaptiveInterpolationValue = movementSettings.AdaptiveInterpolationValue;
            _cachedInterpolationValue = movementSettings.InterpolationValue;

            _cachedTeleportThreshold = (movementSettings.EnableTeleport) ? (movementSettings.TeleportThreshold * movementSettings.TeleportThreshold) : MoveRatesCls.UNSET_VALUE;
        }

        /// <summary>
        /// Deinitializes this smoother resetting values.
        /// </summary>
        public void Deinitialize()
        {
            ResetState();
            _initialized = false;
        }

        /// <summary>
        /// Updates interpolation based on localClient latency when using adaptive interpolation, or uses set value when adaptive interpolation is off.
        /// </summary>
        public void UpdateRealtimeInterpolation(uint clientStateTick)
        {
            /*  If not networked, server is started, or if not
             * using adaptive interpolation then use
             * flat interpolation.*/
            if (!GetUseAdaptiveInterpolation())
            {
                _realtimeInterpolation = _cachedInterpolationValue;
                return;
            }

            /* If here then adaptive interpolation is being calculated. */

            float interpolation;
            TimeManager tm = _initializingNetworkBehaviour.TimeManager;
            if (clientStateTick == TimeManager.UNSET_TICK)
            {
                //Calculate roughly what client state tick would be.
                uint rttToTicks = tm.TimeToTicks(tm.RoundTripTime / 2);

                long estimatedClientStateTick = (long)(rttToTicks / 2) - tm.NetworkManager.PredictionManager.StateInterpolation;
                
                clientStateTick = (estimatedClientStateTick < 0) ? 0 : (uint)estimatedClientStateTick;
            }

            interpolation = (tm.LocalTick - clientStateTick);

            interpolation *= GetInterpolationMultiplier();
            interpolation = Math.Clamp(interpolation, 2f, byte.MaxValue);
            /* Only update realtime interpolation if it changed more than 1
             * tick. This is to prevent excessive changing of interpolation value, which
             * could result in noticeable speed ups/slow downs given movement multiplier
             * may change when buffer is too full or short. */
            if (Math.Abs(_realtimeInterpolation - interpolation) > 1)
                _realtimeInterpolation = (byte)Mathf.CeilToInt(interpolation);

            float GetInterpolationMultiplier()
            {
                switch (_cachedAdaptiveInterpolationValue)
                {
                    case AdaptiveInterpolationType.ExtremelyLow:
                        return 0.075f;
                    case AdaptiveInterpolationType.VeryLow:
                        return 0.15f;
                    case AdaptiveInterpolationType.Low:
                        return 0.3f;
                    case AdaptiveInterpolationType.Moderate:
                        return 0.6f;
                    case AdaptiveInterpolationType.High:
                        return 1.05f;
                    case AdaptiveInterpolationType.VeryHigh:
                        return 1.15f;
                    //Make no changes for maximum.
                    default:
                        NetworkManagerExtensions.LogError($"AdaptiveInterpolationType {_cachedAdaptiveInterpolationValue} is unhandled.");
                        return 1f;
                }
            }
        }

        /// <summary>
        /// This should be called when OnStartClient is invoked on the initializing NetworkBehaviour.
        /// </summary>
        /// <remarks>This does not need to be called if there is no initializing NetworkBehaviour.</remarks>
        public void StartSmoother()
        {
            DetachOnStart();
        }

        /// <summary>
        /// This should be called when OnStopClient is invoked on the initializing NetworkBehaviour.
        /// </summary>
        /// <remarks>This does not need to be called if there is no initializing NetworkBehaviour.</remarks>
        internal void StopSmoother()
        {
            if (!_detachOnStart)
                return;
            if (_previousParent == null || _graphicalTransform == null)
                return;

            AttachOnStop();
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        public void OnUpdate(float delta)
        {
            if (!CanSmooth())
                return;

            MoveToTarget(delta);
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPreTick.
        /// </summary>
        public void OnPreTick()
        {
            if (!CanSmooth())
                return;

            SetUseOwnerSettings(GetUseOwnerSettings());

            _preTicked = true;
            DiscardExcessiveTransformPropertiesQueue();
            _gfxPreTickWorldValues = _graphicalTransform.GetWorldProperties();
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPostReplay.
        /// </summary>
        /// <param name="clientTick">Replay tick for the local client.</param>
        /// <remarks>This is dependent on the initializing NetworkBehaviour being set.</remarks>
        public void OnPostReplicateReplay(uint clientTick)
        {
            if (!NetworkObjectIsReconciling())
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
                DiscardExcessiveTransformPropertiesQueue();

                //Only needs to be put to pretick position if not detached.
                if (!_detachOnStart)
                    _graphicalTransform.SetWorldProperties(_gfxPreTickWorldValues);

                AddTransformProperties(clientTick);
            }
            //If did not pretick then the only thing we can do is snap to instantiated values.
            else
            {
                //Only set to position if not to detach.
                if (!_detachOnStart)
                    _graphicalTransform.SetWorldProperties(GetNetworkObjectWorldPropertiesWithOffset());
            }
        }

        /// <summary>
        /// Returns if the initialized NetworkBehaviour's NetworkObject is reconcilling.
        /// </summary>
        private bool NetworkObjectIsReconciling() => (_initializingNetworkBehaviour == null || _initializingNetworkBehaviour.NetworkObject.IsObjectReconciling);

        /// <summary>
        /// Teleports the graphical to it's starting position and clears the internal movement queue.
        /// </summary>
        /// <remarks>This is dependent on the initializing NetworkBehaviour being set if using adaptive interpolation.</remarks>
        public void Teleport()
        {
            if (_initializingNetworkBehaviour == null)
                return;

            //If using adaptive interpolation then set the tick which was teleported.
            if (_ownerMovementSettings.AdaptiveInterpolationValue != AdaptiveInterpolationType.Off && _initializingNetworkBehaviour != null)
                _teleportedTick = _initializingNetworkBehaviour.TimeManager.LocalTick;

            ClearTransformPropertiesQueue();

            TransformProperties startProperties = _gfxPreTickWorldValues;
            startProperties.Add(_graphicsInitializedOffsetValues);
            _graphicalTransform.SetWorldProperties(startProperties);
        }

        /// <summary>
        /// Clears the pending movement queue.
        /// </summary>
        private void ClearTransformPropertiesQueue()
        {
            _transformProperties.Clear();
            //Also unset move rates since there is no more queue.
            _moveRates = new(MoveRatesCls.UNSET_VALUE);
        }

        /// <summary>
        /// Discards datas over interpolation limit from movement queue.
        /// </summary>
        private void DiscardExcessiveTransformPropertiesQueue()
        {
            int propertiesCount = _transformProperties.Count;
            int dequeueCount = (propertiesCount - (_realtimeInterpolation + MAXIMUM_QUEUED_OVER_INTERPOLATION));
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
        private void AddTransformProperties(uint tick)
        {
            TickTransformProperties tpp = new(tick, GetNetworkObjectWorldPropertiesWithOffset());
            _transformProperties.Enqueue(tpp);

            //If first entry then set move rates.
            if (_transformProperties.Count == 1)
            {
                TransformProperties gfxWorldProperties = _graphicalTransform.GetWorldProperties();
                SetMoveRates(gfxWorldProperties);
                _startFrame = Time.frameCount + 1;
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
                    _transformProperties[index] = new(tick, GetNetworkObjectWorldPropertiesWithOffset(), _graphicalTransform.localScale);
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
        private TransformProperties GetNetworkObjectWorldPropertiesWithOffset() => _targetTransform.GetWorldProperties(_graphicsInitializedOffsetValues);

        /// <summary>
        /// Returns if prediction can be used on this rigidbody.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            //No graphical object is set.
            if (_graphicalTransform == null)
                return false;

            //Server only does not smooth.
            NetworkBehaviour nb = _initializingNetworkBehaviour;
            //If NB is null then we need to try and check instance finder.
            if (nb == null && InstanceFinder.IsServerOnlyStarted)
                return false;
            //Otherwise, use nb.
            if (nb != null && nb.IsServerOnlyStarted)
                return false;

            return true;
        }

        /// <summary>
        /// Sets new rates based on next entries in transformProperties queue, against a supplied TransformProperties.
        /// </summary>
        private void SetMoveRates(in TransformProperties prevValues)
        {
            if (_transformProperties.Count == 0)
            {
                _moveRates = new(MoveRatesCls.UNSET_VALUE);
                return;
            }

            TransformProperties nextValues = _transformProperties.Peek().Properties;

            float duration = _tickDelta;

            _moveRates = MoveRates.GetMoveRates(prevValues, nextValues, duration, _cachedTeleportThreshold);
            _moveRates.TimeRemaining = duration;

            SetMovementMultiplier();
        }

        private void SetMovementMultiplier()
        {
            /* If there's more in queue than interpolation then begin to move faster based on overage.
             * Move 5% faster for every overage. */
            int overInterpolation = (_transformProperties.Count - _realtimeInterpolation);
            //If needs to be adjusted.
            if (overInterpolation != 0)
            {
                _movementMultiplier += (0.015f * overInterpolation);
            }
            //If does not need to be adjusted.
            else
            {
                //If interpolation is 1 then slow down just barely to accomodate for frame delta variance.
                if (_realtimeInterpolation == 1)
                    _movementMultiplier = 1f;
            }

            _movementMultiplier = Mathf.Clamp(_movementMultiplier, 0.95f, 1.05f);
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        private void MoveToTarget(float delta)
        {
            if (Time.frameCount < _startFrame)
                return;
            int tpCount = _transformProperties.Count;
            //No data.
            if (tpCount == 0)
                return;
            /* If buffer is considerably under goal then halt
             * movement. This will allow the buffer to grow. */
            if ((tpCount - _realtimeInterpolation) < -4)
                return;

            TickTransformProperties ttp = _transformProperties.Peek();

            TransformPropertiesFlag smoothedProperties = _cachedSmoothedProperties;
            _moveRates.MoveWorldToTarget(_graphicalTransform, ttp.Properties, smoothedProperties, (delta * _movementMultiplier));

            float tRemaining = _moveRates.TimeRemaining;
            //if TimeLeft is <= 0f then transform is at goal. Grab a new goal if possible.
            if (tRemaining <= 0f)
            {
                //Dequeue current entry and if there's another call a move on it.
                _transformProperties.Dequeue();

                //If there are entries left then setup for the next.
                if (_transformProperties.Count > 0)
                {
                    SetMoveRates(ttp.Properties);
                    //If delta is negative then call move again with abs.
                    if (tRemaining < 0f)
                        MoveToTarget(Mathf.Abs(tRemaining));
                }
                //No remaining, set to snap.
                else
                {
                    ClearTransformPropertiesQueue();
                }
            }
        }

        private void DetachOnStart()
        {
            if (!_detachOnStart)
                return;

            _previousParent = _graphicalTransform.parent;
            TransformProperties gfxWorldProperties = _graphicalTransform.GetWorldProperties();
            _graphicalTransform.SetParent(null);
            _graphicalTransform.SetWorldProperties(gfxWorldProperties);
        }

        /// <summary>
        /// Attachs to Target transform is possible.
        /// </summary>
        private void AttachOnStop()
        {
            if (!_attachOnStop)
                return;
            if (_graphicalTransform == null)
                return;

            if (_targetTransform != null && _targetTransform != _graphicalTransform.parent)
            {
                //Check isQuitting for UnityEditor fix //https://github.com/FirstGearGames/FishNet/issues/818
                if (_detachOnStart && !ApplicationState.IsQuitting())
                    _graphicalTransform.SetParent(_targetTransform);
                _graphicalTransform.SetWorldProperties(GetNetworkObjectWorldPropertiesWithOffset());
            }
            else if (_detachOnStart)
            {
                UnityEngine.Object.Destroy(_graphicalTransform.gameObject);
            }
        }

        public void ResetState()
        {
            if (!_initialized)
                return;

            AttachOnStop();

            _initializingNetworkBehaviour = null;
            _graphicalTransform = null;
            _targetTransform = null;

            _teleportedTick = TimeManager.UNSET_TICK;
            _movementMultiplier = 1f;
            CollectionCaches<TickTransformProperties>.StoreAndDefault(ref _transformProperties);
            _moveRates = default;
            _preTicked = default;
            _graphicsInitializedOffsetValues = default;
            _gfxPreTickWorldValues = default;
            _realtimeInterpolation = default;
        }

        public void InitializeState() { }
    }
}