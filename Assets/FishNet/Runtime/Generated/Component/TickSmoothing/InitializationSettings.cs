using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Transforming.Beta
{
    [System.Serializable]
    public struct InitializationSettings
    {
        /// <summary>
        /// While this script is typically placed on a nested graphical object, the targetTransform would be the object which moves every tick; the TargetTransform can be the same object this script resides but may not be a rigidbody if true;
        /// </summary>
        [Tooltip("While this script is typically placed on a nested graphical object, the targetTransform would be the object which moves every tick; the TargetTransform can be the same object this script resides but may not be a rigidbody if true;")]
        [SerializeField]
        public  Transform TargetTransform;
        /// <summary>
        /// The transform which is smoothed.
        /// </summary>
        [Tooltip("The transform which is smoothed.")]
        [System.NonSerialized]
        internal Transform GraphicalTransform;
        /// <summary>
        /// True to detacth this object from its parent on client start.
        /// </summary>
        [Tooltip("True to detach this object from it's parent on client start.")]
        public bool DetachOnStart;
        /// <summary>
        /// True to re-attach this object to it's parent on client stop.
        /// </summary>
        [Tooltip("True to re-attach this object to it's parent on client stop.")]
        public bool AttachOnStop;
        /// <summary>
        /// True to begin moving soon as movement data becomes available. Movement will ease in until at interpolation value. False to prevent movement until movement data count meet interpolation.
        /// </summary>
        [Tooltip("True to begin moving soon as movement data becomes available. Movement will ease in until at interpolation value. False to prevent movement until movement data count meet interpolation.")]
            
        public bool MoveImmediately => false;
        /// <summary>
        /// NetworkBehaviour which initialized these settings. This value may be null if not initialized from a NetworkBehaviour.
        /// </summary>
        [System.NonSerialized]
        internal NetworkBehaviour InitializingNetworkBehaviour;
        /// <summary>
        /// TimeManager initializing these settings.
        /// </summary>
        [System.NonSerialized]
        internal TimeManager InitializingTimeManager;

        public void UpdateRuntimeSettings(NetworkBehaviour initializingNetworkBehaviour, Transform graphicalTransform, float tickDelta)
        {
            InitializingNetworkBehaviour = initializingNetworkBehaviour;
            GraphicalTransform = graphicalTransform;
            InitializingTimeManager = initializingNetworkBehaviour.TimeManager;
        }
        public void UpdateRuntimeSettings(TimeManager timeManager, Transform graphicalTransform, float tickDelta)
        {
            InitializingNetworkBehaviour = null;
            GraphicalTransform = graphicalTransform;
            InitializingTimeManager = timeManager;
        }
    }
}