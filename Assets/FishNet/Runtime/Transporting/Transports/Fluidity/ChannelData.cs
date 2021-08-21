namespace Fluidity
{

    [System.Serializable]
    public struct ChannelData
    {
        public ChannelTypes ChannelType;
        public int MaximumTransmissionUnit;

        public ChannelData(ChannelTypes channelType)
        {
            ChannelType = channelType;
            MaximumTransmissionUnit = 1200;
        }
        public ChannelData(ChannelTypes channelType, int maximumTransmissionUnit)
        {
            ChannelType = channelType;
            MaximumTransmissionUnit = maximumTransmissionUnit;
        }
    }

}