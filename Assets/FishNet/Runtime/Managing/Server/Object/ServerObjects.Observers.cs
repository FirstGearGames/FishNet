using FishNet.Component.Observing;
using FishNet.Connection;
using FishNet.Managing.Object;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Managing.Server
{
    public partial class ServerObjects : ManagedObjects
    {
        #region Private.
        /// <summary>
        /// Cache filled with objects which observers are being updated.
        /// This is primarily used to invoke events after all observers are updated, rather than as each is updated.
        /// </summary>
        private List<NetworkObject> _observerChangedObjectsCache = new List<NetworkObject>(100);
        /// <summary>
        /// NetworkObservers which require regularly iteration.
        /// </summary>
        private List<NetworkObject> _timedNetworkObservers = new List<NetworkObject>();
        /// <summary>
        /// Index in TimedNetworkObservers to start on next cycle.
        /// </summary>
        private int _nextTimedObserversIndex;
        /// <summary>
        /// HashGrid for the NetworkManager.
        /// </summary>
        private HashGrid _hashGrid;
        #endregion

        protected override void Initialize(NetworkManager manager)
        {
            base.Initialize(manager);
            _hashGrid = manager.GetInstance<HashGrid>(false);
        }

        /// <summary>
        /// Called when MonoBehaviours call Update.
        /// </summary>
        private void Observers_OnUpdate()
        {
            UpdateTimedObservers();
        }

        private float _nextUpdate;

        /// <summary>
        /// Progressively updates NetworkObservers with timed conditions.
        /// </summary>
        private void UpdateTimedObservers()
        {
            if (!base.NetworkManager.IsServer)
                return;
            //No point in updating if the timemanager isn't going to tick this frame.
            if (!base.NetworkManager.TimeManager.FrameTicked)
                return;
            int observersCount = _timedNetworkObservers.Count;
            if (observersCount == 0)
                return;

            ServerManager serverManager = base.NetworkManager.ServerManager;
            TransportManager transportManager = NetworkManager.TransportManager;

            /* Try to iterate all timed observers every half a second.
            * This value will increase as there's more observers or timed conditions. */
            double timeMultiplier = 1d + (float)((serverManager.Clients.Count * 0.005d) + (_timedNetworkObservers.Count * 0.0005d));
            double completionTime = (0.5d * timeMultiplier);
            uint completionTicks = base.NetworkManager.TimeManager.TimeToTicks(completionTime, TickRounding.RoundUp);
            /* Iterations will be the number of objects
             * to iterate to be have completed all objects by
             * the end of completionTicks. */
            int iterations = Mathf.CeilToInt((float)observersCount / (float)completionTicks);

            PooledWriter everyoneWriter = WriterPool.GetWriter(1000);
            PooledWriter ownerWriter = WriterPool.GetWriter(1000);
            PooledWriter largeWriter = WriterPool.GetWriter(1000);

            List<NetworkObject> observerChangedObjectCache = _observerChangedObjectsCache;
            int observerIndex = 0;
            //Index to perform a check on.
            foreach (NetworkConnection conn in serverManager.Clients.Values)
            {
                conn.UpdateHashGridPositions(false);
                largeWriter.Reset();
                //Reset index to start on for every connection.
                observerIndex = 0;
                /* Run the number of calculated iterations.
                 * This is spaced out over frames to prevent
                 * fps spikes. */
                for (int i = 0; i < iterations; i++)
                {
                    observerIndex = _nextTimedObserversIndex + i;
                    /* Compare actual collection size not cached value.
                     * This is incase collection is modified during runtime. */
                    if (observerIndex >= _timedNetworkObservers.Count)
                        observerIndex -= _timedNetworkObservers.Count;

                    /* If still out of bounds something whack is going on.
                    * Reset index and exit method. Let it sort itself out
                    * next iteration. */
                    if (observerIndex < 0 || observerIndex >= _timedNetworkObservers.Count)
                    {
                        _nextTimedObserversIndex = 0;
                        break;
                    }

                    NetworkObject nob = _timedNetworkObservers[observerIndex];
                    ObserverStateChange osc = nob.RebuildObservers(conn, true);
                    if (osc == ObserverStateChange.Added)
                    {
                        everyoneWriter.Reset();
                        ownerWriter.Reset();
                        base.WriteSpawn_Server(nob, conn, everyoneWriter, ownerWriter);
                        observerChangedObjectCache.Add(nob);
                    }
                    else if (osc == ObserverStateChange.Removed)
                    {
                        everyoneWriter.Reset();
                        WriteDespawn(nob, nob.GetDefaultDespawnType(), everyoneWriter);

                    }
                    else
                    {
                        continue;
                    }
                    /* Only use ownerWriter if an add, and if owner. Owner
                     * doesn't matter if not being added because no owner specific
                     * information would be included. */
                    PooledWriter writerToUse = (osc == ObserverStateChange.Added && nob.Owner == conn) ?
                        ownerWriter : everyoneWriter;

                    largeWriter.WriteArraySegment(writerToUse.GetArraySegment());
                }

                if (largeWriter.Length > 0)
                {
                    transportManager.SendToClient(
                        (byte)Channel.Reliable,
                        largeWriter.GetArraySegment(), conn);
                }

                //Invoke spawn callbacks on nobs.
                for (int i = 0; i < observerChangedObjectCache.Count; i++)
                    observerChangedObjectCache[i].InvokePostOnServerStart(conn);
                observerChangedObjectCache.Clear();
            }

            everyoneWriter.Dispose();
            ownerWriter.Dispose();
            largeWriter.Dispose();
            _nextTimedObserversIndex = (observerIndex + 1);
        }

        /// <summary>
        /// Indicates that a networkObserver component should be updated regularly. This is done automatically.
        /// </summary>
        /// <param name="networkObject">NetworkObject to be updated.</param>
        public void AddTimedNetworkObserver(NetworkObject networkObject)
        {
            _timedNetworkObservers.Add(networkObject);
        }

        /// <summary>
        /// Indicates that a networkObserver component no longer needs to be updated regularly. This is done automatically.
        /// </summary>
        /// <param name="networkObject">NetworkObject to be updated.</param>
        public void RemoveTimedNetworkObserver(NetworkObject networkObject)
        {
            _timedNetworkObservers.Remove(networkObject);
        }

        /// <summary>
        /// Removes a connection from observers without synchronizing changes.
        /// </summary>
        /// <param name="connection"></param>
        private void RemoveFromObserversWithoutSynchronization(NetworkConnection connection)
        {
            List<NetworkObject> observerChangedObjectsCache = _observerChangedObjectsCache;
            foreach (NetworkObject nob in Spawned.Values)
            {
                if (nob.RemoveObserver(connection))
                    observerChangedObjectsCache.Add(nob);
            }

            //Invoke despawn callbacks on nobs.
            for (int i = 0; i < observerChangedObjectsCache.Count; i++)
                observerChangedObjectsCache[i].InvokeOnServerDespawn(connection);
            observerChangedObjectsCache.Clear();
        }

        /// <summary>
        /// Rebuilds observers on all NetworkObjects for all connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers()
        {
            List<NetworkObject> nobCache = GetOrderedSpawnedObjects();
            List<NetworkConnection> connCache = CollectionCaches<NetworkConnection>.Retrieve();
            foreach (NetworkConnection conn in base.NetworkManager.ServerManager.Clients.Values)
                connCache.Add(conn);

            RebuildObservers(nobCache, connCache);
            CollectionCaches<NetworkObject>.Store(nobCache);
            CollectionCaches<NetworkConnection>.Store(connCache);
        }

        /// <summary>
        /// Rebuilds observers on NetworkObjects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkObject[] nobs)
        {
            int count = nobs.Length;
            for (int i = 0; i < count; i++)
                RebuildObservers(nobs[i]);
        }

        /// <summary>
        /// Rebuilds observers on NetworkObjects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(List<NetworkObject> nobs)
        {
            int count = nobs.Count;
            for (int i = 0; i < count; i++)
                RebuildObservers(nobs[i]);
        }

        /// <summary>
        /// Rebuilds observers on NetworkObjects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(List<NetworkObject> nobs, NetworkConnection conn)
        {
            RebuildObservers(nobs, conn, nobs.Count);
        }
        /// <summary>
        /// Rebuilds observers on NetworkObjects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(List<NetworkObject> nobs, List<NetworkConnection> conns)
        {
            int count = nobs.Count;
            for (int i = 0; i < count; i++)
                RebuildObservers(nobs[i], conns);
        }
        /// <summary>
        /// Rebuilds observers on all objects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkConnection[] connections)
        {
            int count = connections.Length;
            for (int i = 0; i < count; i++)
                RebuildObservers(connections[i]);
        }
        /// <summary>
        /// Rebuilds observers on all objects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(List<NetworkConnection> connections)
        {
            int count = connections.Count;
            for (int i = 0; i < count; i++)
                RebuildObservers(connections[i]);
        }

        /// <summary>
        /// Rebuilds observers on all NetworkObjects for a connection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkConnection connection)
        {
            List<NetworkObject> cache = GetOrderedSpawnedObjects();
            RebuildObservers(cache, connection);
            CollectionCaches<NetworkObject>.Store(cache);
        }

        /// <summary>
        /// Gets all spawned objects with root objects first.
        /// </summary>
        /// <returns></returns>
        private List<NetworkObject> GetOrderedSpawnedObjects()
        {
            List<NetworkObject> cache = CollectionCaches<NetworkObject>.Retrieve();
            foreach (NetworkObject networkObject in Spawned.Values)
            {
                if (networkObject.IsNested)
                    continue;

                //Add nob and children recursively.
                AddChildNetworkObjects(networkObject);
            }

            void AddChildNetworkObjects(NetworkObject n)
            {
                cache.Add(n);
                foreach (NetworkObject nob in n.ChildNetworkObjects)
                    AddChildNetworkObjects(nob);
            }

            return cache;
        }

        /// <summary>
        /// Rebuilds observers for a connection on NetworkObjects.
        /// </summary>               
        /// <param name="nobs">NetworkObjects to rebuild.</param>
        /// <param name="connection">Connection to rebuild for.</param>
        /// <param name="count">Number of iterations to perform collection. Entire collection is iterated when value is -1.</param>
        public void RebuildObservers(IEnumerable<NetworkObject> nobs, NetworkConnection connection, int count = -1)
        {
            PooledWriter everyoneWriter = WriterPool.GetWriter();
            PooledWriter ownerWriter = WriterPool.GetWriter();

            //If there's no limit on how many can be written set count to the maximum.
            if (count == -1)
                count = int.MaxValue;

            connection.UpdateHashGridPositions(true);
            List<NetworkObject> observerChangedObjectsCache = _observerChangedObjectsCache;

            int iterations;
            using (PooledWriter largeWriter = WriterPool.GetWriter())
            {
                iterations = 0;
                foreach (NetworkObject n in nobs)
                {
                    iterations++;
                    if (iterations > count)
                        break;

                    //If observer state changed then write changes.
                    ObserverStateChange osc = n.RebuildObservers(connection, false);
                    if (osc == ObserverStateChange.Added)
                    {
                        everyoneWriter.Reset();
                        ownerWriter.Reset();
                        base.WriteSpawn_Server(n, connection, everyoneWriter, ownerWriter);
                        observerChangedObjectsCache.Add(n);
                    }
                    else if (osc == ObserverStateChange.Removed)
                    {
                        connection.LevelOfDetails.Remove(n);
                        everyoneWriter.Reset();
                        WriteDespawn(n, n.GetDefaultDespawnType(), everyoneWriter);
                    }
                    else
                    {
                        continue;
                    }
                    /* Only use ownerWriter if an add, and if owner. Owner //cleanup see if rebuild timed and this can be joined or reuse methods.
                     * doesn't matter if not being added because no owner specific
                     * information would be included. */
                    PooledWriter writerToUse = (osc == ObserverStateChange.Added && n.Owner == connection) ?
                        ownerWriter : everyoneWriter;

                    largeWriter.WriteArraySegment(writerToUse.GetArraySegment());
                }

                if (largeWriter.Length > 0)
                {
                    NetworkManager.TransportManager.SendToClient(
                        (byte)Channel.Reliable,
                        largeWriter.GetArraySegment(), connection);
                }
            }

            //Dispose of writers created in this method.
            everyoneWriter.Dispose();
            ownerWriter.Dispose();

            //Invoke spawn callbacks on nobs.
            for (int i = 0; i < observerChangedObjectsCache.Count; i++)
                observerChangedObjectsCache[i].InvokePostOnServerStart(connection);
            observerChangedObjectsCache.Clear();
        }

        /// <summary>
        /// Rebuilds observers for connections on a NetworkObject.
        /// </summary>
        private void RebuildObservers(NetworkObject nob, List<NetworkConnection> conns)
        {
            PooledWriter everyoneWriter = WriterPool.GetWriter();
            PooledWriter ownerWriter = WriterPool.GetWriter();

            int count = conns.Count;
            for (int i = 0; i < count; i++)
            {
                NetworkConnection conn = conns[i];
                /* When not using a timed rebuild such as this connections must have
                 * hashgrid data rebuilt immediately. */
                conn.UpdateHashGridPositions(true);

                everyoneWriter.Reset();
                ownerWriter.Reset();
                //If observer state changed then write changes.
                ObserverStateChange osc = nob.RebuildObservers(conn, false);
                if (osc == ObserverStateChange.Added)
                {
                    base.WriteSpawn_Server(nob, conn, everyoneWriter, ownerWriter);
                }
                else if (osc == ObserverStateChange.Removed)
                {
                    conn.LevelOfDetails.Remove(nob);
                    WriteDespawn(nob, nob.GetDefaultDespawnType(), everyoneWriter);
                }
                else
                {
                    continue;
                }

                /* Only use ownerWriter if an add, and if owner. Owner
                 * doesn't matter if not being added because no owner specific
                 * information would be included. */
                PooledWriter writerToUse = (osc == ObserverStateChange.Added && nob.Owner == conn) ?
                    ownerWriter : everyoneWriter;

                if (writerToUse.Length > 0)
                {
                    NetworkManager.TransportManager.SendToClient(
                        (byte)Channel.Reliable,
                        writerToUse.GetArraySegment(), conn);

                    //If a spawn is being sent.
                    if (osc == ObserverStateChange.Added)
                        nob.InvokePostOnServerStart(conn);
                }

            }

            //Dispose of writers created in this method.
            everyoneWriter.Dispose();
            ownerWriter.Dispose();
        }


        /// <summary>
        /// Rebuilds observers for all connections for a NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkObject nob)
        {
            List<NetworkConnection> cache = CollectionCaches<NetworkConnection>.Retrieve();
            cache.AddRange(NetworkManager.ServerManager.Clients.Values);
            RebuildObservers(nob, cache);
            CollectionCaches<NetworkConnection>.Store(cache);
        }
        /// <summary>
        /// Rebuilds observers for a connection on NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RebuildObservers(NetworkObject nob, NetworkConnection conn)
        {
            List<NetworkConnection> cache = CollectionCaches<NetworkConnection>.Retrieve();
            cache.Add(conn);

            RebuildObservers(nob, cache);
            CollectionCaches<NetworkConnection>.Store(cache);
        }
        /// <summary>
        /// Rebuilds observers for connections on NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkObject networkObject, NetworkConnection[] connections)
        {
            List<NetworkConnection> cache = CollectionCaches<NetworkConnection>.Retrieve();
            cache.AddRange(connections);
            RebuildObservers(networkObject, cache);
            CollectionCaches<NetworkConnection>.Store(cache);
        }


    }

}