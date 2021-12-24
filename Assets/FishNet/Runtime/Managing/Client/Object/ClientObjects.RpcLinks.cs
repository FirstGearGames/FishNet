using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Object.Helping;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Client
{
    /// <summary>
    /// Handles objects and information about objects for the local client. See ManagedObjects for inherited options.
    /// </summary>
    public partial class ClientObjects : ManagedObjects
    {

        #region Private.
        /// <summary>
        /// RPCLinks of currently spawned objects.
        /// </summary>
        private Dictionary<ushort, RpcLink> _rpcLinks = new Dictionary<ushort, RpcLink>();
        #endregion

        /// <summary>
        /// Parses a received RPCLink.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ParseRpcLink(PooledReader reader, ushort index, Channel channel)
        {
            int dataLength;
            if (channel == Channel.Reliable)
            {
                dataLength = (int)UnreliablePacketLength.ReliableOrBroadcast;
            }
            else
            {
                //If length is specified then read it.
                if (reader.Remaining >= 2)
                    dataLength = reader.ReadInt16();
                //Not enough data remaining to specify, purge data.
                else
                    dataLength = (int)UnreliablePacketLength.PurgeRemaiming;
            }

            //Link index isn't stored.
            if (!_rpcLinks.TryGetValue(index, out RpcLink link))
            {
                /* Like other reliable communications the object
                * should never be missing. */
                if (channel == Channel.Reliable)
                {
                    if (NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"RPCLink of Id {index} could not be found. The remaining packet has been purged.");
                    reader.Skip(reader.Remaining);
                }
                //If unreliable just purge data for object.
                else
                {
                    SkipDataLength((PacketId)index, reader, dataLength);
                }

                return;
            }

            //Found NetworkObject for link.
            if (Spawned.TryGetValue(link.ObjectId, out NetworkObject nob))
            {
                NetworkBehaviour nb = nob.NetworkBehaviours[link.ComponentIndex];
                if (link.RpcType == RpcType.Target)
                    nb.OnTargetRpc(link.RpcHash, reader, channel);
                else if (link.RpcType == RpcType.Observers)
                    nb.OnObserversRpc(link.RpcHash, reader, channel);
                else if (link.RpcType == RpcType.Reconcile)
                    nb.OnReconcileRpc(link.RpcHash, reader, channel);
            }
            //Could not find NetworkObject.
            else
            {
                //Reliable should never be out of order/missing.
                if (channel == Channel.Reliable)
                {
                    if (NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"ObjectId {link.ObjectId} for RPCLink {index} could not be found.");
                }
                else
                {
                    SkipDataLength((PacketId)index, reader, dataLength);
                }
            }
        }

        /// <summary>
        /// Sets link to rpcLinks key linkIndex.
        /// </summary>
        /// <param name="linkIndex"></param>
        /// <param name="link"></param>
        internal void SetRpcLink(ushort linkIndex, RpcLink link)
        {
            _rpcLinks[linkIndex] = link;
        }

        /// <summary>
        /// Removes link index keys from rpcLinks.
        /// </summary>
        internal void RemoveLinkIndexes(List<ushort> values)
        {
            if (values == null)
                return;

            for (int i = 0; i < values.Count; i++)
                _rpcLinks.Remove(values[i]);
        }

    }

}