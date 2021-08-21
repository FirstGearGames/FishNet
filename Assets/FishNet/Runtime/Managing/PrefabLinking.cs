using FishNet.Object;

namespace FishNet.Managing
{

    public enum PrefabLinkingType
    {
        Single = 1,
        Dual = 2
    }

    [System.Serializable]
    public struct DualPrefab
    {
        public NetworkObject Server;
        public NetworkObject Client;
    }

}