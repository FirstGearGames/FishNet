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
        /// <summary>
        /// True if initialized compnents for prediction.
        /// </summary>
        private bool _predictionInitialized = false;
        /// <summary>
        /// Rigidbody found on this object. This is used for prediction.
        /// </summary>
        private Rigidbody _rigidbody = null;
        /// <summary>
        /// Rigidbody2D found on this object. This is used for prediction.
        /// </summary>
        private Rigidbody2D _rigidbody2d = null;
        /// <summary>
        /// Last position for TransformMayChange.
        /// </summary>
        private Vector3 _lastMayChangePosition = Vector3.zero;
        /// <summary>
        /// Last rotation for TransformMayChange.
        /// </summary>
        private Quaternion _lastMayChangeRotation = Quaternion.identity;
        /// <summary>
        /// Last scale for TransformMayChange.
        /// </summary>
        private Vector3 _lastMayChangeScale = Vector3.zero;
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
            /* Where to start writing from. When passed
             * into the writer values from this offset
             * and forward will be written. */
            int offset = replicateBuffer.Count - count;
            if (offset < 0)
                offset = 0;

            Channel channel = Channel.Unreliable;
            //Write history to methodWriter.
            PooledWriter methodWriter = WriterPool.GetWriter();
            methodWriter.WriteToEnd(replicateBuffer, offset);

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
        public void SendReconcileRpc<T>(uint hash, T reconcileData, Channel channel)
        {
            if (!IsSpawnedWithWarning())
                return;
            if (!OwnerIsActive)
                return;

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

        /// <summary>
        /// Returns if there is a chance the transform may change after the tick.
        /// </summary>
        /// <returns></returns>
        protected internal bool TransformMayChange()
        {
            if (!_predictionInitialized)
            {
                _predictionInitialized = true;
                _rigidbody = GetComponentInParent<Rigidbody>();
                _rigidbody2d = GetComponentInParent<Rigidbody2D>();
            }

            /* Use distance when checking if changed because rigidbodies can twitch
             * or move an extremely small amount. These small moves are not worth
             * resending over because they often fix themselves each frame. */
            float changeDistance = 0.000004f;

            bool positionChanged = (transform.position - _lastMayChangePosition).sqrMagnitude > changeDistance;
            bool rotationChanged = (transform.rotation.eulerAngles - _lastMayChangeRotation.eulerAngles).sqrMagnitude > changeDistance;
            bool scaleChanged = (transform.localScale - _lastMayChangeScale).sqrMagnitude > changeDistance;
            bool transformChanged = (positionChanged || rotationChanged || scaleChanged);
            /* Returns true if transform.hasChanged, or if either
             * of the rigidbodies have velocity. */
            bool changed = (
                transformChanged ||
                (_rigidbody != null && (_rigidbody.velocity != Vector3.zero || _rigidbody.angularVelocity != Vector3.zero)) ||
                (_rigidbody2d != null && (_rigidbody2d.velocity != Vector2.zero || _rigidbody2d.angularVelocity != 0f))
                );

            //If transform changed update last values.
            if (transformChanged)
            {
                _lastMayChangePosition = transform.position;
                _lastMayChangeRotation = transform.rotation;
                _lastMayChangeScale = transform.localScale;
            }

            return changed;
        }
    }


}