namespace FishNet.Managing.Statistic
{

    public struct NetworkTrafficArgs
    {
        /// <summary>
        /// Number of bytes sent to the server.
        /// </summary>
        public readonly ulong ToServerBytes;
        /// <summary>
        /// Number of bytes sent by the server.
        /// </summary>
        public readonly ulong FromServerBytes;

        public NetworkTrafficArgs(ulong toServerBytes, ulong fromServerBytes)
        {
            ToServerBytes = toServerBytes;
            FromServerBytes = fromServerBytes;
        }
    }
}