using UnityEngine;

namespace FishNet.Component.Prediction
{
    public sealed class NetworkCollision2D : NetworkCollider2D
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