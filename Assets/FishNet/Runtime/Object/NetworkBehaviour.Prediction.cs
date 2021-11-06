using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Object.Prediction.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object
{


    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Registered ServerRpc methods.
        /// </summary>
        private readonly Dictionary<uint, ReplicateDelegate> _replicateDelegates = new Dictionary<uint, ReplicateDelegate>();
        /// <summary>
        /// Registered ObserversRpc methods.
        /// </summary>
        private readonly Dictionary<uint, ReconcileDelegate> _reconcileDelegates = new Dictionary<uint, ReconcileDelegate>();
        #endregion

        /// <summary>
        /// Registers a RPC method.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude] //codegen this can be made protected internal then set public via codegen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterReplicate(uint hash, ReplicateDelegate del)
        {
            _replicateDelegates[hash] = del;
        }
        /// <summary>
        /// Registers a RPC method.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude] //codegen this can be made protected internal then set public via codegen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterReconcile(uint hash, ReconcileDelegate del)
        {
            _reconcileDelegates[hash] = del;
        }

        /// <summary>
        /// Called when a ServerRpc is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnReplicate(PooledReader reader, NetworkConnection sendingClient, Channel channel)
        {
            uint methodHash = reader.ReadByte();

            if (sendingClient == null)
            {
                if (NetworkObject.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"NetworkConnection is null. ServerRpc {methodHash} will not complete. Remainder of packet may become corrupt.");
                return;
            }

            if (_replicateDelegates.TryGetValue(methodHash, out ReplicateDelegate data))
            {
                data.Invoke(this, reader, channel, sendingClient);
            }
            else
            {
                if (NetworkObject.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Replicate not found for hash {methodHash}. Remainder of packet may become corrupt.");
            }
        }

        /// <summary>
        /// Called when an TargetRpc is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnReconcile(uint? hash, PooledReader reader, Channel channel)
        {
            if (hash == null)
                hash = reader.ReadByte();

            if (_reconcileDelegates.TryGetValue(hash.Value, out ReconcileDelegate del))
            {
                del.Invoke(this, reader, channel);
            }
            else
            {
                if (NetworkObject.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Reconcile not found for hash {hash.Value}. Remainder of packet may become corrupt.");
            }
        }

        /// <summary>
        /// Sends a RPC to server.
        /// Internal use.
        /// </summary>
        /// <param name="rpcHash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        [APIExclude] //codegen this can be made internal then set public via codegen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendReplicate(uint rpcHash, PooledWriter methodWriter)
        {
            if (!IsSpawnedWithWarning())
                return;

            PooledWriter writer = CreateReplicate(rpcHash, methodWriter);
            NetworkObject.NetworkManager.TransportManager.SendToServer((byte)Channel.Unreliable, writer.GetArraySegment());
            writer.Dispose();
        }

        /// <summary>
        /// Sends a RPC to target.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="methodWriter"></param>
        /// <param name="channel"></param>
        /// <param name="connection"></param>
        [APIExclude] //codegen this can be made internal then set public via codegen
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendReconcile(uint hash, PooledWriter methodWriter, NetworkConnection connection)
        {
            if (!IsSpawnedWithWarning())
                return;

            if (connection == null)
            {
                if (NetworkObject.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Action cannot be completed as no connection is specified.");
                return;
            }
            else
            {
                /* If not using observers, sending to owner,
                 * or observers contains target. */
                //bool canSendTotarget = (!NetworkObject.UsingObservers ||
                //    NetworkObject.OwnerId == target.ClientId ||
                //    NetworkObject.Observers.Contains(target));
                bool canSendTotarget = NetworkObject.OwnerId == connection.ClientId || NetworkObject.Observers.Contains(connection);

                if (!canSendTotarget)
                {
                    if (NetworkObject.NetworkManager.CanLog(LoggingType.Warning))
                        Debug.LogWarning($"Action cannot be completed as connectionId {connection.ClientId} is not an observer for object {gameObject.name}");
                    return;
                }
            }

            PooledWriter writer = CreateReconcile(hash, methodWriter);
            NetworkObject.NetworkManager.TransportManager.SendToClient((byte)Channel.Unreliable, writer.GetArraySegment(), connection);
            writer.Dispose();
        }


        /// <summary>
        /// Writes a replication and returns the writer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PooledWriter CreateReplicate(uint hash, PooledWriter methodWriter)
        {
            //Writer containing full packet.
            PooledWriter writer = WriterPool.GetWriter();
            writer.WritePacketId(PacketId.Reconcile);
            writer.WriteNetworkBehaviour(this);
            //Write packet length. The +1 is for hash.
            WriteUnreliableLength(writer, methodWriter.Length + 1);
            writer.WriteByte((byte)hash);
            writer.WriteArraySegment(methodWriter.GetArraySegment());
            return writer;
        }

        /// <summary>
        /// Writes a reconcile and returns the writer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PooledWriter CreateReconcile(uint hash, PooledWriter methodWriter)
        {
            //Writer containing full packet.
            PooledWriter writer = WriterPool.GetWriter();
            writer.WritePacketId(PacketId.Reconcile);
            writer.WriteNetworkBehaviour(this);
            //Write packet length. The +1 is for hash.
            WriteUnreliableLength(writer, methodWriter.Length + 1);
            writer.WriteByte((byte)hash);
            writer.WriteArraySegment(methodWriter.GetArraySegment());
            return writer;
        }
    }


}