using FishNet.Transporting;

namespace FishNet.Object.Synchronizing
{
    /// <summary>
    /// Settings which can be passed into SyncTypes.
    /// </summary>
    public struct SyncTypeSetting
    {
        public WritePermission WritePermission;
        public ReadPermission ReadPermission;
        public float SendRate;
        public Channel Channel;

        //Work around for C# parameterless struct limitation.
        public SyncTypeSetting(float sendRate = 0.1f)
        {
            WritePermission = WritePermission.ServerOnly;
            ReadPermission = ReadPermission.Observers;
            SendRate = sendRate;
            Channel = Channel.Reliable;
        }

        public SyncTypeSetting(float sendRate, Channel channel)
        {
            WritePermission = WritePermission.ServerOnly;
            ReadPermission = ReadPermission.Observers;
            SendRate = sendRate;
            Channel = channel;
        }

        public SyncTypeSetting(Channel channel)
        {
            WritePermission = WritePermission.ServerOnly;
            ReadPermission = ReadPermission.Observers;
            SendRate = 0.1f;
            Channel = channel;
        }

        public SyncTypeSetting(WritePermission writePermissions)
        {

            WritePermission = writePermissions;
            ReadPermission = ReadPermission.Observers;
            SendRate = 0.1f;
            Channel = Channel.Reliable;
        }

        public SyncTypeSetting(ReadPermission readPermissions)
        {
            WritePermission = WritePermission.ServerOnly;
            ReadPermission = readPermissions;
            SendRate = 0.1f;
            Channel = Channel.Reliable;
        }

        public SyncTypeSetting(WritePermission writePermissions, ReadPermission readPermissions)
        {

            WritePermission = writePermissions;
            ReadPermission = readPermissions;
            SendRate = 0.1f;
            Channel = Channel.Reliable;
        }

        public SyncTypeSetting(WritePermission writePermissions, ReadPermission readPermissions, float sendRate, Channel channel)
        {
                
            WritePermission = writePermissions;
            ReadPermission = readPermissions;
            SendRate = sendRate;
            Channel = channel;
        }
    }
}
