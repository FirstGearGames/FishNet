using FishNet.Broadcast;
using FishNet.Serializing;
using FishNet.Utility.Performance;
using GameKit.Utilities;
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
        public List<int> Values;
    }

    internal static class ConnectedClientsBroadcastSerializers
    {
        public static void WriteConnectedClientsBroadcast(this PooledWriter writer, ConnectedClientsBroadcast value)
        {
            writer.WriteList(value.Values);
        }

        public static ConnectedClientsBroadcast ReadConnectedClientsBroadcast(this PooledReader reader)
        {
            List<int> cache = CollectionCaches<int>.RetrieveList();
            reader.ReadList(ref cache);
            return new ConnectedClientsBroadcast()
            {
                Values = cache
            };
        }

    }
}

