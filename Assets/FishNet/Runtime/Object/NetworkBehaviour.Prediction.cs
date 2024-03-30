using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Predicting;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
using FishNet.Object.Prediction.Delegating;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object
{

#if PREDICTION_V2
    /* This class is placed in this file while it is in development. 
     * When everything is locked in the class will be integrated properly. */
    internal static class ReplicateTickFinder
    {
        public enum DataPlacementResult
        {
            /// <summary>
            /// Something went wrong; this should never be returned.
            /// </summary>
            Error,
            /// <summary>
            /// Tick was found on an index.
            /// </summary>
            Exact,
            /// <summary>
            /// Tick was not found because it is lower than any of the replicates.
            /// This is also used when there are no datas.
            /// </summary>
            InsertBeginning,
            /// <summary>
            /// Tick was not found but can be inserted in the middle of the collection.
            /// </summary>
            InsertMiddle,
            /// <summary>
            /// Tick was not found because it is larger than any of the replicates.
            /// </summary>
            InsertEnd,
        }

        /// <summary>
        /// Gets the index in replicates where the tick matches.
        /// </summary>
        public static int GetReplicateHistoryIndex<T>(uint tick, List<T> replicatesHistory, out DataPlacementResult findResult) where T : IReplicateData
        {
            int replicatesCount = replicatesHistory.Count;
            if (replicatesCount == 0)
            {
                findResult = DataPlacementResult.InsertBeginning;
                return 0;
            }

            uint firstTick = replicatesHistory[0].GetTick();

            //Try to find by skipping ahead the difference between tick and start.
            int diff = (int)(tick - firstTick);
            /* If the difference is larger than replicatesCount
             * then that means the replicates collection is missing
             * entries. EG if replicates values were 4, 7, 10 and tick were
             * 10 the difference would be 6. While replicates does contain the value
             * there is no way it could be found by pulling index 'diff' since that
             * would be out of bounds. This should never happen under normal conditions, return
             * missing if it does. */
            //Do not need to check less than 0 since we know if here tick is larger than first entry.
            if (diff >= replicatesCount)
            {
                //Try to return value using brute force.
                int index = FindIndexBruteForce(out findResult);
                return index;
            }
            else if (diff < 0)
            {
                findResult = DataPlacementResult.InsertBeginning;
                return 0;
            }
            else
            {
                /* If replicatesHistory contained the ticks
                 * of 1 2 3 4 5, and the tick is 3, then the difference
                 * would be 2 (because 3 - 1 = 2). As we can see index
                 * 2 of replicatesHistory does indeed return the proper tick. */
                //Expected diff to be result but was not.
                if (replicatesHistory[diff].GetTick() != tick)
                {
                    //Try to return value using brute force.
                    int index = FindIndexBruteForce(out findResult);
                    return index;
                }
                //Exact was found, this is the most ideal situation.
                else
                {
                    findResult = DataPlacementResult.Exact;
                    return diff;
                }
            }

            //Tries to find the index by brute forcing the collection.
            int FindIndexBruteForce(out DataPlacementResult result)
            {
                /* Some quick exits to save perf. */
                //If tick is lower than first then it must be inserted at the beginning.
                if (tick < firstTick)
                {
                    result = DataPlacementResult.InsertBeginning;
                    return 0;
                }
                //If tick is larger the last then it must be inserted at the end.
                else if (tick > replicatesHistory[replicatesCount - 1].GetTick())
                {
                    result = DataPlacementResult.InsertEnd;
                    return replicatesCount;
                }
                else
                {
                    //Brute check.
                    for (int i = 0; i < replicatesCount; i++)
                    {
                        uint lTick = replicatesHistory[i].GetTick();
                        //Exact match found.
                        if (lTick == tick)
                        {
                            result = DataPlacementResult.Exact;
                            return i;
                        }
                        /* The checked data is greater than
                         * what was being searched. This means
                         * to insert right before it. */
                        else if (lTick > tick)
                        {
                            result = DataPlacementResult.InsertMiddle;
                            return i;
                        }
                    }

                    //Should be impossible to get here.
                    result = DataPlacementResult.Error;
                    return -1;
                }
            }

        }

    }
#endif

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Public.
        //#if PREDICTION_V2
        //        /// <summary>
        //        /// True if this Networkbehaviour implements prediction methods.
        //        /// </summary>
        //        [APIExclude]
        //        [MakePublic]
        //        protected internal bool UsesPrediction;
        //#endif
        /// <summary>
        /// True if the client has cached reconcile 
        /// </summary>
        internal bool ClientHasReconcileData;
#if !PREDICTION_V2
        /// <summary>
        /// Gets the last tick this NetworkBehaviour reconciled with.
        /// </summary>
        public uint GetLastReconcileTick() => _lastReconcileTick;
        /// <summary>
        /// Sets the last tick this NetworkBehaviour reconciled with.
        /// </summary>
        internal void SetLastReconcileTick(uint value, bool updateGlobals = true)
        {
            _lastReconcileTick = value;
            if (updateGlobals)
                PredictionManager.LastReconcileTick = value;
        }
        /// <summary>
        /// 
        /// </summary>
        private uint _lastReplicateTick;
        /// <summary>
        /// Gets the last tick this NetworkBehaviour replicated with.
        /// </summary>
        public uint GetLastReplicateTick() => _lastReplicateTick;
        /// <summary>
        /// Sets the last tick this NetworkBehaviour replicated with.
        /// For internal use only.
        /// </summary>
        private void SetLastReplicateTick(uint value, bool updateGlobals = true)
        {
            _lastReplicateTick = value;
            if (updateGlobals)
            {
                Owner.LocalReplicateTick = TimeManager.LocalTick;
                PredictionManager.LastReplicateTick = value;
            }
        }
#endif
        /// <summary>
        /// True if this NetworkBehaviour is reconciling.
        /// If this NetworkBehaviour does not implemnent prediction methods this value will always be false.
        /// Value will be false if there is no data to reconcile to, even if the PredictionManager IsReconciling.
        /// Data may be missing if it were intentionally not sent, or due to packet loss.
        /// </summary>
        public bool IsBehaviourReconciling { get; internal set; }
        #endregion

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
        /// <summary>
        /// Number of resends which may occur. This could be for client resending replicates to the server or the server resending reconciles to the client.
        /// </summary>
        private int _remainingResends;
        /// <summary>
        /// Last replicate tick read from remote. This can be the server reading a client or the other way around.
        /// </summary>
        private uint _lastReplicateReadRemoteTick;
#if !PREDICTION_V2
        /// <summary>
        /// Last sent replicate by owning client or server to non-owners.
        /// </summary>
        private uint _lastSentReplicateTick;
        /// <summary>
        /// Last enqueued replicate tick on the server.
        /// </summary> 
        private uint _lastReceivedReplicateTick;
        /// <summary>
        /// Last tick of a reconcile received from the server.
        /// </summary>
        private uint _lastReceivedReconcileTick;
#else
        /// <summary>
        /// Tick when replicates should begun to run. This is set and used when inputs are just received and need to queue to create a buffer.
        /// </summary>
        private uint _replicateStartTick = TimeManager.UNSET_TICK;
        /// <summary>
        /// Last tick iterated during a replicate replay.
        /// </summary>
        private uint _lastReplicatedTick;
        /// <summary>
        /// Last tick read for a reconcile.
        /// </summary>
        private uint _lastReadReconcileTick;
        /// <summary>
        /// Last tick read for a replicate.
        /// </summary>
        private uint _lastReadReplicateTick;
#endif
#if !PREDICTION_V2
        /// <summary>
        /// Last tick a reconcile occured.
        /// </summary>
        private uint _lastReconcileTick;
#endif
        #endregion

        /// <summary>
        /// Registers a RPC method.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [MakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RegisterReplicateRpc(uint hash, ReplicateRpcDelegate del)
        {
            _replicateRpcDelegates[hash] = del;
        }
        /// <summary>
        /// Registers a RPC method.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [MakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RegisterReconcileRpc(uint hash, ReconcileRpcDelegate del)
        {
            _reconcileRpcDelegates[hash] = del;
        }

#if !PREDICTION_V2
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
                _networkObjectCache.NetworkManager.LogError($"NetworkConnection is null. Replicate {methodHash.Value} on {gameObject.name}, behaviour {GetType().Name} will not complete. Remainder of packet may become corrupt.");
                return;
            }

            if (_replicateRpcDelegates.TryGetValueIL2CPP(methodHash.Value, out ReplicateRpcDelegate del))
                del.Invoke(reader, sendingClient, channel);
            else
                _networkObjectCache.NetworkManager.LogWarning($"Replicate not found for hash {methodHash.Value} on {gameObject.name}, behaviour {GetType().Name}. Remainder of packet may become corrupt.");
        }
#else
        /// <summary>
        /// Called when a replicate is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnReplicateRpc(uint? methodHash, PooledReader reader, NetworkConnection sendingClient, Channel channel)
        {
            if (methodHash == null)
                methodHash = ReadRpcHash(reader);

            if (_replicateRpcDelegates.TryGetValueIL2CPP(methodHash.Value, out ReplicateRpcDelegate del))
                del.Invoke(reader, sendingClient, channel);
            else
                _networkObjectCache.NetworkManager.LogWarning($"Replicate not found for hash {methodHash.Value} on {gameObject.name}, behaviour {GetType().Name}. Remainder of packet may become corrupt.");
        }
#endif
        /// <summary>
        /// Called when a reconcile is received.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnReconcileRpc(uint? methodHash, PooledReader reader, Channel channel)
        {
            if (methodHash == null)
                methodHash = ReadRpcHash(reader);

            if (_reconcileRpcDelegates.TryGetValueIL2CPP(methodHash.Value, out ReconcileRpcDelegate del))
                del.Invoke(reader, channel);
            else
                _networkObjectCache.NetworkManager.LogWarning($"Reconcile not found for hash {methodHash.Value}. Remainder of packet may become corrupt.");
        }

#if !PREDICTION_V2
        /// <summary>
        /// Clears cached replicates. This can be useful to call on server and client after teleporting.
        /// </summary>
        /// <param name="asServer">True to reset values for server, false to reset values for client.</param>
        public void ClearReplicateCache(bool asServer)
        {
            ResetLastPredictionTicks();
            ClearReplicateCache_Virtual(asServer);
        }
        /// <summary>
        /// Clears cached replicates for server and client. This can be useful to call on server and client after teleporting.
        /// </summary>
        public void ClearReplicateCache()
        {
            ResetLastPredictionTicks();
            ClearReplicateCache_Virtual(true);
            ClearReplicateCache_Virtual(false);
        }
        /// <summary>
        /// Clears cached replicates.
        /// For internal use only.
        /// </summary>
        /// <param name="asServer"></param>
        [MakePublic]
        internal virtual void ClearReplicateCache_Virtual(bool asServer) { }
#else
        /// <summary>
        /// Resets cached ticks used by prediction, such as last read and replicate tick.
        /// This is generally used when the ticks will be different then what was previously used; eg: when ownership changes.
        /// </summary>
        internal void ResetPredictionTicks()
        {
            _lastReplicateReadRemoteTick = TimeManager.UNSET_TICK;
            _lastReadReconcileTick = TimeManager.UNSET_TICK;
            _lastReadReplicateTick = TimeManager.UNSET_TICK;
        }
        /// <summary>
        /// Clears cached replicates for server and client. This can be useful to call on server and client after teleporting.
        /// </summary>
        public virtual void ClearReplicateCache() { }

        /// <summary>
        /// Clears cached replicates and histories.
        /// </summary>
        [MakePublic]
        [APIExclude]
        protected internal void ClearReplicateCache_Internal<T>(BasicQueue<T> replicatesQueue, List<T> replicatesHistory) where T : IReplicateData
        {
            while (replicatesQueue.Count > 0)
            {
                T data = replicatesQueue.Dequeue();
                data.Dispose();
            }

            for (int i = 0; i < replicatesHistory.Count; i++)
                replicatesHistory[i].Dispose();
            replicatesHistory.Clear();
        }
#endif
#if !PREDICTION_V2
        /// <summary>
        /// Resets last predirection tick values.
        /// </summary>
        private void ResetLastPredictionTicks()
        {
            _lastSentReplicateTick = 0;
            _lastReceivedReplicateTick = 0;
            _lastReceivedReconcileTick = 0;
            SetLastReconcileTick(0, false);
            SetLastReplicateTick(0, false);
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Writes number of past inputs from buffer to writer and sends it to the server.
        /// Internal use. 
        /// </summary>
        private void Owner_SendReplicateRpc<T>(uint hash, List<T> replicates, Channel channel) where T : IReplicateData
        {
            if (!IsSpawnedWithWarning())
                return;

            int bufferCount = replicates.Count;
            int lastBufferIndex = (bufferCount - 1);
            //Nothing to send; should never be possible.
            if (lastBufferIndex < 0)
                return;

            //Number of past inputs to send.
            int pastInputs = Mathf.Min(PredictionManager.RedundancyCount, bufferCount);
            /* Where to start writing from. When passed
			 * into the writer values from this offset
			 * and forward will be written. */
            int offset = bufferCount - pastInputs;
            if (offset < 0)
                offset = 0;

            uint lastReplicateTick = _lastSentReplicateTick;
            if (lastReplicateTick > 0)
            {
                uint diff = TimeManager.LocalTick - GetLastReplicateTick();
                offset += (int)diff - 1;
                if (offset >= replicates.Count)
                    return;
            }

            _lastSentReplicateTick = TimeManager.LocalTick;

            //Write history to methodWriter.
            PooledWriter methodWriter = WriterPool.Retrieve(WriterPool.LENGTH_BRACKET);
            methodWriter.WriteReplicate<T>(replicates, offset);
            PooledWriter writer;
            //if (_rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
            //writer = CreateLinkedRpc(link, methodWriter, Channel.Unreliable);
            //else //todo add support for -> server rpc links.

            _transportManagerCache.CheckSetReliableChannel(methodWriter.Length + MAXIMUM_RPC_HEADER_SIZE, ref channel);
            writer = CreateRpc(hash, methodWriter, PacketId.Replicate, channel);
            NetworkManager.TransportManager.SendToServer((byte)channel, writer.GetArraySegment(), false);

            /* If being sent as reliable then clear buffer
			 * since we know it will get there. 
			 * Also reset remaining resends. */
            if (channel == Channel.Reliable)
            {
                replicates.Clear();
                _remainingResends = 0;
            }

            methodWriter.StoreLength();
            writer.StoreLength();
        }
#endif


#if !PREDICTION_V2
        /// <summary>
        /// Sends a RPC to target.
        /// Internal use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Server_SendReconcileRpc<T>(uint hash, T reconcileData, Channel channel)
        {
            if (!IsSpawned)
                return;
            if (!Owner.IsActive)
                return;

            PooledWriter methodWriter = WriterPool.Retrieve();
            methodWriter.WriteUInt32(GetLastReplicateTick());
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

            methodWriter.Store();
            writer.Store();
        }
#else
        /// <summary>
        /// Sends a RPC to target.
        /// Internal use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [MakePublic]
        [APIExclude]
        protected internal void Server_SendReconcileRpc<T>(uint hash, T reconcileData, Channel channel)
        {
            if (!IsSpawned)
                return;

            //No owner and no state forwarding, nothing to do.
            bool stateForwarding = _networkObjectCache.EnableStateForwarding;
            if (!Owner.IsValid && !stateForwarding)
                return;

            PooledWriter methodWriter = WriterPool.Retrieve();
            /* Tick does not need to be written because it will always
            * be the localTick of the server. For the clients, this will
            * be the LastRemoteTick of the packet. 
            *
            * The exception is for the owner, which we send the last replicate
            * tick so the owner knows which to roll back to. */
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

            //If state forwarding is not enabled then only send to owner.
            if (!stateForwarding)
            {
                Owner.WriteState(writer);
            }
            //State forwarding, send to all.
            else
            {
                foreach (NetworkConnection nc in Observers)
                    nc.WriteState(writer);
            }
            methodWriter.Store();
            writer.Store();
        }
#endif
#if !PREDICTION_V2
        /// <summary> 
        /// Returns if there is a chance the transform may change after the tick.
        /// </summary>
        /// <returns></returns>
        protected internal bool PredictedTransformMayChange()
        {
            if (TimeManager.PhysicsMode == PhysicsMode.Disabled)
                return false;

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
#else
        /// <summary> 
        /// Returns if there is a chance the transform may change after the tick.
        /// </summary>
        /// <returns></returns>
        protected internal bool PredictedTransformMayChange()
        {
            if (TimeManager.PhysicsMode == PhysicsMode.Disabled)
                return false;

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
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Checks conditions for a replicate.
        /// </summary>
        /// <param name="asServer">True if checking as server.</param>
        /// <returns>Returns true if to exit the replicate early.</returns>
        [MakePublic] //internal
        internal bool Replicate_ExitEarly_A(bool asServer, bool replaying, bool allowServerControl)
        {
            bool isOwner = IsOwner;
            //Server.
            if (asServer)
            {
                //No owner, do not try to replicate 'owner' input.
                if (!Owner.IsActive && !allowServerControl)
                {
                    ClearReplicateCache(true);
                    return true;
                }
                //Is client host, no need to use CSP; trust client.
                if (isOwner)
                {
                    ClearReplicateCache();
                    return true;
                }
            }
            //Client.
            else
            {
                //Server does not replay; this should never happen.
                if (replaying && IsServerStarted)
                    return true;
                //Spectators cannot replicate.
                if (!isOwner)
                {
                    ClearReplicateCache(false);
                    return true;
                }
            }

            //Checks pass.
            return false;
        }
#endif


#if PREDICTION_V2   
        /// <summary>
        /// Called internally when an input from localTick should be replayed.
        /// </summary>
        internal virtual void Replicate_Replay_Start(uint replayTick) { }
        /// <summary>
        /// Replays inputs from replicates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void Replicate_Replay<T>(uint replayTick, ReplicateUserLogicDelegate<T> del, List<T> replicatesHistory, Channel channel) where T : IReplicateData
        {
            //Reconcile data was not received so cannot replay.
            if (!ClientHasReconcileData)
                return;

            if (_networkObjectCache.IsOwner)
                Replicate_Replay_Authoritative<T>(replayTick, del, replicatesHistory, channel);
            else
                Replicate_Replay_NonAuthoritative<T>(replayTick, del, replicatesHistory, channel);
        }
        /// <summary>
        /// Replays an input for authoritative entity.
        /// </summary>
        protected internal void Replicate_Replay_Authoritative<T>(uint replayTick, ReplicateUserLogicDelegate<T> del, List<T> replicatesHistory, Channel channel) where T : IReplicateData
        {
            ReplicateTickFinder.DataPlacementResult findResult;
            int replicateIndex = ReplicateTickFinder.GetReplicateHistoryIndex<T>(replayTick, replicatesHistory, out findResult);

            T data;
            ReplicateState state;
            //If found then the replicate has been received by the server.
            if (findResult == ReplicateTickFinder.DataPlacementResult.Exact)
            {
                data = replicatesHistory[replicateIndex];
                state = ReplicateState.ReplayedCreated;

                del.Invoke(data, state, channel);
                _lastReplicatedTick = data.GetTick();
                _networkObjectCache.LastUnorderedReplicateTick = data.GetTick();
            }
        }
        /// <summary>
        /// Replays an input for non authoritative entity.
        /// </summary>
        protected internal void Replicate_Replay_NonAuthoritative<T>(uint replayTick, ReplicateUserLogicDelegate<T> del, List<T> replicatesHistory, Channel channel) where T : IReplicateData
        {
            ReplicateTickFinder.DataPlacementResult findResult;
            int replicateIndex = ReplicateTickFinder.GetReplicateHistoryIndex<T>(replayTick, replicatesHistory, out findResult);

            T data;
            ReplicateState state;
            //If found then the replicate has been received by the server.
            if (findResult == ReplicateTickFinder.DataPlacementResult.Exact)
            {
                data = replicatesHistory[replicateIndex];
                state = ReplicateState.ReplayedCreated;
            }
            //If not not found then it's being run as predicted.
            else
            {
                data = default;
                data.SetTick(replayTick);
                if (replicatesHistory.Count == 0 || replicatesHistory[^1].GetTick() < replayTick)
                    state = ReplicateState.ReplayedFuture;
                else
                    state = ReplicateState.ReplayedCreated;
            }

            del.Invoke(data, state, channel);
            if (_lastReplicatedTick < data.GetTick())
                _lastReplicatedTick = data.GetTick();
            _networkObjectCache.LastUnorderedReplicateTick = data.GetTick();
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Gets the next replicate in perform when server or non-owning client.
        /// </summary>
        [MakePublic] //internal
        internal void Replicate_NonOwner<T>(ReplicateUserLogicDelegate<T> del, BasicQueue<T> q, T serverControlData, bool allowServerControl, Channel channel) where T : IReplicateData
        {
            //If to allow server control make sure there is no owner.
            if (allowServerControl && !Owner.IsValid)
            {
                uint tick = TimeManager.LocalTick;
                serverControlData.SetTick(tick);
                SetLastReplicateTick(tick);
                del.Invoke(serverControlData, true, channel, false);
            }
            //Using client inputs.
            else
            {
                int count = q.Count;
                if (count > 0)
                {
                    ReplicateData(q.Dequeue());
                    count--;

                    PredictionManager pm = PredictionManager;
                    bool consumeExcess = !pm.DropExcessiveReplicates;
                    //Number of entries to leave in buffer when consuming.
                    int leaveInBuffer = (int)pm.QueuedInputs;
                    //Only consume if the queue count is over leaveInBuffer.
                    if (consumeExcess && count > leaveInBuffer)
                    {
                        byte maximumAllowedConsumes = pm.MaximumReplicateConsumeCount;
                        int maximumPossibleConsumes = (count - leaveInBuffer);
                        int consumeAmount = Mathf.Min(maximumAllowedConsumes, maximumPossibleConsumes);

                        for (int i = 0; i < consumeAmount; i++)
                            ReplicateData(q.Dequeue());
                    }

                    void ReplicateData(T data)
                    {
                        uint tick = data.GetTick();
                        SetLastReplicateTick(tick);
                        del.Invoke(data, true, channel, false);
                    }

                    _remainingResends = pm.RedundancyCount;
                }
                else
                {
                    del.Invoke(default, true, channel, false);
                }
            }
        }
#else
        /// <summary>
        /// Gets the next replicate in perform when server or non-owning client.
        /// </summary>
        /// </summary>
        [MakePublic]
        [APIExclude]
        protected internal void Replicate_NonAuthoritative<T>(ReplicateUserLogicDelegate<T> del, BasicQueue<T> replicatesQueue, List<T> replicatesHistory, Channel channel) where T : IReplicateData
        {
            bool ownerlessAndServer = (!Owner.IsValid && IsServerStarted);
            if (IsOwner || ownerlessAndServer)
                return;

            TimeManager tm = _networkObjectCache.TimeManager;
            uint localTick = tm.LocalTick;
            //Server is not initialized.
            if (!_networkObjectCache.IsServerInitialized)
            {
                uint tick;
                if (replicatesHistory.Count > 0)
                {
                    tick = replicatesHistory[^1].GetTick() + 1;
                }
                else
                {
                    if (_lastReplicatedTick != TimeManager.UNSET_TICK)
                        tick = _lastReplicatedTick++;
                    else
                        tick = _networkObjectCache.TimeManager.LastPacketTick.Value();
                }

                T data = default(T);
                data.SetTick(tick);
                ReplicateData(data, ReplicateState.CurrentFuture);
            }
            //If here then server is initialized.
            else
            {
                int count = replicatesQueue.Count;
                /* If count is 0 then data must be set default
                 * and as predicted. */
                if (count == 0)
                {
                    uint tick = (_lastReplicatedTick + 1);
                    T data = default(T);
                    data.SetTick(tick);
                    ReplicateData(data, ReplicateState.CurrentFuture);
                }
                //Not predicted, is user created.
                else
                {
                    //Check to unset start tick, which essentially voids it resulting in inputs being run immediately.
                    if (localTick >= _replicateStartTick)
                        _replicateStartTick = TimeManager.UNSET_TICK;
                    /* As said above, if start tick is unset then replicates
                     * can run. When still set that means the start condition has
                     * not been met yet. */
                    if (_replicateStartTick == TimeManager.UNSET_TICK)
                    {
                        ReplicateData(replicatesQueue.Dequeue(), ReplicateState.CurrentCreated);
                        count--;

                        PredictionManager pm = PredictionManager;
                        bool consumeExcess = (!pm.DropExcessiveReplicates || IsClientOnlyStarted);
                        //Allow 1 over expected before consuming.
                        int leaveInBuffer = 1;
                        //Only consume if the queue count is over leaveInBuffer.
                        if (consumeExcess && count > leaveInBuffer)
                        {
                            byte maximumAllowedConsumes = 1;
                            int maximumPossibleConsumes = (count - leaveInBuffer);
                            int consumeAmount = Mathf.Min(maximumAllowedConsumes, maximumPossibleConsumes);

                            for (int i = 0; i < consumeAmount; i++)
                                ReplicateData(replicatesQueue.Dequeue(), ReplicateState.CurrentCreated);
                        }
                    }
                }
            }

            void ReplicateData(T data, ReplicateState state)
            {
                uint dataTick = data.GetTick();
                _lastReplicatedTick = dataTick;
                if (state == ReplicateState.CurrentCreated)
                    _networkObjectCache.SetReplicateTick(dataTick, true);
                //Add to history.
                replicatesHistory.Add(data);
                del.Invoke(data, state, channel);
            }

        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Returns if a replicates data changed and updates resends as well data tick.
        /// </summary>
        /// <param name="enqueueData">True to enqueue data for replaying.</param>
        /// <returns>True if data has changed..</returns>
        [MakePublic] //internal
        internal void Replicate_Owner<T>(ReplicateUserLogicDelegate<T> del, uint methodHash, List<T> replicates, T data, Channel channel) where T : IReplicateData
        {
            //Only check to enque/send if not clientHost.
            if (!IsServerStarted)
            {
                Func<T, bool> isDefaultDel = PublicPropertyComparer<T>.IsDefault;
                if (isDefaultDel == null)
                {
                    NetworkManager.LogError($"ReplicateComparers not found for type {typeof(T).FullName}");
                    return;
                }

                //If there's no datas then reset last replicate send tick.
                if (replicates.Count == 0)
                    _lastSentReplicateTick = 0;

                PredictionManager pm = NetworkManager.PredictionManager;

                bool isDefault = isDefaultDel.Invoke(data);
                bool mayChange = PredictedTransformMayChange();
                bool resetResends = (pm.UsingRigidbodies || mayChange || !isDefault);
                /* If there is going to be a resend then enqueue data no matter what.
				 * Then ensures there are no data gaps for ticks. EG, input may
				 * look like this...
				 * Move - tick 0.
				 * Idle - tick 1.
				 * Move - tick 2.
				 * 
				 * If there were no 'using rigidbodies' then resetResends may be false.
				 * As result the queue would be filled like this...
				 * Move - tick 0.
				 * Move - tick 2.
				 * 
				 * The ticks are not sent per data, just once and incremented once per data.
				 * Due to this the results would actually be...
				 * Move - tick 0.
				 * Move - tick 1 (should be tick 2!).
				 * 
				 * But by including data if there will be resends the defaults will become added. */
                if (resetResends)
                    _remainingResends = pm.RedundancyCount;

                bool enqueueData = (_remainingResends > 0);
                if (enqueueData)
                {
                    /* Replicates will be limited to 1 second
					 * worth on the client. That means the client
					 * will only lose replays if they do not receive
					 * a response back from the server for over a second.
					 * When a client drops a replay it does not necessarily mean
					 * they will be out of synchronization, but rather they
					 * will not be able to reconcile that tick. */
                    /* Even though limit is 1 second only remove entries if over 2 seconds
					 * to prevent constant remove calls to the collection. */
                    int maximumReplicates = (TimeManager.TickRate * 2);
                    //If over then remove half the replicates.
                    if (replicates.Count >= maximumReplicates)
                    {
                        int removeCount = (maximumReplicates / 2);
                        //Dispose first.
                        for (int i = 0; i < removeCount; i++)
                            replicates[i].Dispose();
                        //Then remove.
                        replicates.RemoveRange(0, removeCount);
                    }

                    uint localTick = TimeManager.LocalTick;
                    //Update tick on the data to current.
                    data.SetTick(localTick);
                    //Add to collection.
                    replicates.Add(data);
                }

                //If theres resends left.
                if (_remainingResends > 0)
                {
                    _remainingResends--;
                    Owner_SendReplicateRpc<T>(methodHash, replicates, channel);
                    //Update last replicate tick.
                    SetLastReplicateTick(TimeManager.LocalTick);
                }
            }

            del.Invoke(data, false, channel, false);
        }
#else
        /// <summary>
        /// Returns if a replicates data changed and updates resends as well data tick.
        /// </summary>
        /// <param name="enqueueData">True to enqueue data for replaying.</param>
        /// <returns>True if data has changed..</returns>
        [MakePublic] //internal
        [APIExclude]
        protected internal void Replicate_Authoritative<T>(ReplicateUserLogicDelegate<T> del, uint methodHash, BasicQueue<T> replicatesQueue, List<T> replicatesHistory, T data, Channel channel) where T : IReplicateData
        {
            bool ownerlessAndServer = (!Owner.IsValid && IsServerStarted);
            if (!IsOwner && !ownerlessAndServer)
                return;

            Func<T, bool> isDefaultDel = PublicPropertyComparer<T>.IsDefault;
            if (isDefaultDel == null)
            {
                NetworkManager.LogError($"ReplicateComparers not found for type {typeof(T).FullName}");
                return;
            }

            PredictionManager pm = NetworkManager.PredictionManager;
            uint localTick = TimeManager.LocalTick;

            data.SetTick(localTick);
            replicatesHistory.Add(data);
            //Check to reset resends.
            bool isDefault = isDefaultDel.Invoke(data);
            bool mayChange = false;// PredictedTransformMayChange();
            bool resetResends = (mayChange || !isDefault);
            /* If remaining resends is more than 0 then that means
             * redundancy is still in effect. When redundancy is not
             * in effect then histories to send can be 1 for this iteration. */
            int pastInputs = (_remainingResends > 0) ? PredictionManager.RedundancyCount : 1;
            //pastInputs = PredictionManager.RedundancyCount;
            if (resetResends)
                _remainingResends = pm.RedundancyCount;

            bool sendData = (_remainingResends > 0);
            if (sendData)
            {
                int replicatesHistoryCount = replicatesHistory.Count;
                /* Remove the number of replicates which are over maximum.
				 * 
				 * The clientHost object must keep redundancy count
				 * to send past inputs to others.
				 * 
				 * Otherwise use maximum client replicates which will be a variable
				 * rate depending on tick rate. The value returned is several seconds
				 * worth of owner inputs to be able to replay during a reconcile. 
				 *
				 * Server does not reconcile os it only needs enough for redundancy.
				 */
                int maxCount = (IsServerStarted) ? pm.RedundancyCount : pm.MaximumClientReplicates;
                //Number to remove which is over max count.
                int removeCount = (replicatesHistoryCount - maxCount);
                //If there are any to remove.
                if (removeCount > 0)
                {
                    //Dispose first.
                    for (int i = 0; i < removeCount; i++)
                        replicatesHistory[i].Dispose();

                    //Then remove range.
                    replicatesHistory.RemoveRange(0, removeCount);
                }

                /* If not server then send to server.
				 * If server then send to clients. */
                bool toServer = !IsServerStarted;
                Replicate_SendAuthoritative(toServer, methodHash, pastInputs, replicatesHistory, localTick, channel);
                _remainingResends--;
            }

            //Update last replicate tick.
            _networkObjectCache.SetReplicateTick(data.GetTick(), true);
            //Owner always replicates with new data.
            del.Invoke(data, ReplicateState.CurrentCreated, channel);
            //TODO: dispose replicate datas from history on replays.            
        }
#endif

#if PREDICTION_V2
        /// <summary>
        /// Sends a Replicate to server or clients.
        /// </summary>
        private void Replicate_SendAuthoritative<T>(bool toServer, uint hash, int pastInputs, List<T> replicatesHistory, uint queuedTick, Channel channel) where T : IReplicateData
        {
            /* Do not use IsSpawnedWithWarning because the server
             * will still call this a tick or two as clientHost when
             * an owner disconnects. This comes from calling Replicate(default)
             * for the server-side processing in NetworkBehaviours. */
            if (!IsSpawned)
                return;

            int historyCount = replicatesHistory.Count;
            //Nothing to send; should never be possible.
            if (historyCount <= 0)
                return;

            //Number of past inputs to send.
            if (historyCount < pastInputs)
                pastInputs = historyCount;
            /* Where to start writing from. When passed
			 * into the writer values from this offset
			 * and forward will be written. 
			 * Always write up to past inputs. */
            int offset = (historyCount - pastInputs);

            //Write history to methodWriter.
            PooledWriter methodWriter = WriterPool.Retrieve(WriterPool.LENGTH_BRACKET);
            /* If going to clients from the server then
             * write the queueTick. */
            if (!toServer)
                methodWriter.WriteTickUnpacked(queuedTick);
            methodWriter.WriteReplicate<T>(replicatesHistory, offset, TimeManager.LocalTick);

            _transportManagerCache.CheckSetReliableChannel(methodWriter.Length + MAXIMUM_RPC_HEADER_SIZE, ref channel);
            PooledWriter writer = CreateRpc(hash, methodWriter, PacketId.Replicate, channel);

            /* toServer will never be true if clientHost.
             * When clientHost and here replicates will
             * always just send to clients, while
             * excluding clientHost. */
            if (toServer)
            {
                NetworkManager.TransportManager.SendToServer((byte)channel, writer.GetArraySegment(), false);
            }
            else
            {
                /* If going to clients from server, then only send
                 * if state forwarding is enabled. */
                if (_networkObjectCache.EnableStateForwarding)
                {
                    //Exclude owner and if clientHost, also localClient.
                    _networkConnectionCache.Clear();
                    _networkConnectionCache.Add(Owner);
                    if (IsClientStarted)
                        _networkConnectionCache.Add(ClientManager.Connection);

                    NetworkManager.TransportManager.SendToClients((byte)channel, writer.GetArraySegment(), Observers, _networkConnectionCache, false);
                }
            }

            /* If sending as reliable there is no reason
			 * to perform resends, so clear remaining resends. */
            if (channel == Channel.Reliable)
                _remainingResends = 0;

            methodWriter.StoreLength();
            writer.StoreLength();
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Reads a replicate the client.
        /// </summary>
        [MakePublic] //Internal.
        internal void Replicate_Reader<T>(PooledReader reader, NetworkConnection sender, T[] arrBuffer, BasicQueue<T> replicates, Channel channel) where T : IReplicateData
        {
            PredictionManager pm = PredictionManager;

            /* Data can be read even if owner is not valid because user
			 * may switch ownership on an object and recv a replicate from
			 * the previous owner. */
            int receivedReplicatesCount = reader.ReadReplicate<T>(ref arrBuffer, TimeManager.LastPacketTick.LastRemoteTick, out _);
            /* Replicate rpc readers relay to this method and
			 * do not have an owner check in the generated code. */
            if (!OwnerMatches(sender))
                return;

            if (receivedReplicatesCount > pm.RedundancyCount)
            {
                sender.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, $"Connection {sender.ToString()} sent too many past replicates. Connection will be kicked immediately.");
                return;
            }

            Replicate_HandleReceivedReplicate<T>(receivedReplicatesCount, arrBuffer, replicates, channel);
        }
#else        
        /// <summary>
        /// Reads a replicate the client.
        /// </summary>
        /// <param name="replicateDataOnly">Data from the reader which only applies to the replicate.</param>
        [MakePublic]
        internal void Replicate_Reader<T>(uint hash, PooledReader reader, NetworkConnection sender, ref T[] arrBuffer, BasicQueue<T> replicatesQueue, List<T> replicatesHistory, Channel channel) where T : IReplicateData
        {
            /* This will never be received on owner, except in the condition
             * the server is the owner and also a client. In such condition 
             * the method is exited after data is parsed. */
            PredictionManager pm = _networkObjectCache.PredictionManager;
            TimeManager tm = _networkObjectCache.TimeManager;
            bool fromServer = (reader.Source == Reader.DataSource.Server);

            uint tick;
            /* If coming from the server then read the tick. Server sends tick
             * if authority or if relaying from another client. The tick which
             * arrives will be the tick the replicate will run on the server. */
            if (fromServer)
                tick = reader.ReadTickUnpacked();
            /* When coming from a client it will always be owner.
             * Client sends out replicates soon as they are run.
             * It's safe to use the LastRemoteTick from the client
             * in addition to QueuedInputs. */
            else
                tick = (tm.LastPacketTick.LastRemoteTick);

            int receivedReplicatesCount = reader.ReadReplicate<T>(ref arrBuffer, tick);

            //If received on clientHost simply ignore after parsing data.
            if (fromServer && IsHostStarted)
                return;

            /* Replicate rpc readers relay to this method and
            * do not have an owner check in the generated code. 
            * Only server needs to check for owners. Clients
            * should accept the servers data regardless. 
            *
            * If coming from a client and that client is not owner then exit. */
            if (!fromServer && !OwnerMatches(sender))
                return;
            //Early exit if old data.
            if (TimeManager.LastPacketTick.LastRemoteTick < _lastReplicateReadRemoteTick)
                return;
            _lastReplicateReadRemoteTick = TimeManager.LastPacketTick.LastRemoteTick;

            //If from a client that is not clientHost do some safety checks.
            if (!fromServer && !Owner.IsLocalClient)
            {
                if (receivedReplicatesCount > pm.RedundancyCount)
                {
                    sender.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, $"Connection {sender.ToString()} sent too many past replicates. Connection will be kicked immediately.");
                    return;
                }
            }

            Replicate_EnqueueReceivedReplicate<T>(receivedReplicatesCount, arrBuffer, replicatesQueue, replicatesHistory, channel);
            Replicate_SendNonAuthoritative<T>(hash, replicatesQueue, channel);
        }
#endif

#if PREDICTION_V2


        /// <summary>
        /// Sends data from a reader which only contains the replicate packet.
        /// </summary>
        /// <param name="tick">Tick of the last replicate entry.</param>
        [MakePublic]
        internal void Replicate_SendNonAuthoritative<T>(uint hash, BasicQueue<T> replicatesQueue, Channel channel) where T : IReplicateData
        {
            if (!IsServerStarted)
                return;
            if (!_networkObjectCache.EnableStateForwarding)
                return;

            int queueCount = replicatesQueue.Count;
            //Limit history count to max of queued amount, or queued inputs, whichever is lesser.
            int historyCount = (int)Mathf.Min(_networkObjectCache.PredictionManager.RedundancyCount, queueCount);
            //None to send.
            if (historyCount == 0)
                return;

            //If the only observer is the owner then there is no need to write.
            int observersCount = Observers.Count;
            //Quick exit for no observers other than owner.
            if (observersCount == 0 || (Owner.IsValid && observersCount == 1))
                return;

            PooledWriter methodWriter = WriterPool.Retrieve(WriterPool.LENGTH_BRACKET);

            uint localTick = _networkObjectCache.TimeManager.LocalTick;
            /* Write when the last entry will run.
             * 
             * Typically the last entry will run on localTick + (queueCount - 1).
             * 1 is subtracted from queueCount because in most cases the first entry
             * is going to run same tick.
             * An exception is when the replicateStartTick is set, then there is going
             * to be a delayed based on start tick difference. */
            uint runTickOflastEntry = localTick + ((uint)queueCount - 1);
            //If start tick is set then add on the delay.
            if (_replicateStartTick != TimeManager.UNSET_TICK)
                runTickOflastEntry += (_replicateStartTick - TimeManager.LocalTick);
            //Write the run tick now.
            methodWriter.WriteTickUnpacked(runTickOflastEntry);
            //Write the replicates.
            methodWriter.WriteReplicate<T>(replicatesQueue, historyCount, runTickOflastEntry);

            PooledWriter writer = CreateRpc(hash, methodWriter, PacketId.Replicate, channel);

            //Exclude owner and if clientHost, also localClient.
            _networkConnectionCache.Clear();
            if (Owner.IsValid)
                _networkConnectionCache.Add(Owner);
            if (IsClientStarted && !Owner.IsLocalClient)
                _networkConnectionCache.Add(ClientManager.Connection);
            NetworkManager.TransportManager.SendToClients((byte)channel, writer.GetArraySegment(), Observers, _networkConnectionCache, false);

            methodWriter.StoreLength();
            writer.StoreLength();
        }
#endif


#if !PREDICTION_V2
        private void Replicate_HandleReceivedReplicate<T>(int receivedReplicatesCount, T[] arrBuffer, BasicQueue<T> replicates, Channel channel) where T : IReplicateData
        {
            PredictionManager pm = PredictionManager;
            bool consumeExcess = !pm.DropExcessiveReplicates;
            //Maximum number of replicates allowed to be queued at once.
            int replicatesCountLimit = (consumeExcess) ? (TimeManager.TickRate * 2) : pm.GetMaximumServerReplicates();
            for (int i = 0; i < receivedReplicatesCount; i++)
            {
                uint tick = arrBuffer[i].GetTick();
                if (tick > _lastReceivedReplicateTick)
                {
                    //Cannot queue anymore, discard oldest.
                    if (replicates.Count >= replicatesCountLimit)
                    {
                        T data = replicates.Dequeue();
                        data.Dispose();
                    }

                    replicates.Enqueue(arrBuffer[i]);
                    _lastReceivedReplicateTick = tick;
                }
            }
        }
#else
        /// <summary>
        /// Handles a received replicate packet.
        /// </summary>
        private void Replicate_EnqueueReceivedReplicate<T>(int receivedReplicatesCount, T[] arrBuffer, BasicQueue<T> replicatesQueue, List<T> replicatesHistory, Channel channel) where T : IReplicateData
        {
            int startQueueCount = replicatesQueue.Count;
            /* Owner never gets this for their own object so
			 * this can be processed under the assumption data is only
			 * handled on unowned objects. */
            PredictionManager pm = PredictionManager;
            //Maximum number of replicates allowed to be queued at once.
            int maximmumReplicates = (IsServerStarted) ? pm.GetMaximumServerReplicates() : pm.MaximumClientReplicates;
            for (int i = 0; i < receivedReplicatesCount; i++)
            {
                T entry = arrBuffer[i];
                uint tick = entry.GetTick();

                //Skip if old data.
                if (tick <= _lastReadReplicateTick)
                {
                    entry.Dispose();
                    continue;
                }
                _lastReadReplicateTick = tick;
                //Cannot queue anymore, discard oldest.
                if (replicatesQueue.Count >= maximmumReplicates)
                {
                    T data = replicatesQueue.Dequeue();
                    data.Dispose();
                }

                /* Check if replicate is already in history.
                 * This can occur when the replicate method has a predicted
                 * state for the tick, but a user created replicate comes
                 * through afterwards. 
                 *
                 * Only perform this check if not the server, since server
                 * does not reconcile it will never use replicatesHistory.
                 * The server also does not predict replicates in the same way
                 * a client does. When an owner sends a replicate to the server
                 * the server only uses the owner tick to check if it's an old replicate.
                 * But when running the replicate, the server applies it's local tick and
                 * sends that to spectators. This ensures that replicates received by a client
                 * with an unstable connection are not skipped. */
                //Add automatically if server.
                if (_networkObjectCache.IsServerStarted)
                {
                    replicatesQueue.Enqueue(entry);
                }
                //Run checks to replace data if not server.
                else
                {
                    /* See if replicate tick is in history. Keep in mind
                     * this is the localTick from the server, not the localTick of
                     * the client which is having their replicate relayed. */
                    ReplicateTickFinder.DataPlacementResult findResult;
                    int index = ReplicateTickFinder.GetReplicateHistoryIndex(tick, replicatesHistory, out findResult);
                    /* Exact entry found. This is the most likely
                     * scenario. Client would have already run the tick
                     * in the future, and it's now being replaced with
                     * the proper data. */
                    if (findResult == ReplicateTickFinder.DataPlacementResult.Exact)
                    {
                        T prevEntry = replicatesHistory[index];
                        prevEntry.Dispose();
                        replicatesHistory[index] = entry;
                    }
                    else if (findResult == ReplicateTickFinder.DataPlacementResult.InsertMiddle)
                    {
                        replicatesHistory.Insert(index, entry);
                    }
                    else if (findResult == ReplicateTickFinder.DataPlacementResult.InsertEnd)
                    {
                        replicatesHistory.Add(entry);
                    }
                    /* Insert beginning should not happen unless the data is REALLY old.
                     * This would mean the network was in an unplayable state. Discard the
                     * data. */
                    if (findResult == ReplicateTickFinder.DataPlacementResult.InsertBeginning)
                    {
                        entry.Dispose();
                    }
                }
            }

            /* If entries are being added after nothing then
             * start the queued inputs delay. Only the server needs
             * to do this since clients implement the queue delay
             * by holding reconcile x ticks rather than not running received
             * x ticks. */
            if (_networkObjectCache.IsServerInitialized && startQueueCount == 0 && replicatesQueue.Count > 0)
                _replicateStartTick = (_networkObjectCache.TimeManager.LocalTick + pm.QueuedInputs);
        }

#endif

#if !PREDICTION_V2
        /// <summary>
        /// Checks conditions for a reconcile.
        /// </summary>
        /// <param name="asServer">True if checking as server.</param>
        /// <returns>Returns true if able to continue.</returns>
        [MakePublic] //internal
        internal bool Reconcile_ExitEarly_A(bool asServer, out Channel channel)
        {
            channel = Channel.Unreliable;
            //Server.
            if (asServer)
            {
                if (_remainingResends <= 0)
                    return true;

                _remainingResends--;
                if (_remainingResends == 0)
                    channel = Channel.Reliable;
            }
            //Client.
            else
            {
                if (!ClientHasReconcileData)
                    return true;

                /* If clientHost then invoke reconciles but
				 * don't actually reconcile. This is done
				 * because certain user code may
				 * rely on those events running even as host. */
                if (IsServerStarted)
                {
                    PredictionManager.InvokeOnReconcile(this, true);
                    PredictionManager.InvokeOnReconcile(this, false);
                    return true;
                }
            }

            //Checks pass.
            return false;
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Updates lastReconcileTick as though running asServer.
        /// </summary>
        /// <param name="ird">Data to set tick on.</param>
        [MakePublic]
        internal void Reconcile_Server<T>(uint methodHash, T data, Channel channel) where T : IReconcileData
        {
            /* //todo
			 * the codegen right now calls this if asServer
			 * and Reconcile_Client if not.
			 * They both need to be called and each handled appropriately,
			 * like done for the replicate method.
			 * Do not forget to make this into a new method using defines,
			 * for for predictionv1 and v2. */
            //Server always uses last replicate tick as reconcile tick.
            uint tick = _lastReplicateTick;
            data.SetTick(tick);
            SetLastReconcileTick(tick);

            PredictionManager.InvokeServerReconcile(this, true);
            Server_SendReconcileRpc(methodHash, data, channel);
            PredictionManager.InvokeServerReconcile(this, false);

        }
#else
        /// <summary>
        /// Sends a reconcile to clients.
        /// </summary>
        public void Reconcile_Server<T>(uint methodHash, T data, Channel channel) where T : IReconcileData
        {
            if (!IsServerStarted)
                return;

            //Tick does not need to be set for reconciles since they come in as state updates, which have the tick included globally.
            //Use reliable during development.
            channel = Channel.Reliable;
            PredictionManager.InvokeServerReconcile(this, true);
            Server_SendReconcileRpc(methodHash, data, channel);
            PredictionManager.InvokeServerReconcile(this, false);
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Processes a reconcile for client.
        /// </summary>
        [MakePublic]
        internal void Reconcile_Client<T, T2>(ReconcileUserLogicDelegate<T> reconcileDel, ReplicateUserLogicDelegate<T2> replicateULDel, List<T2> replicates, T data, Channel channel) where T : IReconcileData where T2 : IReplicateData
        {
            uint tick = data.GetTick();

            /* If the first entry in cllection has a tick higher than
			 * the received tick then something went wrong, do not reconcile. */
            if (replicates.Count > 0 && replicates[0].GetTick() > tick)
                return;

            UnityScene scene = gameObject.scene;
            PhysicsScene ps = scene.GetPhysicsScene();
            PhysicsScene2D ps2d = scene.GetPhysicsScene2D();

            //This must be set before reconcile is invoked.
            SetLastReconcileTick(tick);
            //Invoke that reconcile is starting.
            PredictionManager.InvokeOnReconcile(this, true);
            //Call reconcile user logic.
            reconcileDel?.Invoke(data, false, channel);

            //True if the timemanager is handling physics simulations.
            bool tmPhysics = (TimeManager.PhysicsMode == PhysicsMode.TimeManager);
            //Sync transforms if using tm physics.
            if (tmPhysics)
            {
                Physics.SyncTransforms();
                Physics2D.SyncTransforms();
            }

            //Remove excess from buffered inputs.
            int queueIndex = -1;
            for (int i = 0; i < replicates.Count; i++)
            {
                if (replicates[i].GetTick() == tick)
                {
                    queueIndex = i;
                    break;
                }
            }
            //Now found, weird.
            if (queueIndex == -1)
                replicates.Clear();
            //Remove up to found, including it.
            else
                replicates.RemoveRange(0, queueIndex + 1);

            //Number of replays which will be performed.
            int replays = replicates.Count;
            float tickDelta = (float)TimeManager.TickDelta;

            for (int i = 0; i < replays; i++)
            {
                T2 rData = replicates[i];
                uint replayTick = rData.GetTick();

                PredictionManager.InvokeOnReplicateReplay(scene, replayTick, ps, ps2d, true);

                //Replay the data using the replicate logic delegate.
                replicateULDel.Invoke(rData, false, channel, true);
                if (tmPhysics)
                {
                    ps.Simulate(tickDelta);
                    ps2d.Simulate(tickDelta);
                }

                PredictionManager.InvokeOnReplicateReplay(scene, replayTick, ps, ps2d, false);
            }

            //Reconcile ended.
            PredictionManager.InvokeOnReconcile(this, false);
        }
#else
        /// <summary>
        /// This is called when the networkbehaviour should perform a reconcile.
        /// Codegen overrides this calling Reconcile_Client with the needed data.
        /// </summary>
        internal virtual void Reconcile_Client_Start() { }
        /// <summary>
        /// Processes a reconcile for client.
        /// </summary>
        [APIExclude]
        [MakePublic]
        protected internal void Reconcile_Client<T, T2>(ReconcileUserLogicDelegate<T> reconcileDel, List<T2> replicatesHistory, T data) where T : IReconcileData where T2 : IReplicateData
        {
            IsBehaviourReconciling = true;
            if (replicatesHistory.Count > 0)
            {
                /* Remove replicates up to reconcile. Since the reconcile
                 * is the state after a replicate for it's tick we no longer
                 * need any replicates prior. */
                //Find the closest entry which can be removed.
                int removalCount = 0;
                uint dataTick = data.GetTick();
                //A few quick tests.
                if (replicatesHistory.Count > 0)
                {
                    /* If the last entry in history is less or equal
                     * to datatick then all histories need to be removed
                     * as reconcile is beyond them. */
                    if (replicatesHistory[^1].GetTick() <= dataTick)
                    {
                        removalCount = replicatesHistory.Count;
                    }
                    //Somewhere in between. Find what to remove up to.
                    else
                    {
                        for (int i = 0; i < replicatesHistory.Count; i++)
                        {
                            uint entryTick = replicatesHistory[i].GetTick();
                            /* Soon as an entry beyond dataTick is
                             * found remove up to that entry. */
                            if (entryTick > dataTick)
                            {
                                removalCount = i;
                                break;
                            }
                        }
                    }
                }

                for (int i = 0; i < removalCount; i++)
                    replicatesHistory[i].Dispose();
                replicatesHistory.RemoveRange(0, removalCount);
            }
            //Call reconcile user logic.
            reconcileDel?.Invoke(data, Channel.Reliable);
        }
#endif

#if PREDICTION_V2
        internal void Reconcile_Client_End()
        {
            ClientHasReconcileData = false;
            IsBehaviourReconciling = false;
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Reads a reconcile the client.
        /// </summary>
        public void Reconcile_Reader<T>(PooledReader reader, ref T data, Channel channel) where T : IReconcileData
        {
            uint tick = reader.ReadUInt32();
            T newData = reader.Read<T>();

            //Tick is old or already processed.
            if (tick <= _lastReceivedReconcileTick)
                return;
            //Only owner reconciles. Maybe ownership changed then packet arrived out of order.
            if (!IsOwner)
                return;

            data = newData;
            data.SetTick(tick);
            ClientHasReconcileData = true;
            _lastReceivedReconcileTick = tick;
        }
#else
        /// <summary>
        /// Reads a reconcile for the client.
        /// </summary>
        public void Reconcile_Reader<T>(PooledReader reader, ref T data, Channel channel) where T : IReconcileData
        {
            T newData = reader.Read<T>();
            uint tick = (IsOwner) ? PredictionManager.ClientStateTick : PredictionManager.ServerStateTick;
            //Do not process if an old state.
            if (tick < _lastReadReconcileTick)
                return;

            data = newData;
            data.SetTick(tick);

            ClientHasReconcileData = true;
            _lastReadReconcileTick = tick;
        }
#endif

    }
}