using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Managing.Predicting;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
using FishNet.Object.Prediction.Delegating;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityScene = UnityEngine.SceneManagement.Scene;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Public.
#if PREDICTION_V2
        /// <summary>
        /// True if this Networkbehaviour implements prediction methods.
        /// </summary>
        [APIExclude]
        [CodegenMakePublic]
        protected internal bool UsesPrediction;
#endif
        /// <summary>
        /// True if the client has cached reconcile 
        /// </summary>
        internal bool ClientHasReconcileData;
        /// <summary>
        /// 
        /// </summary>
        private uint _lastReconcileTick;
        /// <summary>
        /// Gets the last tick this NetworkBehaviour reconciled with.
        /// </summary>
        public uint GetLastReconcileTick() => _lastReconcileTick;

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
#if !PREDICTION_V2
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
#else
        /// <summary>
        /// Sets the last tick this NetworkBehaviour replicated with.
        /// For internal use only.
        /// </summary>
        private void SetLastReplicateTick(uint value, bool updateGlobals = true)
        {
            _lastReplicateTick = value;
            _networkObjectCache.LastReplicateTick = value;
            Owner.LastReplicateTick = value;
            if (updateGlobals)
            {
                Owner.LocalReplicateTick = TimeManager.LocalTick;
                PredictionManager.LastReplicateTick = value;
            }
        }

#endif
        /// <summary>
        /// True if this object is reconciling.
        /// </summary>
        public bool IsReconciling { get; internal set; }
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
        #endregion

        /// <summary>
        /// Registers a RPC method.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude]
        [CodegenMakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterReplicateRpc_Internal(uint hash, ReplicateRpcDelegate del)
        {
            _replicateRpcDelegates[hash] = del;
        }
        /// <summary>
        /// Registers a RPC method.
        /// Internal use.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="del"></param>
        [APIExclude]
        [CodegenMakePublic]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal void RegisterReconcileRpc_Internal(uint hash, ReconcileRpcDelegate del)
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
            ClearReplicateCache_Internal(asServer);
        }
        /// <summary>
        /// Clears cached replicates for server and client. This can be useful to call on server and client after teleporting.
        /// </summary>
        public void ClearReplicateCache()
        {
            ResetLastPredictionTicks();
            ClearReplicateCache_Internal(true);
            ClearReplicateCache_Internal(false);
        }
        /// <summary>
        /// Clears cached replicates.
        /// For internal use only.
        /// </summary>
        /// <param name="asServer"></param>
        [CodegenMakePublic]
        [APIExclude]
        protected internal virtual void ClearReplicateCache_Internal(bool asServer) { }
#else
        /// <summary>
        /// Clears cached replicates for server and client. This can be useful to call on server and client after teleporting.
        /// </summary>
        public void ClearReplicateCache()
        {
            ResetLastPredictionTicks();
            ClearReplicateCache_Internal<IReplicateData>(null, null);
        }
        /// <summary>
        /// Clears cached replicates.
        /// For internal use only.
        /// </summary>
        /// <param name="asServer"></param>
        [CodegenMakePublic]
        [APIExclude]
        protected internal virtual void ClearReplicateCache_Internal<T>(BasicQueue<T> replicatesQueue, List<T> replicatesHistory) where T : IReplicateData
        {
            if (replicatesHistory == null)
                return;

            Debug.LogError("Clearing");
            //Queue.
            while (replicatesQueue.Count > 0)
            {
                T data = replicatesQueue.Dequeue();
                data.Dispose();
            }
            //History.
            for (int i = 0; i < replicatesHistory.Count; i++)
                replicatesHistory[i].Dispose();
            replicatesHistory.Clear();
        }
#endif
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


#if !PREDICTION_V2
        /// <summary>
        /// Writes number of past inputs from buffer to writer and sends it to the server.
        /// Internal use. 
        /// </summary>
        [APIExclude]
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
            PooledWriter methodWriter = WriterPool.GetWriter(WriterPool.LENGTH_BRACKET);
            methodWriter.WriteReplicate<T>(replicates, offset);
            PooledWriter writer;
            //if (_rpcLinks.TryGetValueIL2CPP(hash, out RpcLinkType link))
            //writer = CreateLinkedRpc(link, methodWriter, Channel.Unreliable);
            //else //todo add support for -> server rpc links.

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

            methodWriter.DisposeLength();
            writer.DisposeLength();
        }
#endif


#if !PREDICTION_V2
        /// <summary>
        /// Sends a RPC to target.
        /// Internal use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenMakePublic]
        [APIExclude]
        private void Server_SendReconcileRpc<T>(uint hash, T reconcileData, Channel channel)
        {
            if (!IsSpawned)
                return;
            if (!Owner.IsActive)
                return;

            PooledWriter methodWriter = WriterPool.GetWriter();
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

            methodWriter.Dispose();
            writer.Dispose();
        }
#else
        /// <summary>
        /// Sends a RPC to target.
        /// Internal use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenMakePublic]
        [APIExclude]
        protected internal void Server_SendReconcileRpc<T>(uint hash, T reconcileData, Channel channel)
        {
            if (!IsSpawned)
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

            foreach (NetworkConnection nc in Observers)
                nc.WriteState(writer);

            methodWriter.Dispose();
            writer.Dispose();
        }
#endif
        /// <summary> 
        /// Returns if there is a chance the transform may change after the tick.
        /// </summary>
        /// <returns></returns>
        protected internal bool PredictedTransformMayChange()
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

#if !PREDICTION_V2
        /// <summary>
        /// Checks conditions for a replicate.
        /// </summary>
        /// <param name="asServer">True if checking as server.</param>
        /// <returns>Returns true if to exit the replicate early.</returns>
        [CodegenMakePublic] //internal
        [APIExclude]
        public bool Replicate_ExitEarly_A_Internal(bool asServer, bool replaying)
        {
            bool isOwner = IsOwner;
            //Server.
            if (asServer)
            {
                //No owner, do not try to replicate 'owner' input.
                if (!Owner.IsActive)
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
                if (replaying && IsServer)
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
        /// Gets the index in replicates where the tick matches.
        /// </summary>
        private int GetReplicateHistoryIndex<T>(uint tick, List<T> replicatesHistory) where T : IReplicateData
        {
            int replicatesCount = replicatesHistory.Count;
            if (replicatesCount == 0)
            {
                return -1;
            }
            /* If the first entry tick is larger than 
             * replay tick then there is no way
             * future entries will match as they will
             * only increase in tick. */
            else if (replicatesHistory[0].GetTick() > tick)
            {
                return -1;
            }
            /* If the last tick is less than replayTick
             * then something has gone horribly wrong.
             * This should never be possible. */
            else if (replicatesHistory[replicatesCount - 1].GetTick() < tick)
            {
                return -1;
            }
            //Find queueIndex.
            else
            {
                for (int i = 0; i < replicatesHistory.Count; i++)
                {
                    if (replicatesHistory[i].GetTick() == tick)
                        return i;
                }

                //Not found.
                return -1;
            }
        }
        /// <summary>
        /// Called internally when an input from localTick should be replayed.
        /// </summary>
        internal virtual void Replicate_Replay_Start(uint replayTick) { }
        /// <summary>
        /// Replays inputs from replicates.
        /// </summary>
        protected internal void Replicate_Replay<T>(uint replayTick, ReplicateUserLogicDelegate<T> del, List<T> replicatesHistory, Channel channel) where T : IReplicateData
        {
            int replicateIndex = GetReplicateHistoryIndex<T>(replayTick, replicatesHistory);

            T data;
            ReplicateState state;
            if (replicateIndex == -1)
            {
                data = default;
                data.SetTick(replayTick);
                state = ReplicateState.ReplayedUnsetData;
            }
            else
            {
                data = replicatesHistory[replicateIndex];
                state = ReplicateState.ReplayedNewData;
            }

            del.Invoke(data, state, channel);
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Gets the next replicate in perform when server or non-owning client.
        /// </summary>
        [CodegenMakePublic] //internal
        [APIExclude]
        public void Replicate_NonOwner_Internal<T>(ReplicateUserLogicDelegate<T> del, BasicQueue<T> q, Channel channel) where T : IReplicateData
        {
            int count = q.Count;
            if (count > 0)
            {
                ReplicateData(q.Dequeue());
                count--;

                PredictionManager pm = PredictionManager;
                bool consumeExcess = !pm.DropExcessiveReplicates;
                //Number of entries to leave in buffer when consuming.
                const int leaveInBuffer = 2;
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
#else
        /// <summary>
        /// Gets the next replicate in perform when server or non-owning client.
        /// </summary>
        /// </summary>
        [CodegenMakePublic] //internal
        [APIExclude]
        protected internal void Replicate_NonOwner_Internal<T>(ReplicateUserLogicDelegate<T> del, Queue<T> replicatesQueue, List<T> replicatesHistory, Channel channel) where T : IReplicateData
        {
            //Debug.LogError("There needs to be a queue for replicates running and a list for past replicates." +
            //    "The queue will remove an entry every time the replicate is performed, and add it to the past replicates collection." +
            //    "When a reconcile occurs past replicates are removed up to the reconcile point. Be sure to clear both when resetting prediction." +
            //    "Also make sure the list of past replicates cannot get too large should some reason the reconcile packet never come.");

            if (IsOwner)
                return;

            int count = replicatesQueue.Count;
            if (count > 0)
            {
                ReplicateData(replicatesQueue.Dequeue(), false);
                count--;

                PredictionManager pm = PredictionManager;
                bool consumeExcess = (!pm.DropExcessiveReplicates || IsClientOnly);
                const int leaveInBuffer = 1;
                //Only consume if the queue count is over leaveInBuffer.
                if (consumeExcess && count > leaveInBuffer)
                {
                    byte maximumAllowedConsumes = pm.MaximumReplicateConsumeCount;
                    int maximumPossibleConsumes = (count - leaveInBuffer);
                    int consumeAmount = Mathf.Min(maximumAllowedConsumes, maximumPossibleConsumes);

                    for (int i = 0; i < consumeAmount; i++)
                        ReplicateData(replicatesQueue.Dequeue(), false);
                }

                _remainingResends = pm.RedundancyCount;
            }
            else
            {
                ReplicateData(default, true);
            }


            void ReplicateData(T data, bool defaultData)
            {
                //If data is default then set tick to estimated value.
                if (defaultData)
                {
                    _estimatedReplicateTick++;
                    data.SetTick(_estimatedReplicateTick);
                }
                //Not default, update estimated tick.
                else
                {
                    _estimatedReplicateTick = data.GetTick();
                }

                SetLastReplicateTick(_estimatedReplicateTick);
                //Add to history.
                replicatesHistory.Add(data);
                //Invoke replicate method.
                del.Invoke(data, ReplicateState.NewData, channel);
            }
        }

        private uint _estimatedReplicateTick = 0;
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Returns if a replicates data changed and updates resends as well data tick.
        /// </summary>
        /// <param name="enqueueData">True to enqueue data for replaying.</param>
        /// <returns>True if data has changed..</returns>
        [CodegenMakePublic] //internal
        [APIExclude]
        public void Replicate_Owner_Internal<T>(ReplicateUserLogicDelegate<T> del, uint methodHash, List<T> replicates, T data, Channel channel) where T : IReplicateData
        {
            //Only check to enque/send if not clientHost.
            if (!IsServer)
            {
                Func<T, bool> isDefaultDel = GeneratedComparer<T>.IsDefault;
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
        [CodegenMakePublic] //internal
        [APIExclude]
        protected internal void Replicate_Owner_Internal<T>(ReplicateUserLogicDelegate<T> del, uint methodHash, List<T> replicatesHistory, T data, Channel channel) where T : IReplicateData
        {
            if (!IsOwner)
                return;

            //Only check to enqueu/send if not clientHost.
            Func<T, bool> isDefaultDel = GeneratedComparer<T>.IsDefault;
            if (isDefaultDel == null)
            {
                NetworkManager.LogError($"ReplicateComparers not found for type {typeof(T).FullName}");
                return;
            }

            PredictionManager pm = NetworkManager.PredictionManager;
            uint localTick = TimeManager.LocalTick;

            data.SetTick(localTick);
            /* Always add to history so data
             * can be replayed, even if default. */
            replicatesHistory.Add(data);
            //Check to reset resends.
            bool isDefault = isDefaultDel.Invoke(data);
            bool mayChange = PredictedTransformMayChange();
            bool resetResends = (pm.UsingRigidbodies || mayChange || !isDefault);
            if (resetResends)
                _remainingResends = pm.RedundancyCount;

            bool enqueueData = (_remainingResends > 0);
            if (enqueueData)
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
                int maxCount = (IsServer) ? pm.RedundancyCount : pm.MaximumClientReplicates;
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
                bool toServer = !IsServer;
                SendReplicateRpc(toServer, methodHash, replicatesHistory, channel);
                _remainingResends--;
            }

            //Update last replicate tick.
            SetLastReplicateTick(localTick);
            //Owner always replicates with new data.
            del.Invoke(data, ReplicateState.NewData, channel);
        }
#endif

#if PREDICTION_V2
        /// <summary>
        /// Sends a Replicate to server or clients.
        /// </summary>
        private void SendReplicateRpc<T>(bool toServer, uint hash, List<T> replicatesHistory, Channel channel)
        {
            if (!IsSpawnedWithWarning())
                return;

            int historyCount = replicatesHistory.Count;
            //Nothing to send; should never be possible.
            if (historyCount <= 0)
                return;

            //Number of past inputs to send.
            int pastInputs = Mathf.Min(PredictionManager.RedundancyCount, historyCount);
            /* Where to start writing from. When passed
             * into the writer values from this offset
             * and forward will be written. 
             * Always write up to past inputs. */
            int offset = (historyCount - pastInputs);

            //Write history to methodWriter.
            PooledWriter methodWriter = WriterPool.GetWriter(WriterPool.LENGTH_BRACKET);
            methodWriter.WriteReplicate<T>(replicatesHistory, offset);
            PooledWriter writer = CreateRpc(hash, methodWriter, PacketId.Replicate, channel);

            if (toServer)
            {
                NetworkManager.TransportManager.SendToServer((byte)channel, writer.GetArraySegment(), false);
            }
            else
            {
                //Exclude owner and if clientHost, also localClient.
                _networkConnectionCache.Clear();
                _networkConnectionCache.Add(Owner);
                if (IsClient)
                    _networkConnectionCache.Add(ClientManager.Connection);

                NetworkManager.TransportManager.SendToClients((byte)channel, writer.GetArraySegment(), Observers, _networkConnectionCache, false);
            }

            /* If sending as reliable there is no reason
             * to perform resends, so clear remaining resends. */
            if (channel == Channel.Reliable)
                _remainingResends = 0;

            methodWriter.DisposeLength();
            writer.DisposeLength();
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Reads a replicate the client.
        /// </summary>
        /// <param name="replicateDataOnly">Data from the reader which only applies to the replicate.</param>
        [CodegenMakePublic] //Internal.
        public void Replicate_Reader_Internal<T>(PooledReader reader, NetworkConnection sender, T[] arrBuffer, BasicQueue<T> replicates, Channel channel) where T : IReplicateData
        {
            PredictionManager pm = PredictionManager;

            /* Data can be read even if owner is not valid because user
             * may switch ownership on an object and recv a replicate from
             * the previous owner. */
            int receivedReplicatesCount = reader.ReadReplicate<T>(ref arrBuffer, TimeManager.LastPacketTick);
            /* Replicate rpc readers relay to this method and
             * do not have an owner check in the generated code. */
            if (!OwnerMatches(sender))
                return;

            if (receivedReplicatesCount > pm.RedundancyCount)
            {
                sender.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, $"Connection {sender.ToString()} sent to many past replicates. Connection will be kicked immediately.");
                return;
            }

            Replicate_HandleReceivedReplicate<T>(receivedReplicatesCount, arrBuffer, replicates, channel);
        }
#else
        /// <summary>
        /// Reads a replicate the client.
        /// </summary>
        /// <param name="replicateDataOnly">Data from the reader which only applies to the replicate.</param>
        [CodegenMakePublic] //Internal.
        protected internal void Replicate_Reader_Internal<T>(uint hash, PooledReader reader, NetworkConnection sender, T[] arrBuffer, BasicQueue<T> replicatesQueue, Channel channel) where T : IReplicateData
        {
            bool fromClient = (reader.Source == Reader.DataSource.Client);
            bool isLocalClient = Owner.IsLocalClient;
            PredictionManager pm = PredictionManager;
            //int startingPosition = reader.Position;
            //Debug.LogError("Read notes");
            /*
             *  Right now in the stateupdate header the lastpackettick of each client,
             *  to that specific client. This is the tick for that client which the state is,
             *  and the tick that client should roll back to.
             *  
             *  However, for receiving others data the client needs the server localtick, since
             *  the data is coming from the server. We cannot use other client ticks because
             *  it would vary per client; unless we packed the tick into each forwarded inputs
             *  but thats likely not needed.
             *  
             *  Instead, when sending a state update also send the server localtick. When
             *  reconciling and replaying inputs check against received server localtick
             *  if not owner, or StateClientTick if owner.
             * 
             */
            uint lastPacketTick = TimeManager.LastPacketTick;
            //uint lastPacketTick = (fromClient) ? sender.LastPacketTick : reader.ReadTickUnpacked(); //TimeManager.LastPacketTick;
            int receivedReplicatesCount = reader.ReadReplicate<T>(ref arrBuffer, lastPacketTick);
            //Early exit if old data.
            if (lastPacketTick < _lastReceivedReplicateTick)
                return;

            /* Replicate rpc readers relay to this method and
             * do not have an owner check in the generated code. 
             * Only server needs to check for owners. Clients
             * should accept the servers data regardless. 
             *
             * If coming from a client and that client is now owner then exit. */
            if (fromClient && !OwnerMatches(sender))
                return;


            //Only actually enqueue the replicate if it's not from clientHost.
            if (fromClient && !isLocalClient)
            {
                if (receivedReplicatesCount > pm.RedundancyCount)
                {
                    sender.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, $"Connection {sender.ToString()} sent to many past replicates. Connection will be kicked immediately.");
                    return;
                }
            }
            Replicate_HandleReceivedReplicate<T>(receivedReplicatesCount, arrBuffer, replicatesQueue, channel);

            ////Only server needs to send to spectators.
            //if (IsServer)
            //{
            //    ArraySegment<byte> replicateDataOnly = new ArraySegment<byte>(reader.GetByteBuffer(), startingPosition, (reader.Position - startingPosition));
            //    Replicate_Server_SendToSpectators_Internal(hash, replicateDataOnly, replicates.Count);
            //}
        }
#endif


#if !PREDICTION_V2
        private void Replicate_HandleReceivedReplicate<T>(int receivedReplicatesCount, T[] arrBuffer, BasicQueue<T> replicates, Channel channel) where T : IReplicateData
        {
            PredictionManager pm = PredictionManager;
            bool consumeExcess = !pm.DropExcessiveReplicates;
            //Maximum number of replicates allowed to be queued at once.
            int replicatesCountLimit = (consumeExcess) ? PredictionManager.MaximumReplicateConsumeCount : pm.GetMaximumServerReplicates();

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
        /// <typeparam name="T"></typeparam>
        /// <param name="receivedReplicatesCount"></param>
        /// <param name="arrBuffer"></param>
        /// <param name="replicates"></param>
        /// <param name="channel"></param>
        private void Replicate_HandleReceivedReplicate<T>(int receivedReplicatesCount, T[] arrBuffer, BasicQueue<T> replicatesQueue, Channel channel) where T : IReplicateData
        {
            /* Owner never gets this for their own object so
             * this can be processed under the assumption data is only
             * handled on unowned objects. */

            PredictionManager pm = PredictionManager;
            //Maximum number of replicates allowed to be queued at once.
            int maximmumReplicates = (IsServer) ? pm.GetMaximumServerReplicates() : pm.MaximumClientReplicates;

            for (int i = 0; i < receivedReplicatesCount; i++)
            {
                uint tick = arrBuffer[i].GetTick();
                if (tick > _lastReceivedReplicateTick)
                {
                    //Cannot queue anymore, discard oldest.
                    if (replicatesQueue.Count >= maximmumReplicates)
                    {
                        T data = replicatesQueue.Dequeue();
                        data.Dispose();
                    }

                    replicatesQueue.Enqueue(arrBuffer[i]);
                    _lastReceivedReplicateTick = tick;
                }
            }
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Checks conditions for a reconcile.
        /// </summary>
        /// <param name="asServer">True if checking as server.</param>
        /// <returns>Returns true if able to continue.</returns>
        [CodegenMakePublic] //internal
        [APIExclude]
        public bool Reconcile_ExitEarly_A_Internal(bool asServer, out Channel channel)
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

                ClientHasReconcileData = false;
                /* If clientHost then invoke reconciles but
                 * don't actually reconcile. This is done
                 * because certain user code may
                 * rely on those events running even as host. */
                if (IsServer)
                {
                    PredictionManager.InvokeOnReconcile_Internal(this, true);
                    PredictionManager.InvokeOnReconcile_Internal(this, false);
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
        public void Reconcile_Server_Internal<T>(uint methodHash, T data, Channel channel) where T : IReconcileData
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
        public void Reconcile_Server_Internal<T>(uint methodHash, T data, Channel channel) where T : IReconcileData
        {
            if (!IsServer)
                return;

            //Server always uses last replicate tick as reconcile tick.
            uint tick = _lastReplicateTick;
            data.SetTick(tick);
            SetLastReconcileTick(tick);

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
        public void Reconcile_Client_Internal<T, T2>(ReconcileUserLogicDelegate<T> reconcileDel, ReplicateUserLogicDelegate<T2> replicateULDel, List<T2> replicates, T data, Channel channel) where T : IReconcileData where T2 : IReplicateData
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
            PredictionManager.InvokeOnReconcile_Internal(this, true);
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

                PredictionManager.InvokeOnReplicateReplay_Internal(scene, replayTick, ps, ps2d, true);

                //Replay the data using the replicate logic delegate.
                replicateULDel.Invoke(rData, false, channel, true);
                if (tmPhysics)
                {
                    ps.Simulate(tickDelta);
                    ps2d.Simulate(tickDelta);
                }

                PredictionManager.InvokeOnReplicateReplay_Internal(scene, replayTick, ps, ps2d, false);
            }

            //Reconcile ended.
            PredictionManager.InvokeOnReconcile_Internal(this, false);
        }
#else

        /// <summary>
        /// This is called when the networkbehaviour should perform a reconcile.
        /// Codegen overrides this calling Reconcile_Client_Internal with the needed data.
        /// </summary>
        internal virtual void Reconcile_Client_Start() { }
        /// <summary>
        /// Processes a reconcile for client.
        /// </summary>
        [APIExclude]
        [CodegenMakePublic]
        protected internal void Reconcile_Client_Internal<T, T2>(ReconcileUserLogicDelegate<T> reconcileDel, List<T2> replicatesHistory, T data) where T : IReconcileData where T2 : IReplicateData
        {
            if (!ClientHasReconcileData)
                return;

            uint tick = data.GetTick();
            //This must be set before reconcile is invoked.
            SetLastReconcileTick(tick);

            if (replicatesHistory.Count > 0)
            {
                //Remove from replicates up to reconcile.
                int replicateIndex = GetReplicateHistoryIndex<T2>(tick, replicatesHistory);
                if (replicateIndex >= 0)
                    replicatesHistory.RemoveRange(0, replicateIndex + 1);
            }
            //Call reconcile user logic.
            reconcileDel?.Invoke(data, Channel.Reliable);
        }
#endif

#if !PREDICTION_V2
        /// <summary>
        /// Reads a reconcile the client.
        /// </summary>
        public void Reconcile_Reader_Internal<T>(PooledReader reader, ref T data, Channel channel) where T : IReconcileData
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
        public void Reconcile_Reader_Internal<T>(PooledReader reader, ref T data, Channel channel) where T : IReconcileData
        {
            T newData = reader.Read<T>();
            //Server will still get reconcile but should not perform it.
            if (reader.Source == Reader.DataSource.Client)
                return;
            uint tick = (IsOwner) ? PredictionManager.StateClientTick : PredictionManager.StateServerTick;
            /* //TODO old states cannot happen for reliable but can for unreliable.
             * Be sure to check if state is old before processing for unreliable.
             * Do not need to check per NB as state comes through together.
             * If the state tick is less than previous than
             * its old data. */

            data = newData;
            data.SetTick(tick);

            ClientHasReconcileData = true;
            _lastReceivedReconcileTick = tick;
        }
#endif

    }
}