#if !PREDICTION_1

namespace FishNet.Object.Prediction
{
    public enum AdaptiveInterpolationType
    {
        /// <summary>
        /// Adaptive interpolation is disabled. An exact interpolation value is used.
        /// </summary>
        Off = 0,
        /// <summary>
        /// Visual disturbances caused by desynchronization are likely, but graphics are significantly closer to the actual position of the object.
        /// </summary>
        VeryLow = 1,
        /// <summary>
        /// Visual disturbances caused by desynchronization are uncommon, but still very possible for physics bodies. Graphics are closer to the actual position of the object.
        /// </summary>
        Low = 2,
        /// <summary>
        /// Visual disturbances caused by desynchronization are still possible but less likely. Graphics are moderately closer to the actual position of the object.
        /// </summary>
        Medium = 3,
        /// <summary>
        /// Visual disturbances caused by desynchronization are very unlikely. Graphics are are using interpolation almost approximate to clients ping.
        /// </summary>
        High = 4,
        /// <summary>
        /// Visual disturbances caused by desynchronization are extremely unlikely. Graphics are using a generous amount interpolation.
        /// </summary>
        VeryHigh = 5,

    }


}
#endif