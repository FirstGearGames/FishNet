namespace FishNet.Object.Helping
{
    public enum RpcType : byte
    {
        None = 0,
        Server = 1,
        Observers = 2,
        Target = 3,
        Replicate = 4,
        Reconcile = 5
    }

}