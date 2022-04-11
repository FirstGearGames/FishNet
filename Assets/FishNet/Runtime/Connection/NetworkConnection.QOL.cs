using FishNet.Managing;
using System;

namespace FishNet.Connection
{

    /// <summary>
    /// A container for a connected client used to perform actions on and gather information for the declared client.
    /// </summary>
    public partial class NetworkConnection : IEquatable<NetworkConnection>
    {

        /// <summary>
        /// Returns the address of this connection.
        /// </summary>
        /// <returns></returns>
        public string GetAddress()
        {
            if (!IsValid)
                return string.Empty;
            if (NetworkManager == null)
                return string.Empty;

            return NetworkManager.TransportManager.Transport.GetConnectionAddress(ClientId);
        }
    }


}