namespace FishNet.Managing.Scened.Data
{

    /// <summary>
    /// Current scenes which should exist for all players on the network.
    /// </summary>
    public class NetworkedScenesData
    {
        /// <summary>
        /// Single networked scene.
        /// </summary>
        public string Single = string.Empty;
        /// <summary>
        /// Additive networked scenes.
        /// </summary>
        public string[] Additive = new string[0];
    }

}