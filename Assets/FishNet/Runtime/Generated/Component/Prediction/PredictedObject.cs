using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    [AddComponentMenu("FishNet/Component/PredictedObject")]
    public partial class PredictedObject : NetworkBehaviour
    {
        #region Types.
        private enum CollectionState : byte
        {
            Unset = 0,
            Added = 1,
            Removed = 2,
        }

        /// <summary>
        /// How to smooth. Over the tick duration or specified time.
        /// </summary>
        public enum SmoothingDurationType : byte
        {
            Tick = 0,
            Time = 1
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
        /// Gets GraphicalObject.
        /// </summary>
        public Transform GetGraphicalObject => _graphicalObject;
        /// <summary>
        /// Sets GraphicalObject.
        /// </summary>
        /// <param name="value"></param>
        public void SetGraphicalObject(Transform value) => _graphicalObject = value;
        /// <summary>
        /// True to smooth graphical object over tick durations. While true objects will be smooth even with low tick rates, but the visual representation will be behind one tick.
        /// </summary>
        [Tooltip("True to smooth graphical object over tick durations. While true objects will be smooth even with low tick rates, but the visual representation will be behind one tick.")]
        [SerializeField]
        private bool _smoothTicks = true;
        /// <summary>
        /// Gets the value for SmoothTicks.
        /// </summary>
        /// <returns></returns>
        public bool GetSmoothTicks() => _smoothTicks;
        /// <summary>
        /// Sets the value for SmoothTicks.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public void SetSmoothTicks(bool value) => _smoothTicks = value;
        /// <summary>
        /// How to smooth desynchronizations. Tick will smooth over the tick while Time will smooth over a set duration.
        /// </summary>
        [Tooltip("How to smooth desynchronizations. Tick will smooth over the tick while Time will smooth over a set duration.")]
        [SerializeField]
        private SmoothingDurationType _durationType = SmoothingDurationType.Tick;
        /// <summary>
        /// Duration to smooth desynchronizations over.
        /// </summary>
        [Tooltip("Duration to smooth desynchronizations over.")]
        [Range(0.01f, 0.5f)]
        [SerializeField]
        private float _smoothingDuration = 0.125f;
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
        [Range(0f, float.MaxValue)]
        [SerializeField]
        private float _teleportThreshold = 1f;
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
        private float _positionMoveRate = -2;
        /// <summary>
        /// Local rotation of transform when instantiated.
        /// </summary>
        private Quaternion _instantiatedLocalRotation;
        /// <summary>
        /// How quickly to move towards TargetRotation.
        /// </summary>
        private float _rotationMoveRate = -2;
        /// <summary>
        /// PredictedObjects that are spawned for each NetworkManager.
        /// Ideally PredictedObjects will be under the RollbackManager but that requires cross-linking assemblies which isn't possible.
        /// Until codegen can be made to run on the Runtime folder without breaking user code updates this will have to do.
        /// </summary>
        [System.NonSerialized]
        private static Dictionary<NetworkManager, List<PredictedObject>> _predictedObjects = new Dictionary<NetworkManager, List<PredictedObject>>();
        /// <summary>
        /// Current state of this PredictedObject within PredictedObjects collection.
        /// </summary>
        private CollectionState _collectionState = CollectionState.Unset;
        #endregion

        private struct MovedTracker
        {
            public uint LocalTick;
            public bool Moved;

            public MovedTracker(uint localTick, bool moved)
            {
                LocalTick = localTick;
                Moved = moved;
            }
        }

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

            ConfigureRigidbodies();
            ConfigureNetworkTransform();
            //Set in awake so they are default.
            SetGraphicalPreviousProperties();
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

            if (_predictionType != PredictionType.Other)
                InstantiatedRigidbodyCountInternal++;
        }
        private void OnDisable()
        {
            //Only unsubscribe if client.
            if (base.IsClient)
                ChangeSubscriptions(false);

            if (_predictionType != PredictionType.Other)
                InstantiatedRigidbodyCountInternal--;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (base.IsServer)
            {
                _collectionState = CollectionState.Added;
                List<PredictedObject> collection;
                //Add new list to dictionary collection if needed.
                if (!_predictedObjects.TryGetValue(base.NetworkManager, out collection))
                {
                    collection = new List<PredictedObject>();
                    _predictedObjects.Add(base.NetworkManager, collection);
                }

                collection.Add(this);
            }

            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
            _instantiatedLocalPosition = _graphicalObject.localPosition;
            _instantiatedLocalRotation = _graphicalObject.localRotation;
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            Rigidbodies_OnSpawnServer(connection);
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
            if (base.IsServer)
            {
                if (_collectionState == CollectionState.Added)
                {
                    if (_predictedObjects.TryGetValue(base.NetworkManager, out List<PredictedObject> collection))
                    {
                        _collectionState = CollectionState.Removed;
                        collection.Remove(this);
                        if (collection.Count == 0)
                            _predictedObjects.Remove(base.NetworkManager);
                    }
                }
            }

            if (base.TimeManager != null)
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        private void OnDestroy()
        {
            RemoveFromPrefabObjects();
        }

        /// <summary>
        /// Removes this script from _predictedObjects.
        /// </summary>
        private void RemoveFromPrefabObjects()
        {
            //Already removed.
            if (_collectionState != CollectionState.Added)
                return;

            NetworkManager nm = base.NetworkManager;
            //If found then remove normally.
            if (nm != null)
            {
                if (_predictedObjects.TryGetValue(base.NetworkManager, out List<PredictedObject> collection))
                {
                    _collectionState = CollectionState.Removed;
                    collection.Remove(this);
                    if (collection.Count == 0)
                        _predictedObjects.Remove(base.NetworkManager);
                }
            }
            //NetworkManager isn't found, must check all entries. This would only happen if object didnt clean up from network properly.
            else
            {
                List<NetworkManager> removedEntries = new List<NetworkManager>();
                foreach (KeyValuePair<NetworkManager, List<PredictedObject>> item in _predictedObjects)
                {
                    NetworkManager key = item.Key;
                    if (key == null)
                    {
                        removedEntries.Add(key);
                    }
                    else
                    {
                        List<PredictedObject> collection = item.Value;
                        collection.Remove(this);
                        if (collection.Count == 0)
                            removedEntries.Add(key);
                    }
                }

                //Remove entries as needed.
                for (int i = 0; i < removedEntries.Count; i++)
                    _predictedObjects.Remove(removedEntries[i]);
            }
        }

        private void TimeManager_OnUpdate()
        {
            MoveToTarget();
        }

        private void TimeManager_OnPreTick()
        {

            if (CanSmooth())
            {
                /* Only snap to destination if using tick smoothing.
                 * This ensures the graphics will be at the proper location
                 * before the next movement rates are calculated. */
                if (_durationType == SmoothingDurationType.Tick)
                {
                    _graphicalObject.localPosition = _instantiatedLocalPosition;
                    _graphicalObject.localRotation = _instantiatedLocalRotation;
                }
                SetGraphicalPreviousProperties();
            }
        }

        protected void TimeManager_OnPostTick()
        {
            if (CanSmooth())
            {
                ResetGraphicalToPreviousProperties();
                SetGraphicalMoveRates();
            }
            Rigidbodies_TimeManager_OnPostTick();
        }

        /// <summary>
        /// Called before physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        protected virtual void TimeManager_OnPreReplicateReplay(PhysicsScene ps, PhysicsScene2D ps2d)
        {
            Rigidbodies_TimeManager_OnPreReplicateReplay(ps, ps2d);
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
                base.TimeManager.OnUpdate += TimeManager_OnUpdate;
                base.TimeManager.OnPreTick += TimeManager_OnPreTick;
                base.TimeManager.OnPreReplicateReplay += TimeManager_OnPreReplicateReplay;
                base.TimeManager.OnPreReconcile += TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReconcile += TimeManager_OnPostReconcile;
            }
            else
            {
                base.TimeManager.OnUpdate -= TimeManager_OnUpdate;
                base.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                base.TimeManager.OnPreReplicateReplay -= TimeManager_OnPreReplicateReplay;
                base.TimeManager.OnPreReconcile -= TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReconcile -= TimeManager_OnPostReconcile;
            }

            _subscribed = subscribe;
        }

        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        private void TimeManager_OnPreReconcile(NetworkBehaviour nb)
        {
            Rigidbodies_TimeManager_OnPreReconcile(nb);
        }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        private void TimeManager_OnPostReconcile(NetworkBehaviour nb)
        {
            Rigidbodies_TimeManager_OnPostReconcile(nb);
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
            if (!_smoothTicks)
                return false;
            //Only client needs smoothing.
            if (base.IsServerOnly)
                return false;

            return true;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        private void MoveToTarget()
        {
            //Not set, meaning movement doesnt need to happen or completed.
            if (_positionMoveRate == -2f && _rotationMoveRate == -2f)
                return;

            /* Only try to update properties if they have a valid move rate.
             * Properties may have 0f move rate if they did not change. */

            Transform t = _graphicalObject;
            float delta = Time.deltaTime;
            //Position.
            if (_positionMoveRate == -1f)
                t.localPosition = _instantiatedLocalPosition;
            else if (_positionMoveRate > 0f)
                t.localPosition = Vector3.MoveTowards(t.localPosition, _instantiatedLocalPosition, _positionMoveRate * delta);
            //Rotation.
            if (_rotationMoveRate == -1f)
                t.localRotation = _instantiatedLocalRotation;
            else if (_rotationMoveRate > 0f)
                t.localRotation = Quaternion.RotateTowards(t.localRotation, _instantiatedLocalRotation, _rotationMoveRate * delta);

            if (GraphicalObjectMatches(_instantiatedLocalPosition, _instantiatedLocalRotation))
            {
                _positionMoveRate = -2f;
                _rotationMoveRate = -2f;
            }
        }

        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        private void SetGraphicalMoveRates()
        {
            float timeManagerDelta = (float)base.TimeManager.TickDelta;
            float delta = (_durationType == SmoothingDurationType.Tick) ? timeManagerDelta : _smoothingDuration;
            
            /* delta can never be faster than tick rate, otherwise the object will always 
             * get to smoothing goal before the next tick. */
            if (delta < timeManagerDelta)
                delta = timeManagerDelta;

            float distance;
            distance = Vector3.Distance(_instantiatedLocalPosition, _graphicalObject.localPosition);
            //If qualifies for teleporting.
            if (_enableTeleport && distance >= _teleportThreshold)
            {
                _positionMoveRate = -1f;
                _rotationMoveRate = -1f;
            }
            //Smoothing.
            else
            {
                _positionMoveRate = (distance / delta);
                distance = Quaternion.Angle(_instantiatedLocalRotation, _graphicalObject.localRotation);
                if (distance > 0f)
                    _rotationMoveRate = (distance / delta);
            }
        }

        /// <summary>
        /// Caches the transforms current position and rotation.
        /// </summary>
        private void SetGraphicalPreviousProperties()
        {
            _previousPosition = _graphicalObject.position;
            _previousRotation = _graphicalObject.rotation;
        }

        /// <summary>
        /// Resets the transform to cached position and rotation of the transform.
        /// </summary>
        private void ResetGraphicalToPreviousProperties()
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
        /// Configures RigidbodyPauser with settings.
        /// </summary>
        private void ConfigureRigidbodies()
        {
            if (!IsRigidbodyPrediction)
                return;

            _rigidbodyPauser = new RigidbodyPauser();
            if (_predictionType == PredictionType.Rigidbody)
            {
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                _rigidbodyPauser.UpdateRigidbodies(transform, RigidbodyType.Rigidbody, true);
            }
            else
            {
                _rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rigidbodyPauser.UpdateRigidbodies(transform, RigidbodyType.Rigidbody2D, true);
            }
        }

        /// <summary>
        /// Configures NetworkTransform for prediction.
        /// </summary>
        private void ConfigureNetworkTransform()
        {
            if (!IsRigidbodyPrediction)
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