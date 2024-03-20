namespace FishNet.Component.Prediction
{

    public sealed class NetworkTrigger2D : NetworkCollider2D
    {
#if PREDICTION_V2
        protected override void Awake()
        {
            base.IsTrigger = true;
            base.Awake();
        }
#endif
    }

}