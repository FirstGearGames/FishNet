using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;

namespace FishNet.Utility.Performance.Profiling
{

    public static class PacketInfoProvider
    {
        private static Dictionary<Type, List<string>> SyncTypeNamesCache = new();
        private static Dictionary<Type, List<string>> RpcNamesCache = new();

        public static string GetPropertyName(NetworkBehaviour nb, int propertyHash, PacketId packetId)
        {
            string res = "";
            switch (packetId)
            {
                case PacketId.SyncObject:
                case PacketId.SyncVar:
                    if (SyncTypeNamesCache.TryGetValue(nb.GetType(), out var syncTypeNames))
                    {
                        if (propertyHash < syncTypeNames.Count)
                        {
                            res = syncTypeNames[propertyHash];
                        }
                    }
                    else
                    {
                        List<string> typeNamesList = new();
                        int syncTypeCount = 0;
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
                                    }
                                    syncTypeCount++;
                                    typeNamesList.Add(fieldInfo.Name);
                                }
                            }
                        }

                        SyncTypeNamesCache[nb.GetType()] = typeNamesList;
                    }
                    break;
                case PacketId.Reconcile:
                case PacketId.ObserversRpc:
                case PacketId.TargetRpc:
                    if (RpcNamesCache.TryGetValue(nb.GetType(), out var rpcNames))
                    {
                        if (propertyHash < rpcNames.Count)
                        {
                            res = rpcNames[propertyHash];
                        }
                    }
                    else
                    {
                        List<string> rpcNamesList = new();
                        int rpcCount = 0;
                        // Iterate the replicates first, that's what the codegen does
                        foreach (var methodInfo in nb.GetType().GetMethods())
                        {
                            foreach (var customAttribute in methodInfo.CustomAttributes)
                            {
                                if (customAttribute.AttributeType.FullName == typeof(ReplicateAttribute).FullName)
                                {
                                    rpcNamesList.Add(methodInfo.Name);
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
                                        break;
                                    }
                                    rpcCount++;
                                    rpcNamesList.Add(methodInfo.Name);
                                }
                            }
                        }

                        RpcNamesCache[nb.GetType()] = rpcNamesList;
                    }
                    break;
            }

            return res;
        }
    }
}
