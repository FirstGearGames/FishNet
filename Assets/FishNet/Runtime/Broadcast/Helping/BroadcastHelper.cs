using GameKit.Utilities;

namespace FishNet.Broadcast.Helping
{
    internal static class BroadcastHelper
    {
        /// <summary>
        /// Gets the key for a broadcast type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="broadcastType"></param>
        /// <returns></returns>
        public static ushort GetKey<T>()
        {
            return typeof(T).FullName.GetStableHashU16();
        }
    }

}