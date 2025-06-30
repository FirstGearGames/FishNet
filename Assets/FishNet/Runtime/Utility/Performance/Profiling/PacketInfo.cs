using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Utility.Performance.Profiling
{
    [System.Serializable]
    public class PacketInfo
    {
        /// <summary>
        /// Order message was sent/received in frame
        /// </summary>
        [SerializeField] private int _order;
        [SerializeField] private int _length;
        [SerializeField] private int _count;
        [SerializeField] private int _packetId;
        [SerializeField] private string _objectName;
        [SerializeField] private string _rpcName;

        public int Order => _order;
        public int Bytes => _length;
        public int Count => _count;
        public int TotalBytes => Bytes * Count;
        public string ObjectName => _objectName;
        public string PacketIdName => ((PacketId)_packetId).ToString();
        public string RpcName => _rpcName;

        public PacketInfo(IPacketInfoProvider provider, string objectName, PacketId packetId, int length, int count, int order)
        {
            _order = order;
            _length = length;
            _count = count;
            _packetId = (int)packetId;
            _objectName = objectName;
            // var obj = provider.GetNetworkIdentity(id);
            // _objectName = obj != null ? obj.name : null;
            // _rpcName = provider.GetRpcName(msg);
        }
    }
}
