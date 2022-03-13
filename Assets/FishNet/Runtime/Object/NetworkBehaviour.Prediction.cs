using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Object.Prediction.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using FishNet.Utility.Extension;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
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
        private bool _predictionInitialized;
        /// <summary>
        /// Rigidbody found on this object. This is used for prediction.
        /// </summary>
        private Rigidbody _predictionRigidbody;
        /// <summary>
        /// Rigidbody2D found on this object. This is used for prediction.
        /// </summary>
        private Rigidbody2D _predictionRigidbody2d;
        /// <summary>
        /// Last position for TransformMayChange.
        /// </summary>
        private Vector3 _lastMayChangePosition;
        /// <summary>
        /// Last rotation for TransformMayChange.
        /// </summary>
        private Quaternion _lastMayChangeRotation;
        /// <summary>
        /// Last scale for TransformMayChange.
        /// </summary>
        private Vector3 _lastMayChangeScale;
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
                if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"NetworkConnection is null. Replicate {methodHash.Value} on {gameObject.name}, behaviour {GetType().Name} will not complete. Remainder of packet may become corrupt.");
                return;
            }

            if (_replicateRpcDelegates.TryGetValueIL2CPP(methodHash.Value, out ReplicateRpcDelegate del))
            {
                del.Invoke(this, reader, sendingClient);
            }
            else
            {
                if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Replicate not found for hash {methodHash.Value} on {gameObject.name}, behaviour {GetType().Name}. Remainder of packet may become corrupt.");
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

            if (_reconcileRpcDelegates.TryGetValueIL2CPP(methodHash.Value, out ReconcileRpcDelegate del))
            {
                del.Invoke(this, reader);
            }
            else
            {
                if (_networkObjectCache.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Reconcile not found for hash {methodHash.Value}. Remainder of packet may become corrupt.");
            }
        }

        /// <summary>
        /// Clears cached replicates. This can be useful to call on server and client after teleporting.
        /// </summary>
        /// <param name="asServer">True to reset values for server, false to reset values for client.</param>
        public void ClearReplicateCache(bool asServer) { InternalClearReplicateCache(asServer); }
        /// <summary>
        /// Clears cached replicates.
        /// For internal use only.
        /// </summary>
        /// <param name="asServer"></param>
        [APIExclude]
        protected internal virtual void InternalClearReplicateCache(bool asServer) { }

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
            //if (_rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
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
            if (!IsSpawned)
                return;
            if (!Owner.IsActive)
                return;

            PooledWriter methodWriter = WriterPool.GetWriter();
            methodWriter.Write(reconcileData);

            PooledWriter writer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (NetworkManager.DebugManager.ReconcileRpcLinks && _rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#else
            if (_rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
#endif
                writer = CreateLinkedRpc(link, methodWriter, channel);
            else
                writer = CreateRpc(hash, methodWriter, PacketId.Reconcile, channel);

            _networkObjectCache.NetworkManager.TransportManager.SendToClient((byte)channel, writer.GetArraySegment(), Owner);

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
                _predictionRigidbody = GetComponentInParent<Rigidbody>();
                _predictionRigidbody2d = GetComponentInParent<Rigidbody2D>();
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
                (_predictionRigidbody != null && (_predictionRigidbody.velocity != Vector3.zero || _predictionRigidbody.angularVelocity != Vector3.zero)) ||
                (_predictionRigidbody2d != null && (_predictionRigidbody2d.velocity != Vector2.zero || _predictionRigidbody2d.angularVelocity != 0f))
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