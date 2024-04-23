using UnityEngine;

namespace FishNet.Component.Prediction
{
    public sealed class NetworkCollision : NetworkCollider
    {
#if !PREDICTION_1
        protected override void Awake()
        {
            base.IsTrigger = false;
            base.Awake();
        }
#endif

    }

}