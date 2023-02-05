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
        public float SendRate = 0f;
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

        public Settings(WritePermission writePermission, ReadPermission readPermission, float sendRate, Channel channel)
        {
            WritePermission = writePermission;
            ReadPermission = readPermission;
            SendRate = sendRate;
            Channel = channel;
        }

        public Settings(float sendTickrate)
        {
            SendRate = sendTickrate;
        }

        public Settings(ReadPermission readPermission, float sendRate, Channel channel)
        {
            ReadPermission = readPermission;
            SendRate = sendRate;
            Channel = channel;
        }

    }
}
