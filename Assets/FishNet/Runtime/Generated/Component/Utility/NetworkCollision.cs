using UnityEngine;

namespace FishNet.Component.Prediction
{
    public sealed class NetworkCollision : NetworkCollider
    {
#if PREDICTION_V2
        [Tooltip("Units to extend collision traces by. This is used to prevent missed overlaps when colliders do not intersect enough.")]
        [Range(0f, 100f)]
        [SerializeField]
        private float _additionalSize = 0.1f;

        protected override void Awake()
        {
            base.IsTrigger = false;
            base.Awake();
        }

        /// <summary>
        /// Units to extend collision traces by. This is used to prevent missed overlaps when colliders do not intersect enough.
        /// </summary>
        public override float GetAdditionalSize() => _additionalSize;
#endif

    }

}