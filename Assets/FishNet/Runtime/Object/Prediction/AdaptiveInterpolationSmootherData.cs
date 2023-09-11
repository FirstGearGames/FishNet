using UnityEngine;

namespace FishNet.Object.Prediction
{
    /// <summary>
    /// How to favor smoothing for predicted objects.
    /// </summary>
    internal enum AdaptiveSmoothingType
    {
        /// <summary>
        /// Favor accurate collisions. With fast moving objects this may result in some jitter with higher latencies.
        /// </summary>
        Accuracy = 0,
        /// <summary>
        /// A mix between Accuracy and Smoothness.
        /// </summary>
        Mixed = 1,
        /// <summary>
        /// Prefer smooth movement and corrections. Fast moving objects may collide before the graphical representation catches up.
        /// </summary>
        Gradual = 2,
        /// <summary>
        /// Configure values to your preference.
        /// </summary>
        Custom = 3,
    }

    [System.Serializable]
    public struct AdaptiveInterpolationSmoothingData
    {
        [HideInInspector, System.NonSerialized]
        public bool SmoothPosition;
        [HideInInspector, System.NonSerialized]
        public bool SmoothRotation;
        [HideInInspector, System.NonSerialized]
        public bool SmoothScale;
        [HideInInspector,System.NonSerialized]
        public Transform GraphicalObject;
        [HideInInspector,System.NonSerialized]
        public NetworkObject NetworkObject;
        [HideInInspector, System.NonSerialized]
        public float TeleportThreshold;

        /// <summary>
        /// Percentage of ping to use as interpolation. Higher values will result in more interpolation.
        /// </summary>
        [Tooltip("Percentage of ping to use as interpolation. Higher values will result in more interpolation.")]
        [Range(0.01f, 5f)]
        public float NormalPercent;
        /// <summary>
        /// Percentage of ping to use as interpolation when colliding with an object local client owns.
        /// This is used to speed up local interpolation when predicted objects collide with a player as well keep graphics closer to the objects root while colliding.
        /// </summary>
        [Tooltip("Percentage of ping to use as interpolation when colliding with an object local client owns." +
            "This is used to speed up local interpolation when predicted objects collide with a player as well keep graphics closer to the objects root while colliding.")]
        [Range(0.01f, 5f)]
        public float CollisionPercent;
        /// <summary>
        /// How much per tick to decrease to collision interpolation when colliding with a local player object.
        /// Higher values will set interpolation to collision settings faster.
        /// </summary>
        [Tooltip("How much per tick to decrease to collision interpolation when colliding with a local player object. Higher values will set interpolation to collision settings faster.")]
        [Range(0.1f, 10f)]
        public float CollisionStep;
        /// <summary>
        /// How much per tick to increase to normal interpolation when not colliding with a local player object.
        /// Higher values will set interpolation to normal settings faster.
        /// </summary>
        [Tooltip("How much per tick to increase to normal interpolation when not colliding with a local player object. Higher values will set interpolation to normal settings faster.")]
        [Range(0.1f, 10f)]
        public float NormalStep;

        /// <summary>
        /// Interpolation applied regardless of settings when colliding with a localClient object.
        /// </summary>
        internal const byte BASE_COLLISION_INTERPOLATION = 0;
        /// <summary>
        /// Interpolation applied regardless of settings when not colliding with a localClient object.
        /// </summary>
        internal const byte BASE_NORMAL_INTERPOLATION = 1;
    }

}