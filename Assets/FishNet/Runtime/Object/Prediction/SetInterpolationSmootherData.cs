using UnityEngine;

namespace FishNet.Object.Prediction
{
    /// <summary>
    /// Data to be used to configure smoothing for an owned predicted object.
    /// </summary>
    internal struct SetInterpolationSmootherData
    {
        public Transform GraphicalObject;
        public byte Interpolation; 
        public NetworkObject NetworkObject;
        public bool SmoothPosition;
        public bool SmoothRotation;
        public bool SmoothScale;
        public float TeleportThreshold;
    }


}