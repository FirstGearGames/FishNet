#if !FISHNET_THREADED_COLLIDER_ROLLBACK
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{
    public partial class ColliderRollback : NetworkBehaviour
    {
        #region Serialized.
#pragma warning disable CS0414
        /// <summary>
        /// How to configure the bounding box check.
        /// </summary>
        [Tooltip("How to configure the bounding box check.")]
        [SerializeField]
        private BoundingBoxType _boundingBox = BoundingBoxType.Disabled;
        /// <summary>
        /// Physics type to generate a bounding box for.
        /// </summary>
        [Tooltip("Physics type to generate a bounding box for.")]
        [SerializeField]
        private RollbackPhysicsType _physicsType = RollbackPhysicsType.Physics;
        /// <summary>
        /// Size for the bounding box. This is only used when BoundingBox is set to Manual.
        /// </summary>
        [Tooltip("Size for the bounding box.. This is only used when BoundingBox is set to Manual.")]
        [SerializeField]
        private Vector3 _boundingBoxSize = new(3f, 3f, 3f);
        /// <summary>
        /// Objects holding colliders which can rollback.
        /// </summary>
        [Tooltip("Objects holding colliders which can rollback.")]
        [SerializeField]
        private GameObject[] _colliderParents = new GameObject[0];
#pragma warning restore CS0414
        #endregion

        // PROSTART

        #region Private.
        /// <summary>
        /// Rollback data about ColliderParents.
        /// </summary>
        private List<RollingCollider> _rollingColliders;
        /// <summary>
        /// True if rolled back.
        /// </summary>
        private bool _rolledBack;
        /// <summary>
        /// Maximum snapshots allowed. Generated at runtime using snapshot interval and max rollback time.
        /// </summary>
        private int _maxSnapshots;
        /// <summary>
        /// Becomes true once bounding box is made.
        /// </summary>
        private bool _boundingBoxCreated;
        /// <summary>
        /// Number of snapshots written. This value is used to determine if snapshots can be lerped.
        /// </summary>
        private byte _lerpSnapshotCounter;
        #endregion

        public override void OnStartNetwork()
        {
            if (IsServerStarted)
            {
                _maxSnapshots = Mathf.CeilToInt(RollbackManager.MaximumRollbackTime / (float)TimeManager.TickDelta);
                if (_maxSnapshots < 2)
                    _maxSnapshots = 2;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            CreateBoundingBox();
            ChangeEventSubscriptions(true);
            InitializeRollingColliders();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ChangeEventSubscriptions(false);
            DeinitializeRollingColliders();
        }

        /// <summary>
        /// Creates a bounding box collider around this object.
        /// </summary>
        private void CreateBoundingBox()
        {
            if (_boundingBoxCreated)
                return;
            // If here then mark created as true.
            _boundingBoxCreated = true;

            if (_boundingBox == BoundingBoxType.Disabled)
                return;

            int? layer = RollbackManager.BoundingBoxLayerNumber;
            if (layer == null)
                return;

            // Make go to add bounds to.
            GameObject go = new("Rollback Bounding Box");
            go.transform.SetParent(transform);
            go.transform.SetPositionAndRotation(transform.position, transform.rotation);

            if (_boundingBox == BoundingBoxType.Manual)
            {
                go.layer = layer.Value;
                /* Flags check isn't working, not sure why yet. Maybe because enum is part
                 * of a different class? */
                if (_physicsType == RollbackPhysicsType.Physics)
                    go.AddComponent<BoxCollider>();
                else if (_physicsType == RollbackPhysicsType.Physics2D)
                    go.AddComponent<BoxCollider2D>();
                go.transform.localScale = _boundingBoxSize;
            }
        }

        /// <summary>
        /// Subscribes or unsubscribes to events needed for rolling back.
        /// </summary>
        /// <param name = "subscribe"></param>
        private void ChangeEventSubscriptions(bool subscribe)
        {
            RollbackManager rm = RollbackManager;
            if (rm == null)
                return;

            if (subscribe)
                rm.RegisterColliderRollback(this);
            else
                rm.UnregisterColliderRollback(this);
        }

        /// <summary>
        /// Called when a snapshot should be created.
        /// </summary>
        internal void CreateSnapshot()
        {
            // Can't generate a snapshot while rolled back.
            if (_rolledBack)
                return;

            if (_lerpSnapshotCounter < _maxSnapshots)
                _lerpSnapshotCounter++;

            int count = _rollingColliders.Count;
            for (int i = 0; i < count; i++)
                _rollingColliders[i].AddSnapshot();
        }

        /// <summary>
        /// Called when a rollback should occur.
        /// </summary>
        internal void Rollback(float time)
        {
            // Already rolled back.
            if (_rolledBack)
            {
                NetworkManager.LogWarning("Colliders are already rolled back. Returning colliders forward first.");
                Return();
            }
            // None are written.
            else if (_lerpSnapshotCounter == 0)
            {
                return;
            }

            FrameRollbackTypes rollbackType;
            int endFrame;
            float percent;

            /* If time were 0.3f and delta was 0.2f then the
             * result would be 1.5f. This indicates to lerp between
             * the first snapshot, and one after. */
            float decimalFrame = time / (float)TimeManager.TickDelta;
            /* Rollback is beyond written quantity.
             * Set to use the last snapshot. */
            if (decimalFrame > _lerpSnapshotCounter)
            {
                rollbackType = FrameRollbackTypes.Exact;
                // Be sure to subtract 1 to get last entry in snapshots.
                endFrame = _lerpSnapshotCounter - 1;
                // Not needed for exact but must be set.
                percent = 1f;
            }
            // Within frames.
            else
            {
                percent = decimalFrame % 1;
                endFrame = Mathf.CeilToInt(decimalFrame);

                /* If the end frame is larger than or equal to 1
                 * then a lerp between two snapshots can occur. If
                 * equal to 1 then the lerp would occur between 0 and 1. */
                if (endFrame >= 1)
                {
                    rollbackType = FrameRollbackTypes.LerpMiddle;
                    endFrame = Mathf.CeilToInt(decimalFrame);
                }
                // Rolling back only 1 frame.
                else
                {
                    endFrame = 0;
                    rollbackType = FrameRollbackTypes.LerpFirst;
                }
            }


            int count = _rollingColliders.Count;
            for (int i = 0; i < count; i++)
                _rollingColliders[i].Rollback(rollbackType, endFrame, percent);

            _rolledBack = true;
        }

        /// <summary>
        /// Called when a specific collider should return.
        /// </summary>
        internal void Return()
        {
            if (!_rolledBack)
                return;

            int count = _rollingColliders.Count;
            for (int i = 0; i < count; i++)
                _rollingColliders[i].Return();

            _rolledBack = false;
        }

        /// <summary>
        /// Initializes class for use.
        /// </summary>
        private void InitializeRollingColliders()
        {
            _rollingColliders = ResettableCollectionCaches<RollingCollider>.RetrieveList();

            /* Generate a rolling collider for each
             * collider parent. */
            foreach (GameObject colliderParent in _colliderParents)
            {
                if (colliderParent.gameObject == null)
                    continue;

                /* Creates a new rolling collider and fills the snapshots with it's current
                 * position. If you were to roll back before all snapshots could fill
                 * with new data an incorrect rollback position/rotation would be returned
                 * but the chances of this happening are slim to none, and impossible after
                 * the MAX_ROLLBACK_TIME duration has passed. */
                RollingCollider rc = ResettableObjectCaches<RollingCollider>.Retrieve();
                rc.Initialize(colliderParent.transform, _maxSnapshots);
                _rollingColliders.Add(rc);
            }
        }

        /// <summary>
        /// Resets class state pooling objects.
        /// </summary>
        private void DeinitializeRollingColliders()
        {
            _lerpSnapshotCounter = 0;
            ResettableCollectionCaches<RollingCollider>.StoreAndDefault(ref _rollingColliders);
        }
        // PROEND
    }
}
#endif