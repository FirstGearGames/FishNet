using System;

namespace FishNet.Utility.Extension
{
    public static class EnumFN
    {

        /// <summary>
        /// Returns the highest numeric value for T.
        /// </summary>
        internal static int GetHighestValue<T>()
        {
            Type enumType = typeof(T);
            /* Brute force enum values. 
             * Linq Last/Max lookup throws for IL2CPP. */
            int highestValue = 0;
            Array pidValues = Enum.GetValues(enumType);
            foreach (T pid in pidValues)
            {
                object obj = Enum.Parse(enumType, pid.ToString());
                int value = Convert.ToInt32(obj);
                highestValue = Math.Max(highestValue, value);
            }

            return highestValue;
        }


    }

}