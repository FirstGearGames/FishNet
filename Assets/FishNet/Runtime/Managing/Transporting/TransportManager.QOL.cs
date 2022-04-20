using FishNet.Transporting;
using FishNet.Transporting.Multipass;
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


        /// <summary>
        /// Gets transport on index.
        /// Commonly index will be 0 unless using Multipass.
        /// </summary>
        /// <returns></returns>
        public Transport GetTransport(int index)
        {
            //If using multipass try to find the correct transport.
            if (Transport is Multipass mp)
            {
                return mp.GetTransport(index);
            }
            //Not using multipass.
            else
            {
                return Transport;
            }
        }

        /// <summary>
        /// Gets transport of type T.
        /// </summary>
        /// <returns>Returns the found transport which is of type T. Returns null if not found.</returns>
        public Transport GetTransport<T>() where T : Transport
        {
            //If using multipass try to find the correct transport.
            if (Transport is Multipass mp)
            {
                if (typeof(T) == typeof(Multipass))
                    return mp;
                else
                    return mp.GetTransport<T>();
            }
            //Not using multipass.
            else
            {
                if (Transport.GetType() == typeof(T))
                    return Transport;
                else
                    return null;
            }
        }
    }

}