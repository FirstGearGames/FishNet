public sealed class NetworkTrigger : NetworkCollider
{
#if PREDICTION_V2
    protected override void Awake()
    {
        base.IsTrigger = true;
        base.Awake();
    }
#endif
}
