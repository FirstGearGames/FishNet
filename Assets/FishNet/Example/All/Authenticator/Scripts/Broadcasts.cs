
using FishNet.Broadcast;

namespace FishNet.Example.Authenticating
{

    public struct PasswordBroadcast : IBroadcast
    {
        public string Password;
    }

    public struct ResponseBroadcast : IBroadcast
    {
        public bool Passed;
    }

}