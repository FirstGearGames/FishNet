using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;

namespace FishNet.Observing
{
    /// <summary>
    /// Decides, per observer and per send, whether an unreliable ObserversRpc from a NetworkObject should reach that observer.
    /// Assign an implementation to <see cref = "NetworkObject.ObserverSendFilter"/> on the server.
    /// </summary>
    /// <remarks>
    /// Observer conditions decide which connections see an object; this decides how often each of them hears from it,
    /// such as sending NetworkTransform updates to distant observers every other tick.
    /// Only unbuffered ObserversRpcs sent on the unreliable channel are filtered. A skipped unreliable message looks the
    /// same to the receiver as a lost packet, which unreliable senders already tolerate. Reliable and buffered RPCs, such
    /// as a NetworkTransform settle, teleport or interval change, always reach every observer.
    /// The first unreliable ObserversRpc a NetworkBehaviour sends after spawning, or after a reliable ObserversRpc, is
    /// also not filtered. Receivers such as NetworkTransform treat the message after a reliable one as a single interval
    /// of change, so skipping it would make the next message play several intervals at once.
    /// The same applies to an observer that was just added, whose baseline is the spawn message. This is per observer, so
    /// it is left to the filter: let the first unreliable send to a new observer through. NetworkBehaviour.OnSpawnServer
    /// and OnDespawnServer are called for each observer added and removed, and can be used to track this.
    /// Observers already excluded from the RPC, such as the owner when ExcludeOwner is set, are not passed to the filter.
    /// This is called for every observer on every filtered send, so implementations should be fast and not allocate.
    /// When throttling NetworkTransform, keep the gap between messages within its interpolation, otherwise observers
    /// will pause while waiting for enough data to interpolate.
    /// </remarks>
    public interface IObserverSendFilter
    {
        /// <summary>
        /// Returns true to send the RPC to connection, false to skip it for this send.
        /// </summary>
        /// <param name = "networkObject">Object sending the RPC.</param>
        /// <param name = "connection">Observer the RPC would be sent to.</param>
        /// <param name = "channel">Channel the RPC is being sent on. This is currently always Unreliable.</param>
        bool ShouldSend(NetworkObject networkObject, NetworkConnection connection, Channel channel);
    }
}