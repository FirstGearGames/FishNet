using FishNet.Component.Prediction;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
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
        /// <summary>
        /// Last tick this object replicated.
        /// </summary>
        internal EstimatedTick ReplicateTick;
        #endregion

        #region Internal.
        /// <summary>
        /// Pauses rigidbodies for prediction.
        /// </summary>
        internal RigidbodyPauser RigidbodyPauser;
        #endregion

        #region Private.
        #region Preset SmoothingDatas.
        private static AdaptiveInterpolationSmoothingData _accurateSmoothingData = new AdaptiveInterpolationSmoothingData()
        {
            InterpolationPercent = 0.5f,
            CollisionInterpolationPercent = 0.05f,
            InterpolationDecreaseStep = 1,
            InterpolationIncreaseStep = 2,
        };
        private static AdaptiveInterpolationSmoothingData _mixedSmoothingData = new AdaptiveInterpolationSmoothingData()
        {
            InterpolationPercent = 1f,
            CollisionInterpolationPercent = 0.1f,
            InterpolationDecreaseStep = 1,
            InterpolationIncreaseStep = 3,
        };
        private static AdaptiveInterpolationSmoothingData _gradualSmoothingData = new AdaptiveInterpolationSmoothingData()
        {
            InterpolationPercent = 1.5f,
            CollisionInterpolationPercent = 0.2f,
            InterpolationDecreaseStep = 1,
            InterpolationIncreaseStep = 5,
        };
        #endregion
        /// <summary>
        /// Graphical smoother to use when using set for owner.
        /// </summary>
        private SetInterpolationSmoother _ownerSetInterpolationSmoother;
        /// <summary>
        /// Graphical smoother to use when using set for spectators.
        /// </summary>
        private SetInterpolationSmoother _spectatorSetInterpolationSmoother;
        /// <summary>
        /// Graphical smoother to use when using adaptive.
        /// </summary>
        private AdaptiveInterpolationSmoother _adaptiveInterpolationSmoother;
        /// <summary>
        /// NetworkBehaviours which use prediction.
        /// </summary>
        private List<NetworkBehaviour> _predictionBehaviours = new List<NetworkBehaviour>();
        #endregion

        private void Prediction_Awake()
        {
            if (!_enablePrediction)
                return;


            bool usesRb = (_predictionType == PredictionType.Rigidbody);
            bool usesRb2d = (_predictionType == PredictionType.Rigidbody2D);
            if (usesRb || usesRb2d)
            {
                RigidbodyPauser = new RigidbodyPauser();
                RigidbodyType rbType = (usesRb) ? RigidbodyType.Rigidbody : RigidbodyType.Rigidbody2D;
                RigidbodyPauser.UpdateRigidbodies(transform, rbType, true, _graphicalObject);
            }

            //Create SetInterpolation smoother.
            _ownerSetInterpolationSmoother = new SetInterpolationSmoother();
            float teleportThreshold = (_enableTeleport) ? _ownerTeleportThreshold : MoveRates.UNSET_VALUE;
            SetInterpolationSmootherData osd = new SetInterpolationSmootherData()
            {
                GraphicalObject = _graphicalObject,
                Interpolation = _ownerInterpolation,
                SmoothPosition = true,
                SmoothRotation = true,
                SmoothScale = true,
                NetworkObject = this,
                TeleportThreshold = teleportThreshold,
            };
            _ownerSetInterpolationSmoother.InitializeOnce(osd);

            //Spectator.
            //_spectatorSetInterpolationSmoother = new SetInterpolationSmoother();
            //_spectatorSetInterpolationSmoother.InitializeOnce(osd);

            //Create adaptive interpolation smoother if enabled.
            if (_spectatorAdaptiveInterpolation)
            {
                _adaptiveInterpolationSmoother = new AdaptiveInterpolationSmoother();
                //Smoothing values.
                AdaptiveInterpolationSmoothingData aisd;
                if (_adaptiveSmoothingType == AdaptiveSmoothingType.Custom)
                    aisd = _customSmoothingData;
                else
                    aisd = _preconfiguredSmoothingDataPreview;

                //Other details.
                aisd.GraphicalObject = _graphicalObject;
                aisd.SmoothPosition = true;
                aisd.SmoothRotation = true;
                aisd.SmoothScale = true;
                aisd.NetworkObject = this;
                aisd.TeleportThreshold = teleportThreshold;
                _adaptiveInterpolationSmoother.Initialize(aisd);
            }
        }

        private void Prediction_Update()
        {
            if (!_enablePrediction)
                return;

            _ownerSetInterpolationSmoother.Update();
            //  _spectatorSetInterpolationSmoother.Update();
            _adaptiveInterpolationSmoother?.Update();
        }

        private void TimeManager_OnPreTick()
        {
            //Do not need to check use prediction because this method only fires if prediction is on for this object.
            _ownerSetInterpolationSmoother.OnPreTick();
            //   _spectatorSetInterpolationSmoother.OnPreTick();
            _adaptiveInterpolationSmoother?.OnPreTick();
        }
        private void TimeManager_OnPostTick()
        {
            //Do not need to check use prediction because this method only fires if prediction is on for this object.
            _ownerSetInterpolationSmoother.OnPostTick();
            // _spectatorSetInterpolationSmoother.OnPostTick();
            _adaptiveInterpolationSmoother?.OnPostTick();
        }

        private void Prediction_Preinitialize(NetworkManager manager, bool asServer)
        {
            if (!_enablePrediction)
                return;
            if (asServer)
                return;

            if (_predictionBehaviours.Count > 0)
            {
                manager.PredictionManager.OnPreReconcile += PredictionManager_OnPreReconcile;
                manager.PredictionManager.OnReplicateReplay += PredictionManager_OnReplicateReplay;
                manager.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
                if (_adaptiveInterpolationSmoother != null)
                {
                    manager.PredictionManager.OnPreReplicateReplay += PredictionManager_OnPreReplicateReplay;
                    manager.PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
                }
                manager.TimeManager.OnPreTick += TimeManager_OnPreTick;
                manager.TimeManager.OnPostTick += TimeManager_OnPostTick;
            }
        }

        private void Prediction_Deinitialize(bool asServer)
        {
            if (!_enablePrediction)
                return;

            /* Only the client needs to unsubscribe from these but
             * asServer may not invoke as false if the client is suddenly
             * dropping their connection. */
            if (_predictionBehaviours.Count > 0 && NetworkManager != null)
            {
                NetworkManager.PredictionManager.OnPreReconcile -= PredictionManager_OnPreReconcile;
                NetworkManager.PredictionManager.OnReplicateReplay -= PredictionManager_OnReplicateReplay;
                NetworkManager.PredictionManager.OnPostReconcile -= PredictionManager_OnPostReconcile;
                if (_adaptiveInterpolationSmoother != null)
                {
                    NetworkManager.PredictionManager.OnPreReplicateReplay -= PredictionManager_OnPreReplicateReplay;
                    NetworkManager.PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;
                }
                NetworkManager.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                NetworkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        private void PredictionManager_OnPreReconcile(uint clientReconcileTick, uint serverReconcileTick)
        {
            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Reconcile_Client_Start();
        }

        private void PredictionManager_OnPostReconcile(uint clientReconcileTick, uint serverReconcileTick)
        {
            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Reconcile_Client_End();

            /* Unpause rigidbody pauser. It's okay to do that here rather
             * than per NB, where the pausing occurs, because once here
             * the entire object is out of the replay cycle so there's
             * no reason to try and unpause per NB. */
            RigidbodyPauser?.Unpause();
        }


        private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
        {
            /* Adaptive smoother uses localTick (clientTick) to track graphical datas.
            * There's no need to use serverTick since the only purpose of adaptiveSmoother
            * is to smooth graphic changes, not update the transform itself. */
            _adaptiveInterpolationSmoother?.OnPostReplay(clientTick);
        }

        private void PredictionManager_OnReplicateReplay(uint clientTick, uint serverTick)
        {
            uint replayTick = (IsOwner) ? clientTick : serverTick;
            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Replicate_Replay_Start(replayTick);
        }

        private void PredictionManager_OnPreReplicateReplay(uint clientTick, uint serverTick)
        {
            /* Adaptive smoother uses localTick (clientTick) to track graphical datas.
             * There's no need to use serverTick since the only purpose of adaptiveSmoother
             * is to smooth graphic changes, not update the transform itself. */
            _adaptiveInterpolationSmoother?.OnPreReplay(clientTick);
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
        /// Sets the last tick this NetworkBehaviour replicated with.
        /// </summary>
        internal void SetReplicateTick(uint value)
        {
            ReplicateTick.Update(NetworkManager.TimeManager, value, EstimatedTick.OldTickOption.Discard);
            Owner.ReplicateTick.Update(NetworkManager.TimeManager, value, EstimatedTick.OldTickOption.Discard);
        }

#if UNITY_EDITOR
        private void Prediction_OnValidate()
        {
            if (Application.isPlaying)
            {
                //   InitializeSmoother(true);
            }
            else
            {
                if (_adaptiveSmoothingType == AdaptiveSmoothingType.Accuracy)
                    _preconfiguredSmoothingDataPreview = _accurateSmoothingData;
                else if (_adaptiveSmoothingType == AdaptiveSmoothingType.Mixed)
                    _preconfiguredSmoothingDataPreview = _mixedSmoothingData;
                else if (_adaptiveSmoothingType == AdaptiveSmoothingType.Gradual)
                    _preconfiguredSmoothingDataPreview = _gradualSmoothingData;
            }
        }
#endif
    }
#endif
}

