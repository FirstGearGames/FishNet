using FishNet.Object;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public sealed partial class ServerManager : MonoBehaviour
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
            /* Brute force enum values. 
             * Linq Last/Max lookup throws for IL2CPP. */
            ushort highestValue = 0;
            Array pidValues = Enum.GetValues(typeof(PacketId));
            foreach (PacketId pid in pidValues)
                highestValue = Math.Max(highestValue, (ushort)pid);

            highestValue += 1;
            for (ushort i = highestValue; i < ushort.MaxValue; i++)
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