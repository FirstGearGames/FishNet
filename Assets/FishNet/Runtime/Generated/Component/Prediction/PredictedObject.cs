using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    public partial class PredictedObject : NetworkBehaviour
    {
        #region Types.
        /// <summary>
        /// Type of prediction movement being used.
        /// </summary>
        internal enum PredictionType : byte
        {
            Other = 0,
            Rigidbody = 1,
            Rigidbody2D = 2
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
        /// Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.
        /// </summary>
        [Tooltip("Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.")]
        [SerializeField]
        private Transform _graphicalObject;
        /// <summary>
        /// True to smooth graphical object over tick durations. While true objects will be smooth even with low tick rates, but the visual representation will be behind one tick.
        /// </summary>
        [Tooltip("True to smooth graphical object over tick durations. While true objects will be smooth even with low tick rates, but the visual representation will be behind one tick.")]
        [SerializeField]
        private bool _smoothTicks = true;
        /// <summary>
        /// Sets the value for SmoothTicks.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public void SetSmoothTicks(bool value) => _smoothTicks = value;
        /// <summary>
        /// Gets the value for SmoothTicks.
        /// </summary>
        /// <returns></returns>
        public bool GetSmoothTicks() => _smoothTicks;
        /// <summary>
        /// Duration to smooth desynchronizations over.
        /// </summary>
        [Tooltip("Duration to smooth desynchronizations over.")]
        [Range(0.01f, 0.5f)]
        [SerializeField]
        private float _smoothingDuration = 0.125f;
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
        /// NetworkTransform to configure.
        /// </summary>
        [Tooltip("NetworkTransform to configure.")]
        [SerializeField]
        private NetworkTransform _networkTransform;
        /// <summary>
        /// How much of the previous velocity to retain when predicting. Default value is 0f. Increasing this value may result in overshooting with rigidbodies that do not behave naturally, such as controllers or vehicles.
        /// </summary>
        [Tooltip("How much of the previous velocity to retain when predicting. Default value is 0f. Increasing this value may result in overshooting with rigidbodies that do not behave naturally, such as controllers or vehicles.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float _predictionRatio = 0f;
        #endregion

        #region Private.
        /// <summary>
        /// True if subscribed to events.
        /// </summary>
        private bool _subscribed;
        /// <summary>
        /// Next tick to send data.
        /// </summary>
        private uint _nextSendTick;
        /// <summary>
        /// World position before transform was predicted or reset.
        /// </summary>
        private Vector3 _previousPosition;
        /// <summary>
        /// World rotation before transform was predicted or reset.
        /// </summary>
        private Quaternion _previousRotation;
        /// <summary>
        /// Local position of transform when instantiated.
        /// </summary>
        private Vector3 _instantiatedLocalPosition;
        /// <summary>
        /// How quickly to move towards TargetPosition.
        /// </summary>
        private float _positionMoveRate;
        /// <summary>
        /// Local rotation of transform when instantiated.
        /// </summary>
        private Quaternion _instantiatedLocalRotation;
        /// <summary>
        /// How quickly to move towards TargetRotation.
        /// </summary>
        private float _rotationMoveRate;
        #endregion

        #region Consts.
        /// <summary>
        /// How often to synchronize values from server to clients when no changes have been detected.
        /// </summary>
        protected const float SEND_INTERVAL = 1f;
        #endregion

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (!InitializeOnce())
                {
                    this.enabled = false;
                    return;
                }
            }

            ConfigureNetworkTransform();
            //Set in awake so they arent default.
            SetPreviousTransformProperties();
        }

        private void OnEnable()
        {
            /* Only subscribe if client. Client may not be set
             * yet but that's okay because the OnStartClient
             * callback will catch the subscription. This is here
             * should the user disable then re-enable the object after
             * it's initialized. */
            if (base.IsClient)
                ChangeSubscriptions(true);
        }
        private void OnDisable()
        {
            //Only unsubscribe if client.
            if (base.IsClient)
                ChangeSubscriptions(false);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
            _instantiatedLocalPosition = _graphicalObject.localPosition;
            _instantiatedLocalRotation = _graphicalObject.localRotation;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ChangeSubscriptions(true);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            ChangeSubscriptions(false);
        }
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager != null)
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        private void Update()
        {
            DifferenceSmoother_Update();
        }


        protected void TimeManager_OnPostTick()
        {
            if (CanSmooth())
            {
                ResetToTransformPreviousProperties();
                SetTransformMoveRates();
            }
            Rigidbodies_TimeManager_OnPostTick();
        }

        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        protected virtual void TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            Rigidbodies_TimeManager_OnPreReconcile(obj);
            DifferenceSmoother_TimeManager_OnPreReconcile(obj);
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        protected virtual void TimeManager_OnPostReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d)
        {
            Rigidbodies_TimeManager_OnPostReplicateReplay(ps, ps2d);
        }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        protected virtual void TimeManager_OnPostReconcile(NetworkBehaviour obj)
        {
            Rigidbodies_TimeManager_OnPostReconcile(obj);
            DifferenceSmoother_TimeManager_OnPostReconcile(obj);
        }

        private void TimeManager_OnPreTick()
        {
            if (CanSmooth())
                SetPreviousTransformProperties();
        }

        /// <summary>
        /// Subscribes to events needed to function.
        /// </summary>
        /// <param name="subscribe"></param>
        private void ChangeSubscriptions(bool subscribe)
        {
            if (base.TimeManager == null)
                return;
            if (subscribe == _subscribed)
                return;

            if (subscribe)
            {
                base.TimeManager.OnPreTick += TimeManager_OnPreTick;
                base.TimeManager.OnPreReconcile += TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReplicateReplay += TimeManager_OnPostReplicateReplay;
                base.TimeManager.OnPostReconcile += TimeManager_OnPostReconcile;
            }
            else
            {
                base.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                base.TimeManager.OnPreReconcile -= TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReplicateReplay -= TimeManager_OnPostReplicateReplay;
                base.TimeManager.OnPostReconcile -= TimeManager_OnPostReconcile;
            }

            _subscribed = subscribe;
        }


        /// <summary>
        /// Initializes this script for use. Returns true for success.
        /// </summary>
        private bool InitializeOnce()
        {
            //No graphical object, cannot smooth.
            if (_graphicalObject == null)
            {
                if (NetworkManager.StaticCanLog(LoggingType.Error))
                    Debug.LogError($"GraphicalObject is not set on {gameObject.name}. Initialization will fail.");
                return false;
            }

            return true;
        }


        /// <summary>
        /// Returns if prediction can be used on this rigidbody.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            //Only client needs smoothing.
            if (!base.IsClient)
                return false;
            if (!_smoothTicks)
                return false;

            return !base.IsServerOnly;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        private void MoveToTarget()
        {
            //Not set, meaning movement doesnt need to happen or completed.
            if (_positionMoveRate == -1f && _rotationMoveRate == -1f)
                return;

            Transform t = _graphicalObject;
            float delta = Time.deltaTime;
            if (_positionMoveRate > 0f)
                t.localPosition = Vector3.MoveTowards(t.localPosition, _instantiatedLocalPosition, _positionMoveRate * delta);
            if (_rotationMoveRate > 0f)
                t.localRotation = Quaternion.RotateTowards(t.localRotation, _instantiatedLocalRotation, _rotationMoveRate * delta);

            if (GraphicalObjectMatches(_instantiatedLocalPosition, _instantiatedLocalRotation))
            {
                _positionMoveRate = -1f;
                _rotationMoveRate = -1f;
            }

        }


        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        /// <param name="durationOverride">Smooth of this duration when not set to -1f. Otherwise TimeManager.TickDelta is used.</param>
        private void SetTransformMoveRates(float durationOverride = -1f)
        {
            float delta = (durationOverride == -1f) ? (float)base.TimeManager.TickDelta : durationOverride;
            float distance;

            distance = Vector3.Distance(_instantiatedLocalPosition, _graphicalObject.localPosition);
            _positionMoveRate = (distance / delta);
            distance = Quaternion.Angle(_instantiatedLocalRotation, _graphicalObject.localRotation);
            if (distance > 0f)
                _rotationMoveRate = (distance / delta);
        }


        /// <summary>
        /// Caches the transforms current position and rotation.
        /// </summary>
        private void SetPreviousTransformProperties()
        {
            _previousPosition = _graphicalObject.position;
            _previousRotation = _graphicalObject.rotation;
        }

        /// <summary>
        /// Resets the transform to cached position and rotation of the transform.
        /// </summary>
        private void ResetToTransformPreviousProperties()
        {
            _graphicalObject.SetPositionAndRotation(_previousPosition, _previousRotation);
        }



        /// <summary>
        /// Returns if this transform matches arguments.
        /// </summary>
        /// <returns></returns>
        protected bool GraphicalObjectMatches(Vector3 position, Quaternion rotation)
        {
            return (_graphicalObject.localPosition == position && _graphicalObject.localRotation == rotation);
        }

        /// <summary>
        /// Configures NetworkTransform for prediction.
        /// </summary>
        private void ConfigureNetworkTransform()
        {
            if (_predictionType == PredictionType.Other)
                _networkTransform?.ConfigureForCSP();
        }


#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (_graphicalObject != null && _graphicalObject.parent == null)
            {
                Debug.LogError($"The graphical object may not be the root of the transform. Your graphical objects must be beneath your prediction scripts so that they may be smoothed independently during desynchronizations.");
                _graphicalObject = null;
                return;
            }

            ConfigureNetworkTransform();
        }
#endif
    }


}