
using FishNet.Broadcast;
using FishNet.Utility.Performance;
using System.Collections.Generic;

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
        public List<int> Ids;
    }


}