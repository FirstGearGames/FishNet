#if UNITY_EDITOR || DEVELOPMENT_BUILD
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FishNet.Managing.Debugging
{

    internal class ParseLogger
    {
        /// <summary>
        /// Contains the last several non-split packets to arrive. This is used for debugging.
        /// </summary>
        private Queue<PacketId> _incomingPacketIds = new Queue<PacketId>();
        /// <summary>
        /// Maximum number of packets allowed to be queued.
        /// </summary>
        private const int PACKET_COUNT = 5;

        /// <summary>
        /// Resets data.
        /// </summary>
        internal void Reset()
        {
            _incomingPacketIds.Clear();
        }

        /// <summary>
        /// Adds a packet to data.
        /// </summary>
        /// <param name="pId"></param>
        internal void AddPacket(PacketId pId)
        {
            _incomingPacketIds.Enqueue(pId);
            if (_incomingPacketIds.Count > PACKET_COUNT)
                _incomingPacketIds.Dequeue();
        }

        /// <summary>
        /// Prints current data.
        /// </summary>
        internal void Print(NetworkManager nm)
        {
            if (nm == null)
                nm = InstanceFinder.NetworkManager;

            //Only log if a NM was found.
            if (nm != null)
            {
                StringBuilder sb = new StringBuilder();
                foreach (PacketId item in _incomingPacketIds)
                    sb.Insert(0, $"{item.ToString()}{Environment.NewLine}");

                NetworkObject lastNob = Reader.LastNetworkObject;
                string nobData = (lastNob == null) ? "Unset" : $"Id {lastNob.ObjectId} on gameObject {lastNob.name}";
                NetworkBehaviour lastNb = Reader.LastNetworkBehaviour;
                string nbData = (lastNb == null) ? "Unset" : lastNb.GetType().Name;

                nm.LogError($"The last {_incomingPacketIds.Count} packets to arrive are: {Environment.NewLine}{sb.ToString()}");
                nm.LogError($"The last parsed NetworkObject is {nobData}, and NetworkBehaviour {nbData}.");
            }

            Reset();
        }
    }

}
#endif