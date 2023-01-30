using FishNet.Broadcast;
using FishNet.Utility.Performance;

namespace FishNet.Managing.Server
{
    public struct ClientConnectionChangeBroadcast : IBroadcast
    {
        public bool Connected;
        public int Id;
    }

    public struct ConnectedClientsBroadcast : IBroadcast
    {
        public ListCache<int> ListCache; 
    }


}