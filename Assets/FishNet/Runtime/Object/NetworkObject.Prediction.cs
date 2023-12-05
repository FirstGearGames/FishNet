﻿using FishNet.Component.Prediction;
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
        /// <summary>
        /// Last tick to replicate even if out of order. This could be from tick events or even replaying inputs.
        /// </summary>
        internal uint LastUnorderedReplicateTick;
        #endregion

        #region Internal.
        /// <summary>
        /// Pauses rigidbodies for prediction.
        /// </summary>
        public RigidbodyPauser RigidbodyPauser { get; private set; }
        #endregion

        #region Private.
        #region Preset SmoothingDatas.
        [System.NonSerialized]
        private static AdaptiveInterpolationSmoothingData _accurateSmoothingData = new AdaptiveInterpolationSmoothingData()
        {
            NormalPercent = 0.625f,
            CollisionPercent = 0.0625f,
            NormalStep = 0.25f,
            CollisionStep = 1f,
        };
        [System.NonSerialized]
        private static AdaptiveInterpolationSmoothingData _mixedSmoothingData = new AdaptiveInterpolationSmoothingData()
        {
            NormalPercent = 1.25f,
            CollisionPercent = 0.125f,
            NormalStep = 0.25f,
            CollisionStep = 0.75f,
        };
        [System.NonSerialized]
        private static AdaptiveInterpolationSmoothingData _gradualSmoothingData = new AdaptiveInterpolationSmoothingData()
        {
            NormalPercent = 1.875f,
            CollisionPercent = 0.25f,
            NormalStep = 0.25f,
            CollisionStep = 0.5f,
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
        private AdaptiveInterpolationSmoother _spectatorAdaptiveInterpolationSmoother;
        /// <summary>
        /// NetworkBehaviours which use prediction.
        /// </summary>
        private List<NetworkBehaviour> _predictionBehaviours = new List<NetworkBehaviour>();
        /// <summary>
        /// Tick when CollionStayed last called. This only has value if using prediction.
        /// </summary>
        private uint _collisionStayedTick;
        /// <summary>
        /// Local client objects this object is currently colliding with.
        /// </summary>
        private HashSet<GameObject> _localClientCollidedObjects = new HashSet<GameObject>();
#if UNITY_EDITOR
        /// <summary>
        /// This is only used to preview smoother data settings.
        /// </summary>
        [SerializeField]
        private AdaptiveInterpolationSmoothingData _preconfiguredSmoothingDataPreview = _mixedSmoothingData;
#endif
        #endregion

        private void InitializeSmoothers()
        {
            bool usesRb = (_predictionType == PredictionType.Rigidbody);
            bool usesRb2d = (_predictionType == PredictionType.Rigidbody2D);
            if (usesRb || usesRb2d)
            {
                RigidbodyPauser = new RigidbodyPauser();
                RigidbodyType rbType = (usesRb) ? RigidbodyType.Rigidbody : RigidbodyType.Rigidbody2D;
                RigidbodyPauser.UpdateRigidbodies(transform, rbType, true, _graphicalObject);
            }

            if (_graphicalObject == null)
            {
                Debug.Log($"GraphicalObject is null on {this.ToString()}. This may be intentional, and acceptable, if you are smoothing between ticks yourself. Otherwise consider assigning the GraphicalObject field."); 
            }
            else
            {
                //Create SetInterpolation smoother.
                _ownerSetInterpolationSmoother = new SetInterpolationSmoother();
                float teleportThreshold = (_enableTeleport) ? _ownerTeleportThreshold : MoveRatesCls.UNSET_VALUE;
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
                _spectatorSetInterpolationSmoother = new SetInterpolationSmoother();
                _spectatorSetInterpolationSmoother.InitializeOnce(osd);

                //Create adaptive interpolation smoother if enabled.
                if (_spectatorAdaptiveInterpolation)
                {
                    _spectatorAdaptiveInterpolationSmoother = new AdaptiveInterpolationSmoother();
                    //Smoothing values.
                    AdaptiveInterpolationSmoothingData aisd = GetAdaptiveSmoothingData(_adaptiveSmoothingType);
                    //Other details.
                    aisd.GraphicalObject = _graphicalObject;
                    aisd.SmoothPosition = true;
                    aisd.SmoothRotation = true;
                    aisd.SmoothScale = true;
                    aisd.NetworkObject = this;
                    aisd.TeleportThreshold = teleportThreshold;
                    _spectatorAdaptiveInterpolationSmoother.Initialize(aisd);
                }
            }
        }

        private void Prediction_Update()
        {
            if (!_enablePrediction)
                return;
            if (_graphicalObject == null)
                return;

            _ownerSetInterpolationSmoother.Update();
            if (IsHostStarted)
                _spectatorSetInterpolationSmoother.Update();
            else
                _spectatorAdaptiveInterpolationSmoother?.Update();
        }

        private void TimeManager_OnPreTick()
        {
            if (_graphicalObject == null)
                return;

            //Do not need to check use prediction because this method only fires if prediction is on for this object.
            _ownerSetInterpolationSmoother.OnPreTick();
            if (IsHostStarted)
                _spectatorSetInterpolationSmoother.OnPreTick();
            else
                _spectatorAdaptiveInterpolationSmoother?.OnPreTick();
        }
        private void TimeManager_OnPostTick()
        {
            if (_graphicalObject == null)
                return;

            //Do not need to check use prediction because this method only fires if prediction is on for this object.
            _ownerSetInterpolationSmoother.OnPostTick();
            if (IsHostStarted)
                _spectatorSetInterpolationSmoother.OnPostTick();
            else
                _spectatorAdaptiveInterpolationSmoother?.OnPostTick();

            TrySetCollisionExited();
        }

        private void Prediction_Preinitialize(NetworkManager manager, bool asServer)
        {
            if (!_enablePrediction)
                return;

            InitializeSmoothers();

            if (asServer)
                return;

            if (_predictionBehaviours.Count > 0)
            {
                manager.PredictionManager.OnPreReconcile += PredictionManager_OnPreReconcile;
                manager.PredictionManager.OnReplicateReplay += PredictionManager_OnReplicateReplay;
                manager.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
                if (_spectatorAdaptiveInterpolationSmoother != null)
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
                if (_spectatorAdaptiveInterpolationSmoother != null)
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
            _spectatorAdaptiveInterpolationSmoother?.OnPostReconcile(clientReconcileTick, serverReconcileTick);

            for (int i = 0; i < _predictionBehaviours.Count; i++)
                _predictionBehaviours[i].Reconcile_Client_End();

            /* Unpause rigidbody pauser. It's okay to do that here rather
             * than per NB, where the pausing occurs, because once here
             * the entire object is out of the replay cycle so there's
             * no reason to try and unpause per NB. */
            RigidbodyPauser?.Unpause();
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
            if (!IsHostStarted)
                _spectatorAdaptiveInterpolationSmoother?.OnPreReplicateReplay(clientTick, serverTick);
        }

        private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
        {
            /* Adaptive smoother uses localTick (clientTick) to track graphical datas.
            * There's no need to use serverTick since the only purpose of adaptiveSmoother
            * is to smooth graphic changes, not update the transform itself. */
            if (!IsHostStarted)
                _spectatorAdaptiveInterpolationSmoother?.OnPostReplicateReplay(clientTick, serverTick);
        }

        /// <summary>
        /// Returns if this object is colliding with any local client objects.
        /// </summary>
        /// <returns></returns>
        internal bool CollidingWithLocalClient()
        {
            /* If it's been more than 1 tick since collision stayed
             * then do not consider as collided. */
            return (TimeManager.LocalTick - _collisionStayedTick) <= 1;
        }

        /// <summary>
        /// Called when colliding with another object.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (!IsClientInitialized)
                return;
            if (_predictionType != PredictionType.Rigidbody)
                return;

            GameObject go = collision.gameObject;
            if (CollisionEnteredLocalClientObject(go))
                CollisionEntered(go);
        }

        /// <summary>
        /// Called when collision has entered a local clients object.
        /// </summary>
        private void CollisionEntered(GameObject go)
        {
            if (_graphicalObject == null)
                return;

            _collisionStayedTick = TimeManager.LocalTick;
            _localClientCollidedObjects.Add(go);
        }

        /// <summary>
        /// Called when colliding with another object.
        /// </summary>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_graphicalObject == null)
                return;
            if (!IsClientInitialized)
                return;
            if (_predictionType != PredictionType.Rigidbody2D)
                return;

            GameObject go = collision.gameObject;
            if (CollisionEnteredLocalClientObject(go))
                CollisionEntered(go);
        }


        /// <summary>
        /// Called when staying in collision with another object.
        /// </summary>
        private void OnCollisionStay(Collision collision)
        {
            if (!IsClientInitialized)
                return;
            if (_predictionType != PredictionType.Rigidbody)
                return;

            if (_localClientCollidedObjects.Contains(collision.gameObject))
                _collisionStayedTick = TimeManager.LocalTick;
        }
        /// <summary>
        /// Called when staying in collision with another object.
        /// </summary>
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!IsClientInitialized)
                return;
            if (_predictionType != PredictionType.Rigidbody2D)
                return;

            if (_localClientCollidedObjects.Contains(collision.gameObject))
                _collisionStayedTick = TimeManager.LocalTick;
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
        /// Called when collision has exited a local clients object.
        /// </summary>
        private void TrySetCollisionExited()
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
            TimeManager tm = TimeManager;
            if (tm == null || (_collisionStayedTick != 0 && (tm.LocalTick != _collisionStayedTick)))
            {
                _localClientCollidedObjects.Clear();
                _collisionStayedTick = 0;
            }
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
            Owner.ReplicateTick.Update(NetworkManager.TimeManager, value, EstimatedTick.OldTickOption.Discard);
        }

        /// <summary>
        /// Returns which smoothing data to use.
        /// </summary>
        private AdaptiveInterpolationSmoothingData GetAdaptiveSmoothingData(AdaptiveSmoothingType ast)
        {
            if (_adaptiveSmoothingType == AdaptiveSmoothingType.Custom)
                return _customSmoothingData;
            else if (_adaptiveSmoothingType == AdaptiveSmoothingType.Accuracy)
                return _accurateSmoothingData;
            else if (_adaptiveSmoothingType == AdaptiveSmoothingType.Mixed)
                return _mixedSmoothingData;
            else if (_adaptiveSmoothingType == AdaptiveSmoothingType.Gradual)
                return _gradualSmoothingData;

            //Fall through.
            NetworkManager.LogError($"AdaptiveSmoothingType {ast} is unhandled.");
            return _mixedSmoothingData;
        }

#if UNITY_EDITOR
        private void Prediction_OnValidate()
        {
            _preconfiguredSmoothingDataPreview = GetAdaptiveSmoothingData(_adaptiveSmoothingType);
        }
#endif
    }
#endif
}

