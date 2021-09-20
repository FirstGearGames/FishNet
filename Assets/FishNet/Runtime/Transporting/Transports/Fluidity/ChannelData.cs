namespace Fluidity
{

    [System.Serializable]
    public struct ChannelData
    {
        public ChannelType ChannelType;
        public int MaximumTransmissionUnit;

        public ChannelData(ChannelType channelType)
        {
            ChannelType = channelType;
            MaximumTransmissionUnit = 1200;
        }
        public ChannelData(ChannelType channelType, int maximumTransmissionUnit)
        {
            ChannelType = channelType;
            MaximumTransmissionUnit = maximumTransmissionUnit;
        }
    }

}