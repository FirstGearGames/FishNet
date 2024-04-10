namespace FishNet.Component.Prediction
{

    public sealed class NetworkTrigger : NetworkCollider
    {
#if !PREDICTION_1
        protected override void Awake()
        {
            base.IsTrigger = true;
            base.Awake();
        }
#endif
    }

}