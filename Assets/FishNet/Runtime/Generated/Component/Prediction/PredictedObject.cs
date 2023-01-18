﻿using FishNet.Component.Transforming;
using FishNet.Utility.Extension;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    [AddComponentMenu("FishNet/Component/PredictedObject")]
    public partial class PredictedObject : NetworkBehaviour
    {
        #region Types.
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
        [Obsolete("Use GetInterpolation. This method no longer functions.")]
        public bool GetSmoothTicks() => true;
        /// <summary>
        /// Sets the value for SmoothTicks.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [Obsolete("Use SetInterpolation. This method no longer functions.")]
        public void SetSmoothTicks(bool value) { }
        /// <summary>
        /// How far in the past to keep the graphical object when owner. Using a value of 0 will disable interpolation.
        /// </summary>
        [Tooltip("How far in the past to keep the graphical object when owner. Using a value of 0 will disable interpolation.")]
        [Range(0, 255)]
        [SerializeField]
        private byte _ownerInterpolation = 1;
        /// <summary>
        /// Gets the iterpolation value to use when the owner of this object.
        /// </summary>
        /// <param name="asOwner">True to get the interpolation for when owner, false to get the interpolation for when a spectator.</param>
        public byte GetInterpolation(bool asOwner) => (asOwner) ? _ownerInterpolation : _spectatorInterpolation;
        /// <summary>
        /// Sets the interpolation value to use when the owner of this object.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="asOwner">True to set the interpolation for when owner, false to set interpolation for when a spectator.</param>
        public void SetInterpolation(byte value, bool asOwner)
        {
            if (asOwner)
            {
                _ownerInterpolation = value;
                _ownerSmoother?.SetInterpolation(value);
            }
            else
            {
                _spectatorInterpolation = value;
                _spectatorSmoother?.SetInterpolation(value);
            }
        }
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
        /// Time to smooth initial velocities when an object was previously stopped.
        /// </summary>
        [Tooltip("Time to smooth initial velocities when an object was previously stopped.")]
        [Range(0f, 3f)]
        [SerializeField]
        private float _spectatorSmoothingDuration = 0.025f;
        /// <summary>
        /// How far in the past to keep the graphical object when not owner. Using a value of 0 will disable interpolation.
        /// </summary>
        [Tooltip("How far in the past to keep the graphical object when not owner. Using a value of 0 will disable interpolation.")]
        [Range(0, 255)]
        [SerializeField]
        private byte _spectatorInterpolation = 1;
        /// <summary>
        /// Multiplier to apply to movement speed when buffer is over interpolation.
        /// </summary>
        [Tooltip("Multiplier to apply to movement speed when buffer is over interpolation.")]
        [Range(0f, 5f)]
        [SerializeField]
        private float _overflowMultiplier = 0.1f;
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
            base.OnStartNetwork();

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
            base.OnStartClient();
            ChangeSubscriptions(true);
            Rigidbodies_OnStartClient();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            /* If owner or host then use the
             * owner smoother. The owner smoother
             * is not predictive and is preferred
             * for more real time graphical results. */
            if (base.IsOwner || base.IsHost)
                InitializeSmoother(true);
            //Not owner nor server, initialize spectator smoother if using rigidbodies.
            else if (_predictionType != PredictionType.Other)
                InitializeSmoother(false);

            Rigidbodies_OnOwnershipClient(prevOwner);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            ChangeSubscriptions(false);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            UpdateRigidbodiesCount(false);
            if (base.TimeManager != null)
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
                base.PredictionManager.OnPreReplicateReplay += PredictionManager_OnPreReplicateReplay;
                base.PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
                base.PredictionManager.OnPreReconcile += PredictionManager_OnPreReconcile;
                base.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
            }
            else
            {
                base.TimeManager.OnUpdate -= TimeManager_OnUpdate;
                base.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                base.PredictionManager.OnPreReplicateReplay -= PredictionManager_OnPreReplicateReplay;
                base.PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;
                base.PredictionManager.OnPreReconcile -= PredictionManager_OnPreReconcile;
                base.PredictionManager.OnPostReconcile -= PredictionManager_OnPostReconcile;

                //Also some resets
                _lastStateLocalTick = 0;
                _rigidbodyStates.Clear();
                _rigidbody2dStates.Clear();
            }

            _clientSubscribed = subscribe;
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
            Rigidbodies_PredictionManager_OnPreReplicateReplay(tick, ps, ps2d);
        }

        /// <summary>
        /// Called after physics is simulated when replaying a replicate method.
        /// Contains the PhysicsScene and PhysicsScene2D which was simulated.
        /// </summary>
        private void PredictionManager_OnPostReplicateReplay(uint tick, PhysicsScene ps, PhysicsScene2D ps2d)
        {
            _spectatorSmoother?.OnPostReplay();
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
                _ownerSmoother.Initialize(this, _graphicalInstantiatedOffsetPosition, _graphicalInstantiatedOffsetRotation, _graphicalObject, _spectatorSmoothPosition, _spectatorSmoothRotation, _ownerInterpolation, teleportThreshold);
            }
            else
            {
                _spectatorSmoother = new PredictedObjectSpectatorSmoother();
                RigidbodyType rbType = (_predictionType == PredictionType.Rigidbody) ?
                    RigidbodyType.Rigidbody : RigidbodyType.Rigidbody2D;
                float teleportThreshold = (_enableTeleport) ? _teleportThreshold : -1f;
                _spectatorSmoother.Initialize(this, rbType, _rigidbody, _rigidbody2d, _graphicalObject, _spectatorSmoothPosition, _spectatorSmoothRotation, _spectatorSmoothingDuration, _spectatorInterpolation, _overflowMultiplier, teleportThreshold);
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

            _rigidbodyPauser = new RigidbodyPauser();
            if (_predictionType == PredictionType.Rigidbody)
            {
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                _rigidbodyPauser.UpdateRigidbodies(transform, RigidbodyType.Rigidbody, true, _graphicalObject);
            }
            else
            {
                _rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rigidbodyPauser.UpdateRigidbodies(transform, RigidbodyType.Rigidbody2D, true, _graphicalObject);
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
            if (Application.isPlaying)
            {
                InitializeSmoother(true);
                if (_predictionType != PredictionType.Other)
                    InitializeSmoother(false);
            }
        }
#endif
    }


}