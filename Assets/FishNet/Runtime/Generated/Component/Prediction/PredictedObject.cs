using FishNet.Component.Transforming;
using FishNet.Utility.Extension;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using System;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    [AddComponentMenu("FishNet/Component/PredictedObject")]
    public partial class PredictedObject : NetworkBehaviour
    {
#if PREDICTION_1
        #region Types.
        /// <summary>
        /// How to favor smoothing for predicted objects.
        /// </summary>
        public enum SpectatorSmoothingType
        {
            /// <summary>
            /// Favor accurate collisions. With fast moving objects this may result in some jitter with higher latencies.
            /// </summary>
            Accuracy = 0,
            /// <summary>
            /// A mix between Accuracy and Smoothness.
            /// </summary>
            Mixed = 1,
            /// <summary>
            /// Prefer smooth movement and corrections. Fast moving objects may collide before the graphical representation catches up.
            /// </summary>
            Gradual = 2,
            /// <summary>
            /// Configure values to your preference.
            /// </summary>
            Custom = 3,
        }
        /// <summary>
        /// State of this object in a collision.
        /// </summary>
        private enum CollectionState : byte
        {
            Unset = 0,
            Added = 1,
            Removed = 2,
        }
        /// <summary>
        /// Type of prediction movement being used.
        /// </summary>
        internal enum PredictionType : byte
        {
            Other = 0,
            Rigidbody = 1,
            Rigidbody2D = 2
        }
        internal enum ResendType : byte
        {
            Disabled = 0,
            Interval = 1,
        }
        #endregion

        #region Public.
        /// <summary>
        /// True if the prediction type is for a rigidbody.
        /// </summary>
        public bool IsRigidbodyPrediction => (_predictionType == PredictionType.Rigidbody || _predictionType == PredictionType.Rigidbody2D);
        #endregion

        #region Serialized.
        /// <summary>
        /// True if this object implements replicate and reconcile methods.
        /// </summary>
        [Tooltip("True if this object implements replicate and reconcile methods.")]
        [SerializeField]
        private bool _implementsPredictionMethods = true;
        /// <summary>
        /// Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.
        /// </summary>
        [Tooltip("Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.")]
        [SerializeField]
        private Transform _graphicalObject;
        /// <summary>
        /// Gets GraphicalObject.
        /// </summary>
        public Transform GetGraphicalObject() => _graphicalObject;
        /// <summary>
        /// Sets GraphicalObject.
        /// </summary>
        /// <param name="value"></param>
        public void SetGraphicalObject(Transform value)
        {
            _graphicalObject = value;
            SetInstantiatedOffsetValues();
            _spectatorSmoother?.SetGraphicalObject(value);
            _ownerSmoother?.SetGraphicalObject(value);
        }
        /// <summary>
        /// True to enable teleport threshhold.
        /// </summary>
        [Tooltip("True to enable teleport threshhold.")]
        [SerializeField]
        private bool _enableTeleport;
        /// <summary>
        /// How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.
        /// </summary>
        [Tooltip("How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.")]
        [Range(0f, 200f)] //Unity bug? Values ~over 200f lose decimal display within inspector.
        [SerializeField]
        private float _teleportThreshold = 1f;
        /// <summary>
        /// Gets the value for SmoothTicks.
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// Sets the value for SmoothTicks.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <summary>
        /// True to smooth position on owner objects.
        /// </summary>
        [Tooltip("True to smooth position on owner objects.")]
        [SerializeField]
        private bool _ownerSmoothPosition = true;
        /// <summary>
        /// True to smooth rotation on owner objects.
        /// </summary>
        [Tooltip("True to smooth rotation on owner objects.")]
        [SerializeField]
        private bool _ownerSmoothRotation = true;
        /// <summary>
        /// How far in the past to keep the graphical object when owner. Using a value of 0 will disable interpolation.
        /// </summary>
        [Tooltip("How far in the past to keep the graphical object when owner. Using a value of 0 will disable interpolation.")]
        [Range(0, 255)]
        [SerializeField]
        private byte _ownerInterpolation = 1;
        /// <summary>
        /// Type of prediction movement which is being used.
        /// </summary>
        [Tooltip("Type of prediction movement which is being used.")]
        [SerializeField]
        private PredictionType _predictionType;
        /// <summary>
        /// Rigidbody to predict.
        /// </summary>
        [Tooltip("Rigidbody to predict.")]
        [SerializeField]
        private Rigidbody _rigidbody;
        /// <summary>
        /// Rigidbody2D to predict.
        /// </summary>
        [Tooltip("Rigidbody2D to predict.")]
        [SerializeField]
        private Rigidbody2D _rigidbody2d;
        /// <summary>
        /// True to smooth position on spectated objects.
        /// </summary>
        [Tooltip("True to smooth position on spectated objects.")]
        [SerializeField]
        private bool _spectatorSmoothPosition = true;
        /// <summary>
        /// True to smooth rotation on spectated objects.
        /// </summary>
        [Tooltip("True to smooth rotation on spectated objects.")]
        [SerializeField]
        private bool _spectatorSmoothRotation = true;
        /// <summary>
        /// How to favor smoothing for predicted objects.
        /// </summary>
        [Tooltip("How to favor smoothing for predicted objects.")]
        [SerializeField]
        private SpectatorSmoothingType _spectatorSmoothingType = SpectatorSmoothingType.Mixed;
        /// <summary>
        /// Custom settings for smoothing data.
        /// </summary>
        [Tooltip("Custom settings for smoothing data.")]
        [SerializeField]
        private SmoothingData _customSmoothingData = _mixedSmoothingData;
        /// <summary>
        /// Preview of selected preconfigured smoothing data. This is only used for the inspector.
        /// </summary>
        [SerializeField]
        private SmoothingData _preconfiguredSmoothingDataPreview = _mixedSmoothingData;
        /// <summary>
        /// Sets SpectactorSmoothingType value.
        /// </summary>
        /// <param name="value">Value to use.</param>
        public void SetSpectatorSmoothingType(SpectatorSmoothingType value)
        {
            if (base.IsSpawned)
                base.NetworkManager.LogWarning($"Spectator smoothing type may only be set before the object is spawned, such as after instantiating but before spawning.");
            else
                _spectatorSmoothingType = value;
        }

        ///// <summary>
        ///// How far in the past to keep the graphical object when not owner. Using a value of 0 will disable interpolation.
        ///// </summary>
        //[Tooltip("How far in the past to keep the graphical object when not owner. Using a value of 0 will disable interpolation.")]
        //[Range(0, 255)]
        //[SerializeField]
        //private byte _spectatorInterpolation = 4;
        ///// <summary>
        ///// Multiplier to apply to movement speed when buffer is over interpolation.
        ///// </summary>
        //[Tooltip("Multiplier to apply to movement speed when buffer is over interpolation.")]
        //[Range(0f, 5f)]
        //[SerializeField]
        //private float _overflowMultiplier = 0.1f;
        /// <summary>
        /// Multiplier applied to difference in velocity between ticks.
        /// Positive values will result in more velocity while lowers will result in less.
        /// A value of 1f will prevent any velocity from being lost between ticks, unless indicated by the server.
        /// </summary>
        [Tooltip("Multiplier applied to difference in velocity between ticks. Positive values will result in more velocity while lowers will result in less. A value of 1f will prevent any velocity from being lost between ticks, unless indicated by the server.")]
        [Range(-10f, 10f)]
        [SerializeField]
        private float _maintainedVelocity = 0f;
        /// <summary>
        /// How often to resend current values regardless if the state has changed. Using this value will consume more bandwidth but may be preferred if you want to force synchronization the object move on the client but not on the server.
        /// </summary>
        [Tooltip("How often to resend current values regardless if the state has changed. Using this value will consume more bandwidth but may be preferred if you want to force synchronization the object move on the client but not on the server.")]
        [SerializeField]
        private ResendType _resendType = ResendType.Disabled;
        /// <summary>
        /// How often in ticks to resend values.
        /// </summary>
        [Tooltip("How often in ticks to resend values.")]
        [SerializeField]
        private ushort _resendInterval = 30;
        /// <summary>
        /// NetworkTransform to configure.
        /// </summary>
        [Tooltip("NetworkTransform to configure.")]
        [SerializeField]
        private NetworkTransform _networkTransform;
        #endregion

        #region Private.
        /// <summary>
        /// True if client subscribed to events.
        /// </summary>
        private bool _clientSubscribed;
        /// <summary>
        /// True if this PredictedObject has been registered with the PredictionManager.
        /// </summary>
        private bool _registered;
        /// <summary>
        /// GraphicalObject position difference from this object when this is instantiated.
        /// </summary>
        private Vector3 _graphicalInstantiatedOffsetPosition;
        /// <summary>
        /// GraphicalObject rotation difference from this object when this is instantiated.
        /// </summary>
        private Quaternion _graphicalInstantiatedOffsetRotation;
        /// <summary>
        /// Cached localtick for performance.
        /// </summary>
        private uint _localTick;
        /// <summary>
        /// Smoothing component for this object when not owner.
        /// </summary>
        private PredictedObjectSpectatorSmoother _spectatorSmoother;
        /// <summary>
        /// Smoothing component for this object when owner.
        /// This component is also used for non-owned objects when as server.
        /// </summary>
        private PredictedObjectOwnerSmoother _ownerSmoother;
        #endregion

        private void Awake()
        {
            SetInstantiatedOffsetValues();
        }

        public override void OnStartNetwork()
        {
            /* If host then initialize owner smoother.
             * Host will use owner smoothing settings for more
             * accurate results. */
            if (base.IsHostStarted)
                InitializeSmoother(true);

            UpdateRigidbodiesCount(true);
            ConfigureRigidbodies();
            ConfigureNetworkTransform();
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            Rigidbodies_OnSpawnServer(connection);
        }

        public override void OnStartClient()
        {
            ChangeSubscriptions(true);
            Rigidbodies_OnStartClient();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            /* If owner or host then use the
             * owner smoother. The owner smoother
             * is not predictive and is preferred
             * for more real time graphical results. */
            if (base.IsOwner && !base.IsServerStarted)
            {
                /* If has prediction methods implement for owner,
                 * otherwise implement for spectator. */
                InitializeSmoother(_implementsPredictionMethods);
                /* Also set spectator smoothing if does not implement
                 * prediction methods as the spectator smoother is used
                 * for these scenarios. */
                if (!_implementsPredictionMethods)
                    SetTargetSmoothing(base.TimeManager.RoundTripTime, true);
            }
            //Not owner nor server, initialize spectator smoother if using rigidbodies.
            else if (_predictionType != PredictionType.Other)
            {
                InitializeSmoother(false);
                SetTargetSmoothing(base.TimeManager.RoundTripTime, true);
            }

            Rigidbodies_OnOwnershipClient(prevOwner);
        }

        public override void OnStopNetwork()
        {
            ChangeSubscriptions(false);
            UpdateRigidbodiesCount(false);
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        /// <summary>
        /// Updates Rigidbodies count on the PredictionManager.
        /// </summary>
        /// <param name="add"></param>
        private void UpdateRigidbodiesCount(bool add)
        {
            if (_registered == add)
                return;
            if (_predictionType == PredictionType.Other)
                return;

            NetworkManager nm = base.NetworkManager;
            if (nm == null)
                return;

            _registered = add;

            if (add)
            {
                nm.PredictionManager.AddRigidbodyCount(this);
                nm.PredictionManager.OnPreServerReconcile += PredictionManager_OnPreServerReconcile;
            }
            else
            {
                nm.PredictionManager.RemoveRigidbodyCount(this);
                nm.PredictionManager.OnPreServerReconcile -= PredictionManager_OnPreServerReconcile;
            }
        }

        /// <summary>
        /// Sets instantiated offset values for the graphical object.
        /// </summary>
        private void SetInstantiatedOffsetValues()
        {
            transform.SetTransformOffsets(_graphicalObject, ref _graphicalInstantiatedOffsetPosition, ref _graphicalInstantiatedOffsetRotation);
        }

        private void TimeManager_OnUpdate()
        {
            _spectatorSmoother?.ManualUpdate();
            _ownerSmoother?.ManualUpdate();
        }

        private void TimeManager_OnPreTick()
        {
            _localTick = base.TimeManager.LocalTick;
            _spectatorSmoother?.OnPreTick();
            _ownerSmoother?.OnPreTick();
        }

        protected void TimeManager_OnPostTick()
        {
            _spectatorSmoother?.OnPostTick();
            _ownerSmoother?.OnPostTick();
            Rigidbodies_TimeManager_OnPostTick();
        }


        /// <summary>
        /// Subscribes to events needed to function.
        /// </summary>
        /// <param name="subscribe"></param>
        private void ChangeSubscriptions(bool subscribe)
        {
            if (base.TimeManager == null)
                return;
            if (subscribe == _clientSubscribed)
                return;

            if (subscribe)
            {
                base.TimeManager.OnUpdate += TimeManager_OnUpdate;
                base.TimeManager.OnPreTick += TimeManager_OnPreTick;
                //Only client will use these events.
                if (!base.IsServerStarted)
                {
                    base.PredictionManager.OnPreReplicateReplay += PredictionManager_OnPreReplicateReplay;
                    base.PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
                    base.PredictionManager.OnPreReconcile += PredictionManager_OnPreReconcile;
                    base.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
                    base.TimeManager.OnRoundTripTimeUpdated += TimeManager_OnRoundTripTimeUpdated;
                }
            }
            else
            {
                base.TimeManager.OnUpdate -= TimeManager_OnUpdate;
                base.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                //Only client will use these events.
                if (!base.IsServerStarted)
                {
                    base.PredictionManager.OnPreReplicateReplay -= PredictionManager_OnPreReplicateReplay;
                    base.PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;
                    base.PredictionManager.OnPreReconcile -= PredictionManager_OnPreReconcile;
                    base.PredictionManager.OnPostReconcile -= PredictionManager_OnPostReconcile;
                    base.TimeManager.OnRoundTripTimeUpdated -= TimeManager_OnRoundTripTimeUpdated;
                }

                //Also some resets
                _lastStateLocalTick = 0;
                _rigidbodyStates.Clear();
                _rigidbody2dStates.Clear();
            }

            _clientSubscribed = subscribe;
        }

        private void TimeManager_OnRoundTripTimeUpdated(long obj)
        {
            Rigidbodies_OnRoundTripTimeUpdated(obj);
        }

        private void PredictionManager_OnPreServerReconcile(NetworkBehaviour obj)
        {
            SendRigidbodyState(obj);
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        protected virtual void PredictionManager_OnPreReplicateReplay(uint tick, PhysicsScene ps, PhysicsScene2D ps2d)
        {
            _spectatorSmoother?.OnPreReplay(tick);
            Rigidbodies_PredictionManager_OnPreReplicateReplay(tick, ps, ps2d);
        }

        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void PredictionManager_OnPostReplicateReplay(uint tick, PhysicsScene ps, PhysicsScene2D ps2d)
        {
            _spectatorSmoother?.OnPostReplay(tick);
            Rigidbodies_PredictionManager_OnPostReplicateReplay(tick, ps, ps2d);
        }

        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        private void PredictionManager_OnPreReconcile(NetworkBehaviour nb)
        {
            Rigidbodies_TimeManager_OnPreReconcile(nb);
        }

        /// <summary>
        /// Called after performing a reconcile on NetworkBehaviour.
        /// </summary>
        private void PredictionManager_OnPostReconcile(NetworkBehaviour nb)
        {
            Rigidbodies_TimeManager_OnPostReconcile(nb);
        }

        /// <summary>
        /// Initializes a smoother with configured values.
        /// </summary>
        private void InitializeSmoother(bool ownerSmoother)
        {
            ResetGraphicalTransform();

            if (ownerSmoother)
            {
                _ownerSmoother = new PredictedObjectOwnerSmoother();
                float teleportThreshold = (_enableTeleport) ? _teleportThreshold : -1f;
                _ownerSmoother.Initialize(this, _graphicalInstantiatedOffsetPosition, _graphicalInstantiatedOffsetRotation, _graphicalObject, _ownerSmoothPosition, _ownerSmoothRotation, _ownerInterpolation, teleportThreshold);
            }
            else
            {
                _spectatorSmoother = new PredictedObjectSpectatorSmoother();
                RigidbodyType rbType = (_predictionType == PredictionType.Rigidbody) ?
                    RigidbodyType.Rigidbody : RigidbodyType.Rigidbody2D;
                float teleportThreshold = (_enableTeleport) ? _teleportThreshold : -1f;
                _spectatorSmoother.Initialize(this, rbType, _rigidbody, _rigidbody2d, _graphicalObject, _spectatorSmoothPosition, _spectatorSmoothRotation, teleportThreshold);
            }

            void ResetGraphicalTransform()
            {
                _graphicalObject.position = (transform.position + _graphicalInstantiatedOffsetPosition);
                _graphicalObject.rotation = (_graphicalInstantiatedOffsetRotation * transform.rotation);
            }
        }

        /// <summary>
        /// Configures RigidbodyPauser with settings.
        /// </summary>
        private void ConfigureRigidbodies()
        {
            if (!IsRigidbodyPrediction)
                return;

            bool warn = false;
            _rigidbodyPauser = new RigidbodyPauser();
            if (_predictionType == PredictionType.Rigidbody)
            {
                if (_rigidbody.isKinematic)
                    warn = true;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                _rigidbodyPauser.UpdateRigidbodies(transform, RigidbodyType.Rigidbody, true);
            }
            else
            {
                if (_rigidbody2d.isKinematic || !_rigidbody2d.simulated)
                    warn = true;
                _rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rigidbodyPauser.UpdateRigidbodies(transform, RigidbodyType.Rigidbody2D, true);
            }

            if (warn)
                base.NetworkManager.LogWarning($"When using Kinematic or non-simulated rigidbodies you typically will want to use {nameof(PredictionType.Other)} and synchronize to spectators with a {nameof(NetworkTransform)}.");
        }

        /// <summary>
        /// Configures NetworkTransform for prediction.
        /// </summary>
        private void ConfigureNetworkTransform()
        {
            if (!IsRigidbodyPrediction)
                _networkTransform?.ConfigureForPrediction();
        }


#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (Application.isPlaying)
            {
                InitializeSmoother(true);
            }
            else
            {
                if (_spectatorSmoothingType == SpectatorSmoothingType.Accuracy)
                    _preconfiguredSmoothingDataPreview = _accurateSmoothingData;
                else if (_spectatorSmoothingType == SpectatorSmoothingType.Mixed)
                    _preconfiguredSmoothingDataPreview = _mixedSmoothingData;
                else if (_spectatorSmoothingType == SpectatorSmoothingType.Gradual)
                    _preconfiguredSmoothingDataPreview = _gradualSmoothingData;
            }
        }
#endif
#endif
    }


}