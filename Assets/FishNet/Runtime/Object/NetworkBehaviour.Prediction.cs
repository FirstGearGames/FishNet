using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Object.Prediction.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo(Constants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Registered Replicate methods.
        /// </summary>
        private readonly Dictionary<uint, ReplicateRpcDelegate> _replicateRpcDelegates = new Dictionary<uint, ReplicateRpcDelegate>();
        /// <summary>
        /// Registered Reconcile methods.
        /// </summary>
        private readonly Dictionary<uint, ReconcileRpcDelegate> _reconcileRpcDelegates = new Dictionary<uint, ReconcileRpcDelegate>();
        #endregion

        /// <summary>
        /// Registers a RPC method.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude] //codegen this can be made protected internal then set public via codegen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterReplicateRpc(uint hash, ReplicateRpcDelegate del)
        {
            _replicateRpcDelegates[hash] = del;
        }
        /// <summary>
        /// Registers a RPC method.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude] //codegen this can be made protected internal then set public via codegen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterReconcileRpc(uint hash, ReconcileRpcDelegate del)
        {
            _reconcileRpcDelegates[hash] = del;
        }


        /// <summary>
        /// Called when a replicate is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnReplicateRpc(uint? methodHash, PooledReader reader, NetworkConnection sendingClient, Channel channel)
        {
            if (methodHash == null)
                methodHash = ReadRpcHash(reader);

            if (sendingClient == null)
            {
                if (NetworkObject.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"NetworkConnection is null. Replicate {methodHash} will not complete. Remainder of packet may become corrupt.");
                return;
            }

            if (_replicateRpcDelegates.TryGetValue(methodHash.Value, out ReplicateRpcDelegate del))
            {
                del.Invoke(this, reader, sendingClient);
            }
            else
            {
                if (NetworkObject.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Replicate not found for hash {methodHash.Value}. Remainder of packet may become corrupt.");
            }
        }



        /// <summary>
        /// Called when a reconcile is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnReconcileRpc(uint? methodHash, PooledReader reader, Channel channel)
        {
            if (methodHash == null)
                methodHash = ReadRpcHash(reader);

            if (_reconcileRpcDelegates.TryGetValue(methodHash.Value, out ReconcileRpcDelegate del))
            {
                del.Invoke(this, reader);
            }
            else
            {
                if (NetworkObject.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Reconcile not found for hash {methodHash.Value}. Remainder of packet may become corrupt.");
            }
        }

        /// <summary>
        /// Writes number of past inputs from buffer to writer and sends it to the server.
        /// Internal use. 
        /// </summary> //codegen can be made internal, then public via codegen
        [APIExclude]
        public void SendReplicateRpc<T>(uint hash, List<T> replicateBuffer, int count)
        {
            if (!IsSpawnedWithWarning())
                return;

            int lastBufferIndex = (replicateBuffer.Count - 1);
            //Nothing to send; should never be possible.
            if (lastBufferIndex < 0)
                return;

            int bufferCount = replicateBuffer.Count;
            //Populate history into a new array. //todo fix GC
            count = Mathf.Min(bufferCount, count);

            T[] sent = new T[count];
            for (int i = 0; i < count; i++)
                sent[i] = replicateBuffer[(bufferCount - count) + i];

            Channel channel = Channel.Unreliable;
            //Write history to methodWriter.
            PooledWriter methodWriter = WriterPool.GetWriter();
            methodWriter.Write(sent);

            PooledWriter writer;
            //if (_rpcLinks.TryGetValue(hash, out RpcLinkType link))
            //writer = CreateLinkedRpc(link, methodWriter, Channel.Unreliable);
            //else //todo add support for -> server rpc links.
            writer = CreateRpc(hash, methodWriter, PacketId.Replicate, channel);
            NetworkManager.TransportManager.SendToServer((byte)channel, writer.GetArraySegment(), false);

            methodWriter.Dispose();
            writer.Dispose();
        }

        /// <summary>
        /// Sends a RPC to target.
        /// Internal use.
        /// </summary>
        [APIExclude] //codegen this can be made internal then set public via codegen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendReconcileRpc<T>(uint hash, T reconcileData)
        {
            if (!IsSpawnedWithWarning())
                return;
            if (!OwnerIsActive)
                return;

            Channel channel = Channel.Unreliable;
            PooledWriter methodWriter = WriterPool.GetWriter();
            methodWriter.Write(reconcileData);

            PooledWriter writer;
            //if (_rpcLinks.TryGetValue(hash, out RpcLinkType link))
                //writer = CreateLinkedRpc(link, methodWriter, Channel.Unreliable);
            //else
                writer = CreateRpc(hash, methodWriter, PacketId.Reconcile, channel);
            NetworkObject.NetworkManager.TransportManager.SendToClient((byte)channel, writer.GetArraySegment(), Owner);

            methodWriter.Dispose();
            writer.Dispose();
        }

    }


}