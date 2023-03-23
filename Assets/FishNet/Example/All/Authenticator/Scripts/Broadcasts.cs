
using FishNet.Broadcast;

namespace FishNet.Example.Authenticating
{
    public struct HostPasswordBroadcast : IBroadcast
    {
        public string Password;
    }

    public struct PasswordBroadcast : IBroadcast
    {
        public string Password;
    }

    public struct ResponseBroadcast : IBroadcast
    {
        public bool Passed;
    }

}