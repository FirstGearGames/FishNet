using FishNet.Component.Prediction;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object
{
#if PREDICTION_V2
    public partial class NetworkObject : MonoBehaviour
    {
        #region Types.
#if PREDICTION_V2
        /// <summary>
        /// Type of prediction movement being used.
        /// </summary>
        [System.Serializable]
        internal enum PredictionType : byte
        {
            Other = 0,
            Rigidbody = 1,
            Rigidbody2D = 2
        }
#endif
        #endregion

        #region Public.
#if PREDICTION_V2
        /// <summary>
        /// True if a reconcile is occuring on any NetworkBehaviour that is on or nested of this NetworkObject. Runtime NetworkBehaviours are not included, such as if you child a NetworkObject to another at runtime.
        /// </summary>
        public bool IsObjectReconciling { get; private set; }
#endif
        /// <summary>
        /// Last tick this object replicated.
        /// </summary>
        internal EstimatedTick ReplicateTick { get; private set; } = new EstimatedTick();
        /// <summary>
        /// Last tick to replicate even if out of order. This could be from tick events or even replaying inputs.
        /// </summary>
        internal uint LastUnorderedReplicateTick;
        #endregion

        #region Internal.
        /// <summary>
        /// Pauses and unpauses rigidbodies when they do not have data to reconcile to.
        /// </summary>
        public RigidbodyPauser RigidbodyPauser => _rigidbodyPauser;
        private RigidbodyPauser _rigidbodyPauser;
        #endregion

        #region Serialized.
#if PREDICTION_V2
        /// <summary>
        /// True if this object uses prediciton methods.
        /// </summary>
        public bool EnablePrediction => _enablePrediction;
        [Tooltip("True if this object uses prediction methods.")]
        [SerializeField]
        private bool _enablePrediction;
        /// <summary>
        /// What type of component is being used for prediction? If not using rigidbodies set to other.
        /// </summary>
        [Tooltip("What type of component is being used for prediction? If not using rigidbodies set to other.")]
        [SerializeField]
        private PredictionType _predictionType = PredictionType.Other;
        /// <summary>
        /// Object containing graphics when using prediction. This should be child of the predicted root.
        /// </summary>
        [Tooltip("Object containing graphics when using prediction. This should be child of the predicted root.")]
        [SerializeField]
        private Transform _graphicalObject;
        /// <summary>
        /// True to forward replicate and reconcile states to all clients. This is ideal with games where you want all clients and server to run the same inputs. False to only use prediction on the owner, and synchronize to spectators using other means such as a NetworkTransform.
        /// </summary>
        public bool EnableStateForwarding => (_enablePrediction && _enableStateForwarding);
        [Tooltip("True to forward replicate and reconcile states to all clients. This is ideal with games where you want all clients and server to run the same inputs. False to only use prediction on the owner, and synchronize to spectators using other means such as a NetworkTransform.")]
        [SerializeField]
        private bool _enableStateForwarding = true;
        /// <summary>
        /// NetworkTransform to configure for prediction. Specifying this is optional.
        /// </summary>
        [Tooltip("NetworkTransform to configure for prediction. Specifying this is optional.")]
        [SerializeField]
        private NetworkTransform _networkTransform;
        /// <summary>
        /// How many ticks to interpolate graphics on objects owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.
        /// </summary>
        [Tooltip("How many ticks to interpolate graphics on objects owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.")]
        [Range(1, byte.MaxValue)]
        [SerializeField]
        private byte _ownerInterpolation = 1;
        /// <summary>
        /// True to enable teleport threshhold.
        /// </summary>
        [Tooltip("True to enable teleport threshhold.")]
        [SerializeField]
        private bool _enableTeleport;
        /// <summary>
        /// Distance the graphical object must move between ticks to teleport the transform properties.
        /// </summary>
        [Tooltip("Distance the graphical object must move between ticks to teleport the transform properties.")]
        [Range(0.001f, ushort.MaxValue)]
        [SerializeField]
        private float _teleportThreshold = 1f;
#endif
        #endregion

        #region Private.
        /// <summary>
        /// Graphical smoother to use when using set for owner.
        /// </summary>
        private LocalTransformTickSmoother _tickSmoother;
        /// <summary>
        /// NetworkBehaviours which use prediction.
        /// </summary>
        private List<NetworkBehaviour> _predictionBehaviours = new List<NetworkBehaviour>();
        #endregion

        private void Prediction_Update()
        {
            if (!_enablePrediction)
                return;

            _tickSmoother?.Update();
        }

        private void TimeManager_OnPreTick()
        {
            _tickSmoother?.OnPreTick();
        }
        private void TimeManager_OnPostTick()
        {
            _tickSmoother?.OnPostTick();
        }

        private void Prediction_Preinitialize(NetworkManager manager, bool asServer)
        {
            if (!_enablePrediction)
                return;

            if (!_enableStateForwarding && _networkTransform != null)
                _networkTransform.ConfigureForPrediction(_predictionType);

            ReplicateTick.Initialize(manager.TimeManager);
            InitializeSmoothers();

            if (asServer)
                return;

            if (_predictionBehaviours.Count > 0)
            {
                manager.PredictionManager.OnReconcile += PredictionManager_OnReconcile;
                manager.PredictionManager.OnReplicateReplay += PredictionManager_OnReplicateReplay;
                manager.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
                manager.TimeManager.OnPreTick += TimeManager_OnPreTick;
                manager.TimeManager.OnPostTick += TimeManager_OnPostTick;
            }
        }

        private void Prediction_Deinitialize(bool asServer)
        {
            if (!_enablePrediction)
                return;

            DeinitializeSmoothers();
            /* Only the client needs to unsubscribe from these but
             * asServer may not invoke as false if the client is suddenly
             * dropping their connection. */
            if (_predictionBehaviours.Count > 0 && NetworkManager != null)
            {
                NetworkManager.PredictionManager.OnReconcile -= PredictionManager_OnReconcile;
                NetworkManager.PredictionManager.OnReplicateReplay -= PredictionManager_OnReplicateReplay;
                NetworkManager.PredictionManager.OnPostReconcile -= PredictionManager_OnPostReconcile;
                NetworkManager.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                NetworkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }


        /// <summary>
        /// Initializes tick smoothing.
        /// </summary>
        private void InitializeSmoothers()
        {
            bool usesRb = (_predictionType == PredictionType.Rigidbody);
            bool usesRb2d = (_predictionType == PredictionType.Rigidbody2D);
            if (usesRb || usesRb2d)
            {
                _rigidbodyPauser = ResettableObjectCaches<RigidbodyPauser>.Retrieve();
                RigidbodyType rbType = (usesRb) ? RigidbodyType.Rigidbody : RigidbodyType.Rigidbody2D;
                _rigidbodyPauser.UpdateRigidbodies(transform, rbType, true);
            }

            if (_graphicalObject == null)
            {
                NetworkManagerExtensions.Log($"GraphicalObject is null on {gameObject.name}. This may be intentional, and acceptable, if you are smoothing between ticks yourself. Otherwise consider assigning the GraphicalObject field.");
            }
            else
            {
                if (_tickSmoother == null)
                    _tickSmoother = ResettableObjectCaches<LocalTransformTickSmoother>.Retrieve();
                float teleportT = (_enableTeleport) ? _teleportThreshold : MoveRatesCls.UNSET_VALUE;
                _tickSmoother.InitializeOnce(_graphicalObject, teleportT, (float)TimeManager.TickDelta, _ownerInterpolation);
            }
        }

        /// <summary>
        /// Initializes tick smoothing.
        /// </summary>
        private void DeinitializeSmoothers()
        {
            if (_tickSmoother != null)
            {
                ResettableObjectCaches<LocalTransformTickSmoother>.StoreAndDefault(ref _tickSmoother);
                ResettableObjectCaches<RigidbodyPauser>.StoreAndDefault(ref _rigidbodyPauser);
            }
        }

        private void PredictionManager_OnReconcile(uint clientReconcileTick, uint serverReconcileTick)
        {
            bool hasData = false;

            for (int i = 0; i < _predictionBehaviours.Count; i++)
            {
                if (_predictionBehaviours[i].ClientHasReconcileData)
                {
                    hasData = true;
                    _predictionBehaviours[i].Reconcile_Client_Start();
                }
                else if (_rigidbodyPauser != null)
                {
                    _rigidbodyPauser.Pause();
                }

            }
            IsObjectReconciling = hasData;
        }

        private void PredictionManager_OnPostReconcile(uint clientReconcileTick, uint serverReconcileTick)
        {
            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Reconcile_Client_End();

            /* Unpause rigidbody pauser. It's okay to do that here rather
             * than per NB, where the pausing occurs, because once here
             * the entire object is out of the replay cycle so there's
             * no reason to try and unpause per NB. */
            if (_rigidbodyPauser != null)
                _rigidbodyPauser.Unpause();
            IsObjectReconciling = false;
        }


        private void PredictionManager_OnReplicateReplay(uint clientTick, uint serverTick)
        {
            uint replayTick = (IsOwner) ? clientTick : serverTick;
            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Replicate_Replay_Start(replayTick + 1);
        }

        /// <summary>
        /// Registers a NetworkBehaviour that uses prediction with the NetworkObject.
        /// This method should only be called once throughout the entire lifetime of this object.
        /// </summary>
        internal void RegisterPredictionBehaviourOnce(NetworkBehaviour nb)
        {
            _predictionBehaviours.Add(nb);
        }

        /// <summary>
        /// Resets replicate tick and unordered replicate tick.
        /// </summary>
        internal void ResetReplicateTick()
        {
            ReplicateTick.Reset();
            LastUnorderedReplicateTick = 0;
        }

        /// <summary>
        /// Sets the last tick this NetworkBehaviour replicated with.
        /// </summary>
        internal void SetReplicateTick(uint value, bool setUnordered)
        {
            if (setUnordered)
                LastUnorderedReplicateTick = value;

            ReplicateTick.Update(NetworkManager.TimeManager, value, EstimatedTick.OldTickOption.Discard);
            if (Owner.IsValid)
                Owner.ReplicateTick.Update(NetworkManager.TimeManager, value, EstimatedTick.OldTickOption.Discard);
        }


    }
#endif
}

