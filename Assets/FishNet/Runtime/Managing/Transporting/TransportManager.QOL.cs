using FishNet.Connection;
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
        /// <summary>
        /// Returns IsLocalTransport for the transportId, optionally checking against a connectionId.
        /// </summary>
        public bool IsLocalTransport(int transportId, int connectionId = NetworkConnection.UNSET_CLIENTID_VALUE)
        {
            if (Transport == null)
                return false;

            if (Transport is Multipass mp)
                return mp.IsLocalTransport(transportId, connectionId);
            else
                return Transport.IsLocalTransport(connectionId);
        }

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
        /// <returns>Returns the found transport which is of type T. Returns default of T if not found.</returns>
        public T GetTransport<T>() where T : Transport
        {
            //If using multipass try to find the correct transport.
            if (Transport is Multipass mp)
            {
                if (typeof(T) == typeof(Multipass))
                    return (T)(object)mp;
                else
                    return mp.GetTransport<T>();
            }
            //Not using multipass.
            else
            {
                if (Transport.GetType() == typeof(T))
                    return (T)(object)Transport;
                else
                    return default(T);
            }
        }
    }

}