namespace FishNet.Component.ColliderRollback
{

    public struct PreciseTick
    {
        public uint Tick;
        public byte Percent;

        public PreciseTick(uint tick, byte percent)
        {
            Tick = tick;
            Percent = percent;
        }
    }

}