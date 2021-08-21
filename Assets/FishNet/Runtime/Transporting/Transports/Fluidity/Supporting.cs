using System;
using ENet;

namespace Fluidity
{
    [Serializable]
    public enum ChannelTypes : byte
    {
        Reliable = PacketFlags.Reliable,   
        Unreliable = PacketFlags.Unsequenced
    }

    public struct GlobalConstants
    {
        public const string VERSION = "0.0.1b1";
        public const string SCHEME = "enet";
        public const string BIND_ALL_IPV4 = "0.0.0.0";
        public const string BIND_ALL = "::0";
    }

}

namespace Fluidity.Server
{

    public enum CommandTypes : byte
    {
        ClientWantsToStop,
        DisconnectPeerNextIteration,
        DisconnectPeerNow
    }

    public struct IncomingPacket
    {
        public byte Channel;
        public int ConnectionId;
        public Packet Packet;
    }


    public struct OutgoingPacket
    {
        public byte Channel;
        public int ConnectionId;
        public Packet Packet;
    }

    public struct CommandPacket
    {
        public CommandTypes Type;
        public int ConnectionId;
    }

    public struct ConnectionEvent
    {
        public bool Connected;
        public int ConnectionId;        
    }

}


namespace Fluidity.Client
{

    public enum CommandTypes : byte
    {
    }

    public struct IncomingPacket
    {
        public byte Channel;
        public Packet Packet;
    }


    public struct OutgoingPacket
    {
        public byte Channel;
        public Packet Packet;
    }

    public struct CommandPacket
    {
        public CommandTypes Type;
    }


    public struct ConnectionEvent
    {
        public bool Connected;
    }
}




