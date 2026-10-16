#if FISHNET_THREADED_COLLIDER_ROLLBACK
using System.Collections.Generic;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using Unity.Mathematics;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{
    public class ColliderRollback : NetworkBehaviour
    {
        #region Serialized.
#pragma warning disable CS0414
        /// <summary>
        /// How to configure the bounding box check.
        /// </summary>
        [Tooltip("How to configure the bounding box check.")]
        [SerializeField]
        private RollbackManager.BoundingBoxType _boundingBox = RollbackManager.BoundingBoxType.Disabled;
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
        /// Center for the bounding box. This is only used when BoundingBox is set to Manual.
        /// </summary>
        [Tooltip("Center for the bounding box.. This is only used when BoundingBox is set to Manual.")]
        [SerializeField]
        private Vector3 _boundingBoxCenter = new(0f, 0f, 0f);
        /// <summary>
        /// Local Rotation for the bounding box. This is only used when BoundingBox is set to Manual.
        /// </summary>
        [Tooltip("Center for the bounding box.. This is only used when BoundingBox is set to Manual.")]
        [SerializeField]
        private Quaternion _boundingBoxLocalRotation = Quaternion.identity;
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
        private List<Transform> _rollingColliders;
        #endregion

        #region Internal.
        /// <summary>
        /// Rollback data about ColliderParents.
        /// </summary>
        internal IReadOnlyList<Transform> GetRollingColliders() => _rollingColliders;
        /// <summary>
        /// BoundingBoxData.
        /// </summary>
        internal RollbackManager.BoundingBoxData GetBoundingBoxData() => new(
            _physicsType, _boundingBox, _boundingBoxSize / 2, _boundingBoxCenter, _boundingBoxLocalRotation);
        #endregion

        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializeRollingColliders();
            ChangeEventSubscriptions(true);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ChangeEventSubscriptions(false);
            DeinitializeRollingColliders();
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
        /// Initializes class for use.
        /// </summary>
        private void InitializeRollingColliders()
        {
            _rollingColliders = CollectionCaches<Transform>.RetrieveList();

            /* Generate a rolling collider for each
             * collider parent. */
            foreach (GameObject colliderParent in _colliderParents)
            {
                if (colliderParent.gameObject == null)
                    continue;
                
                _rollingColliders.Add(colliderParent.transform);
            }
        }

        /// <summary>
        /// Resets class state pooling objects.
        /// </summary>
        private void DeinitializeRollingColliders()
        {
            CollectionCaches<Transform>.StoreAndDefault(ref _rollingColliders);
        }
        // PROEND
    }
}
#endif