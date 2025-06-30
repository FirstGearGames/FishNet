namespace FishNet.Utility.Performance.Profiling
{
    public static class Names
    {
        public const string PLAYER_COUNT = "All Players Count";
        public const string PLAYER_COUNT_TOOLTIP = "Number of players connected to the server";
        public const string AUTHENTICATED_COUNT = "Authenticated Players Count";
        public const string AUTHENTICATED_COUNT_TOOLTIP = "Number of Authenticated players connected to the server, This should be same as total players most of the time";
        public const string CHARACTER_COUNT = "Character Count";
        public const string CHARACTER_COUNT_TOOLTIP = "Number of players with spawned GameObjects";

        public const string OBJECT_COUNT = "Object Count";
        public const string OBJECT_COUNT_TOOLTIP = "Number of NetworkObjects spawned";

        public const string SENT_COUNT = "Sent Messages";
        public const string SENT_BYTES = "Sent Bytes";
        public const string SENT_PER_SECOND = "Sent Per Second";

        public const string RECEIVED_COUNT = "Received Messages";
        public const string RECEIVED_BYTES = "Received Bytes";
        public const string RECEIVED_PER_SECOND = "Received Per Second";

        public const string PER_SECOND_TOOLTIP = "Sum of Bytes over the previous second";
    }
}
