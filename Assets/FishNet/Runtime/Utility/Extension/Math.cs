namespace FishNet.Utility.Extension
{

    public static class MathFN
    {

        public static sbyte ClampSByte(long value, sbyte min, sbyte max)
        {
            if (value < min)
                return min;
            else if (value > max)
                return max;
            else
                return (sbyte)value;
        }

        public static double ClampDouble(double value, double min, double max)
        {
            if (value < min)
                return min;
            else if (value > max)
                return max;
            else
                return value;
        }
    }

}