using FishNet.Object;

public class Item : NetworkBehaviour
{
    private float _weight;

    public float Weight => _weight;

    [Server]
    public void UpdateWeight(float weight)
    {
        _weight = weight;
    }
}