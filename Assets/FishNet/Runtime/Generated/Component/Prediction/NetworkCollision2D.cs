using UnityEngine;

namespace FishNet.Component.Prediction
{
    public sealed class NetworkCollision2D : NetworkCollider2D
    {
#if PREDICTION_V2

        /// <summary>
        /// Percentage larger than each collider for each overlap test. This is used to prevent missed overlaps when colliders do not intersect enough.
        /// </summary>
        [Tooltip("Percentage larger than each collider for each overlap test. This is used to prevent missed overlaps when colliders do not intersect enough.")]
        [Range(0f, 0.2f)]
        [SerializeField]
        private float _sizeMultiplier = 0.05f;

        protected override void Awake()
        {
            base.IsTrigger = false;
            base.Awake();
        }

        protected override float GetSizeMultiplier() => (1f + _sizeMultiplier);
#endif

    }

}