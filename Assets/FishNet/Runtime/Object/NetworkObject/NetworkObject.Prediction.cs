#define NEW_RECONCILE_TEST
using System;
using FishNet.Component.Prediction;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Server;
using Unity.Profiling;
using UnityEngine;

#pragma warning disable CS0618 // Type or member is obsolete

// ReSharper disable once CheckNamespace
namespace FishNet.Object
{
    public partial class NetworkObject
    {
        #region Types.
        /// <summary>
        /// Type of prediction movement being used.
        /// </summary>
        [Serializable]
        internal enum PredictionType : byte
        {
            Other = 0,
            Rigidbody = 1,
            Rigidbody2D = 2
        }

        /// <summary>
        /// How local reconciles are applied when using level of detail.
        /// </summary>
        internal enum LocalReconcileLODCalculationType
        {
            /// <summary>
            /// Local reconciles will only be applied on very near objects.
            /// </summary>
            /// <remarks>This will cause only very near objects to reconcile using local reconcile data.</remarks>
            CloseObjectsOnly,
            /// <summary>
            /// Local reconciles will be applied on any object which the tick fits the Level of detail window.
            /// </summary>
            /// <remarks>Using this option will result in more objects reconciling when the server does not send reconcile data due to level of detail, but the client thinks it should have it.</remarks>
            ObjectsWithinLevelOfDetail,
        }

        /// <summary>
        /// How to correct, or reset a rigidbody transform after a reconcile when the reconcile state is local, and the rigidbody has near nil differences from when the reconcile started.
        /// </summary>
        /// <remarks>Due to physics not being deterministic a reconcile can cause a rigidbody to finish with different results than what it started it, even if the rigidbody did not experience any difference in forces. These options allow FishNet to reset the rigidbody to as it were before the reconcile if the differences are minor enough. By resetting values de-synchronization and subtly observed shaking can be prevented or significantly reduced.</remarks>
        [Serializable]
        internal enum RigidbodyLocalReconcileCorrectionType : byte
        {
            /// <summary>
            /// Do not make corrections.
            /// </summary>
            Disabled = 0,
            /// <summary>
            /// Only reset the transform.
            /// </summary>
            TransformOnly = 1,
            /// <summary>
            /// Reset the transform and the rigidbody velocities and sleep state.
            /// </summary>
            /// <remarks>Rigidbodies for the reset are read from the object's <see cref = "RigidbodyPauser"/>.</remarks>
            TransformAndVelocities = 2
        }
        #endregion

        #region Public.
        /// <summary>
        /// True if a reconcile is occuring on any NetworkBehaviour that is on or nested of this NetworkObject. Runtime NetworkBehaviours are not included, such as if you child a NetworkObject to another at runtime.
        /// </summary>
        public bool IsObjectReconciling { get; internal set; }
        /// <summary>
        /// Graphical smoother to use when using set for owner.
        /// </summary>
        [Obsolete("This field will be removed in v5. Instead reference NetworkTickSmoother on each graphical object used.")]
        public TransformTickSmoother PredictionSmoother { get; private set; }
        #endregion

        #region Internal.
        /// <summary>
        /// Pauses and unpauses rigidbodies when they do not have data to reconcile to.
        /// </summary>
        public RigidbodyPauser RigidbodyPauser => _rigidbodyPauser;
        private RigidbodyPauser _rigidbodyPauser;
        /// <summary>
        /// True if PredictionType is set to a rigidbody value.
        /// </summary>
        internal bool IsRigidbodyPredictionType;
        #endregion

        #region Serialized.
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
        /// Object state corrections to apply after replaying from a local state when non-deterministic physics have possibly provided a different result under the same conditions.
        /// </summary>
        [Tooltip("Object state corrections to apply after replaying from a local state when non-deterministic physics have possibly provided a different result under the same conditions.")]
        [SerializeField]
        private RigidbodyLocalReconcileCorrectionType _localReconcileCorrectionType = RigidbodyLocalReconcileCorrectionType.TransformAndVelocities;
        /// <summary>
        /// Object containing graphics when using prediction. This should be child of the predicted root.
        /// </summary>
        [Tooltip("Object containing graphics when using prediction. This should be child of the predicted root.")]
        [SerializeField]
        private Transform _graphicalObject;

        /// <summary>
        /// Gets the current graphical object for prediction.
        /// </summary>
        /// <returns></returns>
        public Transform GetGraphicalObject() => _graphicalObject;

        /// <summary>
        /// Sets a new graphical object for prediction.
        /// </summary>
        /// <param name = "t"></param>
        public void SetGraphicalObject(Transform t)
        {
            _graphicalObject = t;
            InitializeTickSmoother();
        }

        /// <summary>
        /// True to detach and re-attach the graphical object at runtime when the client initializes/deinitializes the item.
        /// This can resolve camera jitter or be helpful objects child of the graphical which do not handle reconiliation well, such as certain animation rigs.
        /// Transform is detached after OnStartClient, and reattached before OnStopClient.
        /// </summary>
        [Tooltip("True to detach and re-attach the graphical object at runtime when the client initializes/deinitializes the item. This can resolve camera jitter or be helpful objects child of the graphical which do not handle reconiliation well, such as certain animation rigs. Transform is detached after OnStartClient, and reattached before OnStopClient.")]
        [SerializeField]
        private bool _detachGraphicalObject;
        /// <summary>
        /// True to forward replicate and reconcile states to all clients. This is ideal with games where you want all clients and server to run the same inputs. False to only use prediction on the owner, and synchronize to spectators using other means such as a NetworkTransform.
        /// </summary>
        public bool EnableStateForwarding => _enablePrediction && _enableStateForwarding;
        [Tooltip("True to forward replicate and reconcile states to all clients. This is ideal with games where you want all clients and server to run the same inputs. False to only use prediction on the owner, and synchronize to spectators using other means such as a NetworkTransform.")]
        [SerializeField]
        private bool _enableStateForwarding = true;
        /// <summary>
        /// NetworkTransform to configure for prediction. Specifying this is optional.
        /// </summary>
        [Tooltip("NetworkTransform to configure for prediction. Specifying this is optional.")]
        [SerializeField]
        private NetworkTransform _networkTransform;
        internal NetworkTransform PredictionNetworkTransform => _networkTransform;
        /// <summary>
        /// How many ticks to interpolate graphics on objects owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.
        /// </summary>
        [Tooltip("How many ticks to interpolate graphics on objects owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.")]
        [Range(1, byte.MaxValue)]
        [SerializeField]
        private byte _ownerInterpolation = 1;
        /// <summary>
        /// Properties of the graphicalObject to smooth when owned.
        /// </summary>
        [SerializeField]
        private TransformPropertiesFlag _ownerSmoothedProperties = (TransformPropertiesFlag)~(-1 << 8);
        /// <summary>
        /// Interpolation amount of adaptive interpolation to use on non-owned objects. Higher levels result in more interpolation. When off spectatorInterpolation is used; when on interpolation based on strength and local client latency is used.
        /// </summary>
        [Tooltip("Interpolation amount of adaptive interpolation to use on non-owned objects. Higher levels result in more interpolation. When off spectatorInterpolation is used; when on interpolation based on strength and local client latency is used.")]
        [SerializeField]
        private AdaptiveInterpolationType _adaptiveInterpolation = AdaptiveInterpolationType.Low;
        /// <summary>
        /// Properties of the graphicalObject to smooth when the object is spectated.
        /// </summary>
        [SerializeField]
        private TransformPropertiesFlag _spectatorSmoothedProperties = (TransformPropertiesFlag)~(-1 << 8);
        /// <summary>
        /// How many ticks to interpolate graphics on objects when not owned by the client.
        /// </summary>
        [Tooltip("How many ticks to interpolate graphics on objects when not owned by the client.")]
        [Range(1, byte.MaxValue)]
        [SerializeField]
        private byte _spectatorInterpolation = 2;
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
        #endregion

        #region Private.
        /// <summary>
        /// True if prediction behaviours have already been registered.
        /// </summary>
        private bool _predictionBehavioursRegistered;
        /// <summary>
        /// NetworkBehaviours which use prediction.
        /// </summary>
        private HashSet<NetworkBehaviour> _predictionBehaviours;
        /// <summary>
        /// Snapshot of the rigidbodies' transform and simulation state captured on the most recent remote reconcile.
        /// A local reconcile with an insignificant change resets to it. Null unless the object uses 3D rigidbody prediction.
        /// </summary>
        private List<RigidbodyPauser.RigidbodyData> _remoteRigidbodySnapshot;
        /// <summary>
        /// 2D equivalent of <see cref="_remoteRigidbodySnapshot"/>. Null unless the object uses 2D rigidbody prediction.
        /// </summary>
        private List<RigidbodyPauser.Rigidbody2DData> _remoteRigidbody2dSnapshot;
        #endregion

        #region Private Profiler Markers
        private static readonly ProfilerMarker _pm_OnPreTick = new("NetworkObject.TimeManager_OnPreTick()");
        private static readonly ProfilerMarker _pm_OnPostReplicateReplay = new("NetworkObject.PredictionManager_OnPostReplicateReplay(uint, uint)");
        private static readonly ProfilerMarker _pm_OnPostTick = new("NetworkObject.TimeManager_OnPostTick()");
        private static readonly ProfilerMarker _pm_OnPreReconcile = new("NetworkObject.PredictionManager_OnPreReconcile(uint, uint)");
        private static readonly ProfilerMarker _pm_OnReconcile = new("NetworkObject.PredictionManager_OnReconcile(uint, uint)");
        private static readonly ProfilerMarker _pm_OnPostReconcile = new("NetworkObject.PredictionManager_OnPostReconcile(uint, uint)");
        private static readonly ProfilerMarker _pm_OnReplicateReplay = new("NetworkObject.PredictionManager_OnReplicateReplay(uint, uint)");
        #endregion

        private void TimeManager_OnUpdate_Prediction()
        {
            if (!_enablePrediction)
                return;

            if (PredictionSmoother != null)
                PredictionSmoother.OnUpdate();
        }

        private void InitializeEarly_Prediction(NetworkManager manager, bool asServer)
        {
            if (!_enablePrediction)
                return;

            if (!_enableStateForwarding && _networkTransform != null)
                _networkTransform.ConfigureForPrediction(_predictionType);

            IsRigidbodyPredictionType = _predictionType == PredictionType.Rigidbody || _predictionType == PredictionType.Rigidbody2D;

            if (!_predictionBehavioursRegistered)
            {
                foreach (NetworkBehaviour behaviour in NetworkBehaviours)
                    TryRegisterPredictionBehaviour(behaviour);

                _predictionBehavioursRegistered = true;
            }

            if (!asServer)
                InitializeSmoothers();

            ChangePredictionSubscriptions(true, manager, asServer);
        }

        private void Deinitialize_Prediction(bool asServer)
        {
            if (!_enablePrediction)
                return;

            DeinitializeSmoothers();
            ChangePredictionSubscriptions(subscribe: false, NetworkManager, asServer);
        }

        /// <summary>
        /// Changes subscriptions to use callbacks for prediction.
        /// </summary>
        private void ChangePredictionSubscriptions(bool subscribe, NetworkManager manager, bool asServer)
        {
            /* Only the client needs to unsubscribe from these but
             * asServer may not invoke as false if the client is suddenly
             * dropping their connection. */
            if (asServer && subscribe)
                return;

            if (manager == null)
                return;

            if (_predictionBehaviours.Count == 0)
                return;

            if (subscribe)
            {
                manager.PredictionManager.OnPreReconcile += PredictionManager_OnPreReconcile;
                manager.PredictionManager.OnReconcile += PredictionManager_OnReconcile;
                manager.PredictionManager.OnReplicateReplay += PredictionManager_OnReplicateReplay;
                manager.PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
                manager.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
                manager.TimeManager.OnPreTick += TimeManager_OnPreTick;
                manager.TimeManager.OnPostTick += TimeManager_OnPostTick;
            }
            else
            {
                manager.PredictionManager.OnPreReconcile -= PredictionManager_OnPreReconcile;
                manager.PredictionManager.OnReconcile -= PredictionManager_OnReconcile;
                manager.PredictionManager.OnReplicateReplay -= PredictionManager_OnReplicateReplay;
                manager.PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;
                manager.PredictionManager.OnPostReconcile -= PredictionManager_OnPostReconcile;
                manager.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                manager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        /// <summary>
        /// Initializes tick smoothing.
        /// </summary>
        private void InitializeSmoothers()
        {
            if (IsRigidbodyPredictionType)
            {
                _rigidbodyPauser = ResettableObjectCaches<RigidbodyPauser>.Retrieve();
                RigidbodyType rbType = _predictionType == PredictionType.Rigidbody ? RigidbodyType.Rigidbody : RigidbodyType.Rigidbody2D;
                _rigidbodyPauser.UpdateRigidbodies(transform, rbType, getInChildren: true);

                //Rent the remote snapshot list matching the rigidbody type.
                if (_predictionType == PredictionType.Rigidbody)
                    _remoteRigidbodySnapshot = CollectionCaches<RigidbodyPauser.RigidbodyData>.RetrieveList();
                else
                    _remoteRigidbody2dSnapshot = CollectionCaches<RigidbodyPauser.Rigidbody2DData>.RetrieveList();
            }

            if (_graphicalObject == null)
            {
                //Removed per community request; the document has shown to no longer use this field for a hefty duration.
                //NetworkManager.Log($"GraphicalObject is null on {gameObject.name}. This may be intentional, and acceptable, if you are smoothing between ticks yourself. Otherwise consider assigning the GraphicalObject field.");
            }
            else
            {
                if (PredictionSmoother == null)
                    PredictionSmoother = ResettableObjectCaches<TransformTickSmoother>.Retrieve();

                InitializeTickSmoother();
            }
        }

        /// <summary>
        /// Initializes the tick smoother.
        /// </summary>
        private void InitializeTickSmoother()
        {
            if (PredictionSmoother == null)
                return;

            float teleportT = _enableTeleport ? _teleportThreshold : MoveRates.UNSET_VALUE;
            PredictionSmoother.InitializeNetworked(this, _graphicalObject, _detachGraphicalObject, teleportT, (float)TimeManager.TickDelta, _ownerInterpolation, _ownerSmoothedProperties, _spectatorInterpolation, _spectatorSmoothedProperties, _adaptiveInterpolation);
        }

        /// <summary>
        /// Initializes tick smoothing.
        /// </summary>
        private void DeinitializeSmoothers()
        {
            if (PredictionSmoother != null)
            {
                PredictionSmoother.Deinitialize();
                ResettableObjectCaches<TransformTickSmoother>.Store(PredictionSmoother);
                PredictionSmoother = null;
                ResettableObjectCaches<RigidbodyPauser>.StoreAndDefault(ref _rigidbodyPauser);

                //The snapshot lists live and die with the pauser; return them here only.
                if (_remoteRigidbodySnapshot != null)
                {
                    CollectionCaches<RigidbodyPauser.RigidbodyData>.Store(_remoteRigidbodySnapshot);
                    _remoteRigidbodySnapshot = null;
                }
                if (_remoteRigidbody2dSnapshot != null)
                {
                    CollectionCaches<RigidbodyPauser.Rigidbody2DData>.Store(_remoteRigidbody2dSnapshot);
                    _remoteRigidbody2dSnapshot = null;
                }
            }
        }

        private void InvokeStartCallbacks_Prediction(bool asServer)
        {
            if (!asServer)
            {
                TimeManager.OnUpdate += TimeManager_Update;

                if (PredictionSmoother != null)
                    PredictionSmoother.OnStartClient();
            }
        }

        private void InvokeStopCallbacks_Prediction(bool asServer)
        {
            if (!asServer)
                return;

            if (TimeManager != null)
                TimeManager.OnUpdate -= TimeManager_Update;

            if (PredictionSmoother != null)
                PredictionSmoother.OnStopClient();
        }

        private void TimeManager_OnPreTick()
        {
            using (_pm_OnPreTick.Auto())
            {
                if (PredictionSmoother != null)
                    PredictionSmoother.OnPreTick();
            }
        }

        private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
        {
            using (_pm_OnPostReplicateReplay.Auto())
            {
                if (PredictionSmoother != null)
                    PredictionSmoother.OnPostReplicateReplay(clientTick);
            }
        }

        private void TimeManager_OnPostTick()
        {
            using (_pm_OnPostTick.Auto())
            {
                if (PredictionSmoother != null)
                    PredictionSmoother.OnPostTick(NetworkManager.TimeManager.LocalTick);
            }
        }

        private void PredictionManager_OnPreReconcile(uint clientTick, uint serverTick)
        {
            using (_pm_OnPreReconcile.Auto())
            {
                if (PredictionSmoother != null)
                    PredictionSmoother.OnPreReconcile();
            }
        }

        private void PredictionManager_OnReconcile(uint clientReconcileTick, uint serverReconcileTick)
        {
            using (_pm_OnReconcile.Auto())
            {
                if (!IsClientInitialized)
                    return;

                /* Tell all prediction behaviours to set/validate their
                 * reconcile data now. This will use reconciles from the server
                 * whenever possible, and local reconciles if a server reconcile
                 * is not available. */
                foreach (NetworkBehaviour networkBehaviour in _predictionBehaviours)
                    networkBehaviour.Reconcile_Client_Start();

                /* If still not reconciling then pause rigidbody.
                 * This shouldn't happen unless the user is not calling
                 * reconcile at all. */
                if (!IsObjectReconciling)
                {
                    if (_rigidbodyPauser != null)
                        _rigidbodyPauser.Pause();
                }
            }
        }

        private void PredictionManager_OnPostReconcile(uint clientReconcileTick, uint serverReconcileTick)
        {
            using (_pm_OnPostReconcile.Auto())
            {
                if (!IsClientInitialized)
                    return;

                /* Rigidbody correction. On a remote reconcile snapshot the server-authoritative rigidbody
                 * state; on a local reconcile, when the object barely diverged from that snapshot, snap it
                 * back to the snapshot. The snapshot lists are non-null only when using rigidbody prediction. */
                if (_localReconcileCorrectionType != RigidbodyLocalReconcileCorrectionType.Disabled && _rigidbodyPauser != null)
                {
                    bool canTakeSnapshot = LIsReconciling(remote: true);
                    if (!canTakeSnapshot)
                    {
                        if (_remoteRigidbodySnapshot != null && _remoteRigidbodySnapshot.Count == 0 || _remoteRigidbody2dSnapshot != null && _remoteRigidbody2dSnapshot.Count == 0)
                            canTakeSnapshot = true;
                    }

                    if (canTakeSnapshot)
                    {
                        if (_remoteRigidbodySnapshot != null)
                            _rigidbodyPauser.GetSnapshot(_remoteRigidbodySnapshot);
                        else if (_remoteRigidbody2dSnapshot != null)
                            _rigidbodyPauser.GetSnapshot(_remoteRigidbody2dSnapshot);
                    }
                    //A local reconcile: snap back to the last remote snapshot when the change is minor.
                    else if (LIsReconciling(remote: false))
                    {
                        if (_remoteRigidbodySnapshot != null && _remoteRigidbodySnapshot.Count > 0 && LSnapshotIsMinorChange(_remoteRigidbodySnapshot))
                            _rigidbodyPauser.ApplySnapshot(_remoteRigidbodySnapshot, restoreSimulationState: false);
                        else if (_remoteRigidbody2dSnapshot != null && _remoteRigidbody2dSnapshot.Count > 0 && LSnapshot2dIsMinorChange(_remoteRigidbody2dSnapshot))
                            _rigidbodyPauser.ApplySnapshot(_remoteRigidbody2dSnapshot, restoreSimulationState: false);
                    }

                    //Returns if any prediction behaviour reconciled with the specified data source.
                    bool LIsReconciling(bool remote)
                    {
                        foreach (NetworkBehaviour nb in _predictionBehaviours)
                        {
                            if (nb.IsBehaviourReconciling && nb.IsReconcileRemote == remote)
                                return true;
                        }

                        return false;
                    }

                    //Returns true when every rigidbody is still within threshold of its snapshot.
                    bool LSnapshotIsMinorChange(List<RigidbodyPauser.RigidbodyData> snapshot)
                    {
                        const float sqrDistance = 0.001f;
                        const float angleDistance = 5f;

                        for (int i = 0; i < snapshot.Count; i++)
                        {
                            Rigidbody rb = snapshot[i].Rigidbody;
                            if (rb == null)
                                continue;
                            if ((rb.transform.position - snapshot[i].Position).sqrMagnitude > sqrDistance)
                                return false;
                            if (rb.transform.rotation.Angle(snapshot[i].Rotation, precise: true) > angleDistance)
                                return false;
                        }

                        return true;
                    }

                    bool LSnapshot2dIsMinorChange(List<RigidbodyPauser.Rigidbody2DData> snapshot)
                    {
                        const float sqrDistance = 0.0001f;
                        const float angleDistance = 4f;

                        for (int i = 0; i < snapshot.Count; i++)
                        {
                            Rigidbody2D rb = snapshot[i].Rigidbody2d;
                            if (rb == null)
                                continue;
                            if ((rb.position - snapshot[i].Position).sqrMagnitude > sqrDistance)
                                return false;
                            if (Mathf.Abs(Mathf.DeltaAngle(rb.rotation, snapshot[i].Rotation)) > angleDistance)
                                return false;
                        }

                        return true;
                    }
                }

                /* IsReconcileRemote and Reconcile_Client_End (which unsets IsBehaviourReconciling) are the
                 * flags the corrections above read to tell a remote reconcile from a local one, so they
                 * are cleared only after those corrections, not before. */
                foreach (NetworkBehaviour networkBehaviour in _predictionBehaviours)
                {
                    networkBehaviour.IsReconcileRemote = false;
                    networkBehaviour.Reconcile_Client_End();
                }

                /* Unpause rigidbody pauser. It's okay to do that here rather
                 * than per NB, where the pausing occurs, because once here
                 * the entire object is out of the replay cycle so there's
                 * no reason to try and unpause per NB. */
                if (_rigidbodyPauser != null)
                    _rigidbodyPauser.Unpause();

                IsObjectReconciling = false;
            }
        }

        private void PredictionManager_OnReplicateReplay(uint clientTick, uint serverTick)
        {
            using (_pm_OnReplicateReplay.Auto())
            {
                if (!IsClientInitialized)
                    return;

                uint replayTick = IsOwner ? clientTick : serverTick;

                foreach (NetworkBehaviour networkBehaviour in _predictionBehaviours)
                    networkBehaviour.Replicate_Replay_Start(replayTick);
            }
        }

        /// <summary>
        /// Registers a NetworkBehaviour if it uses prediction.
        /// </summary>
        /// <returns>True if behavior was registered or already registered.</returns>
        // ReSharper disable once UnusedMethodReturnValue.Local
        private bool TryRegisterPredictionBehaviour(NetworkBehaviour nb)
        {
            if (!nb.UsesPrediction)
                return false;

            _predictionBehaviours.Add(nb);
            return true;
        }

        /// <summary>
        /// Clears replication queue inserting them into the past replicates history when possible.
        /// This should only be called when client only.
        /// </summary>
        internal void EmptyReplicatesQueueIntoHistory()
        {
            foreach (NetworkBehaviour networkBehaviour in _predictionBehaviours)
                networkBehaviour.EmptyReplicatesQueueIntoHistory_Start();
        }

        /// <summary>
        /// Sets the last tick a NetworkBehaviour replicated with.
        /// </summary>
        /// <param name = "setUnordered">True to set unordered value, false to set ordered.</param>
        internal void SetReplicateTick(uint value, bool createdReplicate)
        {
            if (createdReplicate && Owner.IsValid)
                // ReSharper disable once RedundantArgumentDefaultValue
                Owner.ReplicateTick.Update(NetworkManager.TimeManager, value, EstimatedTick.OldTickOption.Discard);
        }
    }

    /// <summary>
    /// Place this component on your NetworkManager object to remove ownership of objects for a disconnecting client.
    /// This prevents any owned object from being despawned when the owner disconnects.
    /// </summary>
    public class GlobalPreserveOwnedObjects : MonoBehaviour
    {
        private void Awake()
        {
            ServerManager sm = GetComponent<ServerManager>();
            sm.Objects.OnPreDestroyClientObjects += Objects_OnPreDestroyClientObjects;
        }

        protected virtual void Objects_OnPreDestroyClientObjects(NetworkConnection conn)
        {
            foreach (NetworkObject networkObject in conn.Objects)
                networkObject.RemoveOwnership();
        }
    }

    /// <summary>
    /// Place this component on NetworkObjects you wish to remove ownership on for a disconnecting owner.
    /// This prevents the object from being despawned when the owner disconnects.
    /// </summary>
    public class NetworkPreserveOwnedObjects : NetworkBehaviour
    {
        public override void OnStartServer()
        {
            ServerManager.Objects.OnPreDestroyClientObjects += OnPreDestroyClientObjects;
        }

        public override void OnStopServer()
        {
            if (ServerManager != null)
                ServerManager.Objects.OnPreDestroyClientObjects -= OnPreDestroyClientObjects;
        }

        private void OnPreDestroyClientObjects(NetworkConnection conn)
        {
            if (conn == Owner)
                RemoveOwnership();
        }
    }
}