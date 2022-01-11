using FishNet.Utility.Performance;
using System;

namespace FishNet.Tugboat
{


    internal struct Packet
    {
        public readonly int ConnectionId;
        public readonly byte[] Data;
        public readonly int Length;
        public readonly byte Channel;

        public Packet(int connectionId, byte[] data, int length, byte channel)
        {
            ConnectionId = connectionId;
            Data = data;
            Length = length;
            Channel = channel;
        }

        public Packet(int sender, ArraySegment<byte> segment, byte channel)
        {
            Data = ByteArrayPool.Retrieve(segment.Count, true);
            Buffer.BlockCopy(segment.Array, segment.Offset, Data, 0, segment.Count);
            ConnectionId = sender;
            Length = segment.Count;
            Channel = channel;
        }

        public ArraySegment<byte> GetArraySegment()
        {
            return new ArraySegment<byte>(Data, 0, Length);
        }

        public void Dispose()
        {
            ByteArrayPool.Store(Data, true);
        }

    }


}

namespace FishNet.Tugboat.Server
{

    internal struct RemoteConnectionEvent
    {
        public readonly bool Connected;
        public readonly int ConnectionId;
        public RemoteConnectionEvent(bool connected, int connectionId)
        {
            Connected = connected;
            ConnectionId = connectionId;
        }
    }
}

