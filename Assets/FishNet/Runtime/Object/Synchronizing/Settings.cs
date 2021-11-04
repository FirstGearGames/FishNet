using FishNet.Transporting;

namespace FishNet.Object.Synchronizing.Internal
{
    public class Settings
    {
        /// <summary>
        /// Defines the write permissions for this var
        /// </summary>
        public WritePermission WritePermission = WritePermission.ServerOnly;
        /// <summary>
        /// Clients which may receive updated values.
        /// </summary>
        public ReadPermission ReadPermission = ReadPermission.Observers;
        /// <summary>
        /// How often this variable may synchronize.
        /// </summary>
        public float SendTickRate = 0f;
        /// <summary>
        /// Channel to send values on.
        /// </summary>
        public Channel Channel = Channel.Reliable;

        /// <summary>
        /// Constructs a new NetworkedVarSettings instance
        /// </summary>
        public Settings()
        {

        }

        public Settings(WritePermission writePermission, ReadPermission readPermission, float sendTickrate, Channel channel)
        {
            WritePermission = writePermission;
            ReadPermission = readPermission;
            SendTickRate = sendTickrate;
            Channel = channel;
        }

        public Settings(float sendTickrate)
        {
            SendTickRate = sendTickrate;
        }

        public Settings(ReadPermission readPermission, float sendTickrate, Channel channel)
        {
            ReadPermission = readPermission;
            SendTickRate = sendTickrate;
            Channel = channel;
        }

    }
}
