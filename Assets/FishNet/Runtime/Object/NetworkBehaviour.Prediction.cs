﻿using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Predicting;
using FishNet.Managing.Timing;
using FishNet.Object.Prediction;
using FishNet.Object.Prediction.Delegating;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Constant;
using FishNet.Utility.Extension;
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
        /// <summary>
        /// 
        /// </summary>
        private uint _lastReconcileTick;
        /// <summary>
        /// Gets the last tick this NetworkBehaviour reconciled with.
        /// </summary>
        public uint GetLastReconcileTick() => _lastReconcileTick;
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
        private void SetLastReplicateTick(uint value)
        {
            Owner.LocalReplicateTick = TimeManager.LocalTick;
            _lastReplicateTick = value;
            //Also set on the timemanager.
            PredictionManager.LastReplicateTick = value;
        }
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
        /// Last enqueued replicate tick on the server.
        /// </summary> 
        private uint _lastReceivedReplicateTick;
        /// <summary>
        /// Last tick of a reconcile received from the server.
        /// </summary>
        private uint _lastReceivedReconcileTick;
        /// <summary>
        /// True if the client has cached reconcile 
        /// </summary>
        private bool _clientHasReconcileData;
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
        /// Resets last predirection tick values.
        /// </summary>
        private void ResetLastPredictionTicks()
        {
            _lastReconcileTick = 0;
            _lastSentReplicateTick = 0;
            _lastReceivedReplicateTick = 0;
            _lastReceivedReconcileTick = 0;
            SetLastReplicateTick(0);
        }
        /// <summary>
        /// Clears cached replicates.
        /// For internal use only.
        /// </summary>
        /// <param name="asServer"></param>
        [CodegenMakePublic]
        [APIExclude]
        protected internal virtual void ClearReplicateCache_Internal(bool asServer) { }

        /// <summary>
        /// Writes number of past inputs from buffer to writer and sends it to the server.
        /// Internal use. 
        /// </summary>
        [CodegenMakePublic]
        [APIExclude]
        private void SendReplicateRpc<T>(uint hash, List<T> replicates, Channel channel) where T : IReplicateData
        {
            if (!IsSpawnedWithWarning())
                return;

            int bufferCount = replicates.Count;
            int lastBufferIndex = (bufferCount - 1);
            //Nothing to send; should never be possible.
            if (lastBufferIndex < 0)
                return;

            //Number of past inputs to send.
            int pastInputs = Mathf.Min(PredictionManager.GetRedundancyCount(), bufferCount);
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

        private uint _lastSentReplicateTick;

        /// <summary>
        /// Sends a RPC to target.
        /// Internal use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CodegenMakePublic]
        [APIExclude]
        internal void SendReconcileRpc<T>(uint hash, T reconcileData, Channel channel)
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

        /// <summary>
        /// Gets the next replicate in queue.
        /// </summary>
        [CodegenMakePublic] //internal
        [APIExclude]
        public void Replicate_Server_Internal<T>(ReplicateUserLogicDelegate<T> del, Queue<T> q, Channel channel) where T : IReplicateData
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
                    byte maximumAllowedConsumes = pm.GetMaximumConsumeCount();
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

                _remainingResends = pm.GetRedundancyCount();
            }
            else
            {
                del.Invoke(default, true, channel, false);
            }
        }

        /// <summary>
        /// Returns the maximum amount of replicates when consuming multiple inputs per tick.
        /// </summary>
        /// <returns></returns>
        private int GetMaximumReplicatesWhenConsuming() => (TimeManager.TickRate * 3);

        /// <summary>
        /// Returns if a replicates data changed and updates resends as well data tick.
        /// </summary>
        /// <param name="enqueueData">True to enqueue data for replaying.</param>
        /// <returns>True if data has changed..</returns>
        [CodegenMakePublic] //internal
        [APIExclude]
        public void Replicate_Client_Internal<T>(ReplicateUserLogicDelegate<T> del, uint methodHash, List<T> replicates, T data, Channel channel) where T : IReplicateData
        {
            //Only check to enqueu/send if not clientHost.
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
                bool mayChange = TransformMayChange();
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
                    _remainingResends = pm.GetRedundancyCount();

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
                    SendReplicateRpc<T>(methodHash, replicates, channel);
                    //Update last replicate tick.
                    SetLastReplicateTick(TimeManager.LocalTick);
                }
            }

            del.Invoke(data, false, channel, false);
        }


        /// <summary>
        /// Reads a replicate the client.
        /// </summary>
        public void Replicate_Reader_Internal<T>(PooledReader reader, NetworkConnection sender, T[] arrBuffer, Queue<T> replicates, Channel channel) where T : IReplicateData
        {
            int receivedReplicatesCount = reader.ReadReplicate<T>(ref arrBuffer, TimeManager.LastPacketTick);
            if (!OwnerMatches(sender))
                return;

            PredictionManager pm = PredictionManager;
            bool consumeExcess = !pm.DropExcessiveReplicates;
            //Maximum number of replicates allowed to be queued at once.
            int replicatesCountLimit = (consumeExcess) ?
                GetMaximumReplicatesWhenConsuming() : pm.GetMaximumServerReplicates();

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
                if (!_clientHasReconcileData)
                    return true;

                _clientHasReconcileData = false;
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

        /// <summary>
        /// Updates lastReconcileTick as though running asServer.
        /// </summary>
        /// <param name="ird">Data to set tick on.</param>
        public void Reconcile_Server_Internal<T>(uint methodHash, T data, Channel channel) where T : IReconcileData
        {
            //Server always uses last replicate tick as reconcile tick.
            uint tick = _lastReplicateTick;
            data.SetTick(tick);
            _lastReconcileTick = tick;

            PredictionManager.InvokeServerReconcile(this, true);
            SendReconcileRpc(methodHash, data, channel);
            PredictionManager.InvokeServerReconcile(this, false);

        }

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
            _lastReconcileTick = tick;
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
            _clientHasReconcileData = true;
            _lastReceivedReconcileTick = tick;
        }

    }
}