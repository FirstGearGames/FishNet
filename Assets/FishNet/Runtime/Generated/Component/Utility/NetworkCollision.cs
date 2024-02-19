public sealed class NetworkCollision : NetworkCollider
{
    #if PREDICTION_V2
    protected override void Awake()
    {
        base.IsTrigger = false;
        base.Awake();
    }
#endif

}
