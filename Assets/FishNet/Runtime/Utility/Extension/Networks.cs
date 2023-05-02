using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object;

namespace FishNet.Utility.Extension
{
    [APIExclude]
    public static class NetworksFN
    {
        /// <summary>
        /// Returns if logic could have potentially called already on server side, and is calling a second time for clientHost side.
        /// </summary>
        public static bool DoubleLogic(this NetworkObject nob, bool asServer) => (!asServer && nob.NetworkManager.IsServer);
        /// <summary>
        /// Returns if logic could have potentially called already on server side, and is calling a second time for clientHost side.
        /// </summary>
        public static bool DoubleLogic(this NetworkManager manager, bool asServer) => (!asServer && manager.IsServer);
        /// <summary>
        /// Returns if logic could have potentially called already on server side, and is calling a second time for clientHost side.
        /// </summary>
        public static bool DoubleLogic(this NetworkBehaviour nb, bool asServer) => (!asServer && nb.NetworkManager.IsServer);

    }

}