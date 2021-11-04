using FishNet.Object;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public partial class ServerManager : MonoBehaviour
    {


        #region Internal
        /// <summary>
        /// Current RPCLinks.
        /// </summary>
        internal Dictionary<ushort, RpcLink> RpcLinks = new Dictionary<ushort, RpcLink>();
        /// <summary>
        /// RPCLink indexes which can be used.
        /// </summary>
        private Stack<ushort> _availableRpcLinkIndexes = new Stack<ushort>();
        #endregion

        /// <summary>
        /// Initializes RPC Links for NetworkBehaviours.
        /// </summary>
        private void InitializeRpcLinks()
        {
            ushort startingLinkIndex = (ushort)(1 + (ushort)Enum.GetValues(typeof(PacketId)).Cast<PacketId>().Max());
            for (ushort i = startingLinkIndex; i < ushort.MaxValue; i++)
                _availableRpcLinkIndexes.Push(i);
        }

        /// <summary>
        /// Sets the next RPC Link to use.
        /// </summary>
        /// <returns>True if a link was available and set.</returns>
        internal bool GetRpcLink(out ushort value)
        {
            if (_availableRpcLinkIndexes.Count > 0)
            {
                value = _availableRpcLinkIndexes.Pop();
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// Sets data to RpcLinks for linkIndex.
        /// </summary>
        internal void SetRpcLink(ushort linkIndex, RpcLink data)
        {
            RpcLinks[linkIndex] = data;
        }

        /// <summary>
        /// Returns RPCLinks to availableRpcLinkIndexes.
        /// </summary>
        internal void ReturnRpcLinks()
        {

        }
    }

}