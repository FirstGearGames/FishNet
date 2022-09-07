using FishNet.Managing;
using FishNet.Serializing.Helping;
using System;

namespace FishNet.Connection
{

    /// <summary>
    /// A container for a connected client used to perform actions on and gather information for the declared client.
    /// </summary>
    public partial class NetworkConnection : IEquatable<NetworkConnection>
    {

        /// <summary>
        /// Called when the server performs replication on any object which has this connection as the owner.
        /// </summary>
        [CodegenExclude] //Make private.
        public void ReplicationPerformedInternal()
        {

        }
     
    }


}