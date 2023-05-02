using FishNet.Managing;
using FishNet.Object.Prediction;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object
{
#if PREDICTION_V2
    public partial class NetworkObject : MonoBehaviour
    {

        #region Public.
        /// <summary>
        /// Last tick this object replicated.
        /// </summary>
        internal uint LastReplicateTick;
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
        /// Graphical smoother to use when using set.
        /// </summary>
        private SetInterpolationSmoother _setSmoother;
        /// <summary>
        /// Graphical smoother to use when using adaptive.
        /// </summary>
        private AdaptiveInterpolationSmoother _adaptiveSmoother;
        #endregion

        /// <summary>
        /// NetworkBehaviours which use prediction.
        /// </summary>
        private List<NetworkBehaviour> _predictionBehaviours = new List<NetworkBehaviour>();

        private void Prediction_Awake()
        {
            if (!_usePrediction)
                return;

            //Create SetInterpolation smoother.
            _setSmoother = new SetInterpolationSmoother();
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
            _setSmoother.InitializeOnce(osd);
            //Create adaptive interpolation smoother if enabled.
            if (_spectatorAdaptiveInterpolation)
            {
                _adaptiveSmoother = new AdaptiveInterpolationSmoother();
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
                _adaptiveSmoother.Initialize(aisd);
            }
        }

        private void Prediction_Update()
        {
            if (!_usePrediction)
                return;

            _setSmoother.Update();
            _adaptiveSmoother?.Update();
        }

        private void TimeManager_OnPreTick()
        {
            //Do not need to check use prediction because this method only fires if prediction is on for this object.
            _setSmoother.OnPreTick();
            _adaptiveSmoother?.OnPreTick();
        }
        private void TimeManager_OnPostTick()
        {
            //Do not need to check use prediction because this method only fires if prediction is on for this object.
            _setSmoother.OnPostTick();
            _adaptiveSmoother?.OnPostTick();
        }

        private void Prediction_Preinitialize(NetworkManager manager, bool asServer)
        {
            if (!_usePrediction)
                return;
            if (asServer)
                return;

            if (_predictionBehaviours.Count > 0)
            {
                manager.PredictionManager.OnPreReconcile += PredictionManager_OnPreReconcile;
                manager.PredictionManager.OnReplicateReplay += PredictionManager_OnReplicateReplay;
                if (_adaptiveSmoother != null)
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
            if (!_usePrediction)
                return;
            if (asServer)
                return;

            if (_predictionBehaviours.Count > 0 && NetworkManager != null)
            {
                NetworkManager.PredictionManager.OnPreReconcile -= PredictionManager_OnPreReconcile;
                NetworkManager.PredictionManager.OnReplicateReplay -= PredictionManager_OnReplicateReplay;
                if (_adaptiveSmoother != null)
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


        private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
        {
            _adaptiveSmoother.OnPostReplay(serverTick);
        }

        private void PredictionManager_OnReplicateReplay(uint clientTick, uint serverTick)
        {
            uint replayTick = (IsOwner) ? clientTick : serverTick;
            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Replicate_Replay_Start(replayTick);
        }

        private void PredictionManager_OnPreReplicateReplay(uint clientTick, uint serverTick)
        {
            _adaptiveSmoother.OnPreReplay(serverTick);
        }

        /// <summary>
        /// Registers a NetworkBehaviour that uses prediction with the NetworkObject.
        /// This method should only be called once throughout the entire lifetime of this object.
        /// </summary>
        internal void RegisterPredictionBehaviourOnce(NetworkBehaviour nb)
        {
            _predictionBehaviours.Add(nb);
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

