using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Managing.Transporting
{

    /// <summary>
    /// Communicates with the Transport to send and receive data.
    /// </summary>
    public sealed partial class TransportManager : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Returns IsLocalTransport for the current transport.
        /// </summary>
        public bool IsLocalTransport(int connectionId) => (Transport == null) ? false : Transport.IsLocalTransport(connectionId);
        #endregion

    }


}