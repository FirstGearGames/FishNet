using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{

    public class ColliderRollback : NetworkBehaviour
    {
        #region Types.
        internal enum BoundingBoxType
        {
            /// <summary>
            /// Disable this feature.
            /// </summary>
            Disabled,
            /// <summary>
            /// Manually specify the dimensions of a bounding box.
            /// </summary>
            Manual,
        }
        
        #endregion

        #region Serialized.
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
        private Vector3 _boundingBoxSize = new Vector3(3f, 3f, 3f);
        /// <summary>
        /// Objects holding colliders which can rollback.
        /// </summary>
        [Tooltip("Objects holding colliders which can rollback.")]
        [SerializeField]
        private GameObject[] _colliderParents = new GameObject[0];
        #endregion

        
    }

}
