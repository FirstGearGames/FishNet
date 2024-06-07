using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Object.Helping;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
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
            //Link index isn't stored.
            if (!_rpcLinks.TryGetValueIL2CPP(index, out RpcLink link))
            {
                dataLength = Packets.GetPacketLength(ushort.MaxValue, reader, channel);
                SkipDataLength(index, reader, dataLength);
                return;
            }
            //Found NetworkObject for link.
            else if (Spawned.TryGetValueIL2CPP(link.ObjectId, out NetworkObject nob))
            {
                //Still call GetPacketLength to remove any extra bytes at the front of the reader.
                NetworkBehaviour nb = nob.NetworkBehaviours[link.ComponentIndex];
                if (link.RpcType == RpcType.Target)
                {
                    Packets.GetPacketLength((ushort)PacketId.TargetRpc, reader, channel);
                    nb.OnTargetRpc(link.RpcHash, reader, channel);
                }
                else if (link.RpcType == RpcType.Observers)
                {
                    Packets.GetPacketLength((ushort)PacketId.ObserversRpc, reader, channel);
                    nb.OnObserversRpc(link.RpcHash, reader, channel);
                }
                else if (link.RpcType == RpcType.Reconcile)
                {
                    Packets.GetPacketLength((ushort)PacketId.Reconcile, reader, channel);
                    nb.OnReconcileRpc(link.RpcHash, reader, channel);
                }
            }
            //Could not find NetworkObject.
            else
            {
                dataLength = Packets.GetPacketLength(index, reader, channel);
                SkipDataLength(index, reader, dataLength, link.ObjectId);
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