using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;

namespace FishNet.Utility.Performance.Profiling
{

    public static class PacketInfoProvider
    {
        // private readonly NetworkWorld _world;
        //
        // public PacketInfoProvider(NetworkWorld world)
        // {
        //     _world = world;
        // }
        //
        // public uint? GetNetId(NetworkDiagnostics.MessageInfo info)
        // {
        //     switch (info.message)
        //     {
        //         case RpcMessage msg: return msg.NetId;
        //         case RpcWithReplyMessage msg: return msg.NetId;
        //         case SpawnMessage msg: return msg.NetId;
        //         case RemoveAuthorityMessage msg: return msg.NetId;
        //         case ObjectDestroyMessage msg: return msg.NetId;
        //         case ObjectHideMessage msg: return msg.NetId;
        //         case UpdateVarsMessage msg: return msg.NetId;
        //         default: return default;
        //     }
        // }
        //
        // public NetworkIdentity GetNetworkIdentity(uint? netId)
        // {
        //     if (!netId.HasValue)
        //         return null;
        //
        //     if (_world == null)
        //         return null;
        //
        //     return _world.TryGetIdentity(netId.Value, out var identity)
        //         ? identity
        //         : null;
        // }
        //
        // public string GetRpcName(NetworkDiagnostics.MessageInfo info)
        // {
        //     switch (info.message)
        //     {
        //         case RpcMessage msg:
        //             return GetRpcName(msg.NetId, msg.FunctionIndex);
        //         case RpcWithReplyMessage msg:
        //             return GetRpcName(msg.NetId, msg.FunctionIndex);
        //         default: return string.Empty;
        //     }
        // }
        //
        // private string GetRpcName(uint netId, int functionIndex)
        // {
        //     var identity = GetNetworkIdentity(netId);
        //     if (identity == null)
        //         return string.Empty;
        //
        //     var rpc = identity.RemoteCallCollection.GetAbsolute(functionIndex);
        //     return rpc.Name;
        // }

        public static string GetPropertyName(NetworkBehaviour nb, int propertyHash, PacketId packetId)
        {
            string res = "";
            switch (packetId)
            {
                case PacketId.SyncObject:
                case PacketId.SyncVar:
                    int syncTypeCount = 0;
                    bool foundSyncType = false;
                    foreach (var fieldInfo in nb.GetType().GetFields())
                    {
                        foreach (var customAttribute in fieldInfo.CustomAttributes)
                        {
                            if (customAttribute.AttributeType.FullName == typeof(SyncVarAttribute).FullName ||
                                customAttribute.AttributeType.FullName == typeof(SyncObjectAttribute).FullName)
                            {
                                if (syncTypeCount == propertyHash)
                                {
                                    res = fieldInfo.Name;
                                    foundSyncType = true;
                                    break;
                                }
                                syncTypeCount++;
                            }
                        }

                        if (foundSyncType)
                        {
                            break;
                        }
                    }
                    break;
                case PacketId.Reconcile:
                case PacketId.ObserversRpc:
                case PacketId.TargetRpc:
                    int rpcCount = 0;
                    bool foundRpc = false;
                    // Iterate the replicates first, that's what the codegen does
                    foreach (var fieldInfo in nb.GetType().GetMethods())
                    {
                        foreach (var customAttribute in fieldInfo.CustomAttributes)
                        {
                            if (customAttribute.AttributeType.FullName == typeof(ReplicateAttribute).FullName)
                            {
                                rpcCount++;
                            }
                        }
                    }
                    foreach (var methodInfo in nb.GetType().GetMethods())
                    {
                        foreach (var customAttribute in methodInfo.CustomAttributes)
                        {
                            if (customAttribute.AttributeType.FullName == typeof(ObserversRpcAttribute).FullName ||
                                customAttribute.AttributeType.FullName == typeof(TargetRpcAttribute).FullName ||
                                customAttribute.AttributeType.FullName == typeof(ReconcileAttribute).FullName)
                            {
                                if (rpcCount == propertyHash)
                                {
                                    res = methodInfo.Name;
                                    foundRpc = true;
                                    break;
                                }
                                rpcCount++;
                            }
                        }

                        if (foundRpc)
                        {
                            break;
                        }
                    }
                    break;
            }

            return res;
        }
    }
}
