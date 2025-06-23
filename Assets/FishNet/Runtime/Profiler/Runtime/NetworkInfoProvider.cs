//using Mirage.RemoteCalls;

//namespace Mirage.NetworkProfiler
//{
//    /// <summary>
//    /// Returns information about NetworkMessage
//    /// </summary>
//    public interface INetworkInfoProvider
//    {
//        uint? GetNetId(NetworkDiagnostics.MessageInfo info);
//        NetworkIdentity GetNetworkIdentity(uint? netId);
//        string GetRpcName(NetworkDiagnostics.MessageInfo info);
//    }

//    public class NetworkInfoProvider : INetworkInfoProvider
//    {
//        private readonly NetworkWorld _world;

//        public NetworkInfoProvider(NetworkWorld world)
//        {
//            _world = world;
//        }

//        public uint? GetNetId(NetworkDiagnostics.MessageInfo info)
//        {
//            switch (info.message)
//            {
//                case RpcMessage msg: return msg.NetId;
//                case RpcWithReplyMessage msg: return msg.NetId;
//                case SpawnMessage msg: return msg.NetId;
//                case RemoveAuthorityMessage msg: return msg.NetId;
//                case ObjectDestroyMessage msg: return msg.NetId;
//                case ObjectHideMessage msg: return msg.NetId;
//                case UpdateVarsMessage msg: return msg.NetId;
//                default: return default;
//            }
//        }

//        public NetworkIdentity GetNetworkIdentity(uint? netId)
//        {
//            if (!netId.HasValue)
//                return null;

//            if (_world == null)
//                return null;

//            return _world.TryGetIdentity(netId.Value, out var identity)
//                ? identity
//                : null;
//        }

//        public string GetRpcName(NetworkDiagnostics.MessageInfo info)
//        {
//            switch (info.message)
//            {
//                case RpcMessage msg:
//                    return GetRpcName(msg.NetId, msg.FunctionIndex);
//                case RpcWithReplyMessage msg:
//                    return GetRpcName(msg.NetId, msg.FunctionIndex);
//                default: return string.Empty;
//            }
//        }

//        private string GetRpcName(uint netId, int functionIndex)
//        {
//            var identity = GetNetworkIdentity(netId);
//            if (identity == null)
//                return string.Empty;

//            var rpc = identity.RemoteCallCollection.GetAbsolute(functionIndex);
//            return rpc.Name;
//        }
//    }
//}
