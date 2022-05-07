using FishNet.Connection;
using FishNet.Managing.Object;
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
        /// Cache filled with objects which are being spawned on clients due to an observer change.
        /// </summary>
        private List<NetworkObject> _observerChangeObjectsCache = new List<NetworkObject>(100);
        /// <summary>
        /// NetworkObservers which require regularly iteration.
        /// </summary>
        private List<NetworkObject> _timedNetworkObservers = new List<NetworkObject>();
        /// <summary>
        /// Index in TimedNetworkObservers to start on next cycle.
        /// </summary>
        private int _nextTimedObserversIndex;
        #endregion

        /// <summary>
        /// Called when MonoBehaviours call Update.
        /// </summary>
        partial void PartialOnUpdate()
        {
            UpdateTimedObservers();
        }

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
             * This value will increase as there's more observers. */
            int completionTicks = Mathf.Max(1, (base.NetworkManager.TimeManager.TickRate * 2));
            /* Multiply required ticks based on connection count and nob count. This will
             * reduce how quickly observers update slightly but will drastically
             * improve performance. */
            float tickMultiplier = 1f + (float)(
                (serverManager.Clients.Count * 0.005f) +
                (serverManager.Objects.Spawned.Count * 0.0005f)
                );
            /* Add an additional iteration to prevent
             * 0 iterations */
            int iterations = (observersCount / (int)(completionTicks * tickMultiplier)) + 1;
            if (iterations > observersCount)
                iterations = observersCount;


            PooledWriter everyoneWriter = WriterPool.GetWriter();
            PooledWriter ownerWriter = WriterPool.GetWriter();

            //Index to perform a check on.
            int observerIndex = 0;
            foreach (NetworkConnection conn in serverManager.Clients.Values)
            {

                int cacheIndex = 0;
                using (PooledWriter largeWriter = WriterPool.GetWriter())
                {
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
                            WriteSpawn(nob, conn, ref everyoneWriter, ref ownerWriter);
                            CacheObserverChange(nob, ref cacheIndex);
                        }
                        else if (osc == ObserverStateChange.Removed)
                        {
                            everyoneWriter.Reset();
                            WriteDespawn(nob, ref everyoneWriter);
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
                    for (int i = 0; i < cacheIndex; i++)
                        _observerChangeObjectsCache[i].InvokePostOnServerStart(conn);
                }
            }

            everyoneWriter.Dispose();
            ownerWriter.Dispose();
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
        /// Caches an observer change.
        /// </summary>
        /// <param name="cacheIndex"></param>
        private void CacheObserverChange(NetworkObject nob, ref int cacheIndex)
        {
            /* If this spawn would exceed cache size then
            * add instead of set value. */
            if (_observerChangeObjectsCache.Count <= cacheIndex)
                _observerChangeObjectsCache.Add(nob);
            else
                _observerChangeObjectsCache[cacheIndex] = nob;

            cacheIndex++;
        }

        /// <summary>
        /// Removes a connection from observers without synchronizing changes.
        /// </summary>
        /// <param name="connection"></param>
        private void RemoveFromObserversWithoutSynchronization(NetworkConnection connection)
        {
            int cacheIndex = 0;

            foreach (NetworkObject nob in Spawned.Values)
            {
                if (nob.RemoveObserver(connection))
                    CacheObserverChange(nob, ref cacheIndex);
            }

            //Invoke despawn callbacks on nobs.
            for (int i = 0; i < cacheIndex; i++)
                _observerChangeObjectsCache[i].InvokeOnServerDespawn(connection);
        }

        /// <summary>
        /// Rebuilds observers on all objects for all connections.
        /// </summary>
        public void RebuildObservers()
        {
            ListCache<NetworkObject> nobCache = ListCaches.GetNetworkObjectCache();
            foreach (NetworkObject nob in Spawned.Values)
                nobCache.AddValue(nob);
            ListCache<NetworkConnection> connCache = ListCaches.GetNetworkConnectionCache();
            foreach (NetworkConnection conn in base.NetworkManager.ServerManager.Clients.Values)
                connCache.AddValue(conn);

            RebuildObservers(nobCache, connCache);
            ListCaches.StoreCache(nobCache);
            ListCaches.StoreCache(connCache);
        }

        /// <summary>
        /// Rebuilds observers on objects.
        /// </summary>
        /// <param name="connection"></param>
        public void RebuildObservers(NetworkObject[] nobs)
        {
            int count = nobs.Length;
            for (int i = 0; i < count; i++)
                RebuildObservers(nobs[i]);
        }

        /// <summary>
        /// Rebuilds observers on objects.
        /// </summary>
        /// <param name="connection"></param>
        public void RebuildObservers(List<NetworkObject> nobs)
        {
            int count = nobs.Count;
            for (int i = 0; i < count; i++)
                RebuildObservers(nobs[i]);
        }

        /// <summary>
        /// Rebuilds observers on objects.
        /// </summary>
        public void RebuildObservers(ListCache<NetworkObject> nobs)
        {
            int count = nobs.Written;
            List<NetworkObject> collection = nobs.Collection;
            for (int i = 0; i < count; i++)
                RebuildObservers(collection[i]);
        }
        /// <summary>
        /// Rebuilds observers on objects.
        /// </summary>
        public void RebuildObservers(ListCache<NetworkObject> nobs, ListCache<NetworkConnection> conns)
        {
            int count = nobs.Written;
            List<NetworkObject> collection = nobs.Collection;
            for (int i = 0; i < count; i++)
                RebuildObservers(collection[i], conns);
        }
        /// <summary>
        /// Rebuilds observers on all objects for a connections.
        /// </summary>
        /// <param name="connection"></param>
        public void RebuildObservers(ListCache<NetworkConnection> connections)
        {
            int count = connections.Written;
            List<NetworkConnection> collection = connections.Collection;
            for (int i = 0; i < count; i++)
                RebuildObservers(collection[i]);
        }
        /// <summary>
        /// Rebuilds observers on all objects for a connections.
        /// </summary>
        /// <param name="connection"></param>
        public void RebuildObservers(NetworkConnection[] connections)
        {
            int count = connections.Length;
            for (int i = 0; i < count; i++)
                RebuildObservers(connections[i]);
        }
        /// <summary>
        /// Rebuilds observers on all objects for a connections.
        /// </summary>
        /// <param name="connection"></param>
        public void RebuildObservers(List<NetworkConnection> connections)
        {
            int count = connections.Count;
            for (int i = 0; i < count; i++)
                RebuildObservers(connections[i]);
        }
        /// <summary>
        /// Rebuilds observers on all objects for a connection.
        /// </summary>
        /// <param name="connection"></param>
        public void RebuildObservers(NetworkConnection connection)
        {
            PooledWriter everyoneWriter = WriterPool.GetWriter();
            PooledWriter ownerWriter = WriterPool.GetWriter();

            int observerCacheIndex = 0;
            using (PooledWriter largeWriter = WriterPool.GetWriter())
            {
                observerCacheIndex = 0;
                foreach (NetworkObject nob in Spawned.Values)
                {
                    //If observer state changed then write changes.
                    ObserverStateChange osc = nob.RebuildObservers(connection, false);
                    if (osc == ObserverStateChange.Added)
                    {
                        everyoneWriter.Reset();
                        ownerWriter.Reset();
                        WriteSpawn(nob, connection, ref everyoneWriter, ref ownerWriter);
                        CacheObserverChange(nob, ref observerCacheIndex);
                    }
                    else if (osc == ObserverStateChange.Removed)
                    {
                        everyoneWriter.Reset();
                        WriteDespawn(nob, ref everyoneWriter);
                    }
                    else
                    {
                        continue;
                    }
                    /* Only use ownerWriter if an add, and if owner. Owner //cleanup see if rebuild timed and this can be joined or reuse methods.
                     * doesn't matter if not being added because no owner specific
                     * information would be included. */
                    PooledWriter writerToUse = (osc == ObserverStateChange.Added && nob.Owner == connection) ?
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
            for (int i = 0; i < observerCacheIndex; i++)
                _observerChangeObjectsCache[i].InvokePostOnServerStart(connection);
        }

        /// <summary>
        /// Rebuilds observers for cached connections for a NetworkObject.
        /// </summary>
        private void RebuildObservers(NetworkObject networkObject, ListCache<NetworkConnection> cache)
        {
            PooledWriter everyoneWriter = WriterPool.GetWriter();
            PooledWriter ownerWriter = WriterPool.GetWriter();

            int written = cache.Written;
            for (int i = 0; i < written; i++)
            {
                NetworkConnection conn = cache.Collection[i];

                everyoneWriter.Reset();
                ownerWriter.Reset();
                //If observer state changed then write changes.
                ObserverStateChange osc = networkObject.RebuildObservers(conn, false);
                if (osc == ObserverStateChange.Added)
                    WriteSpawn(networkObject, conn, ref everyoneWriter, ref ownerWriter);
                else if (osc == ObserverStateChange.Removed)
                    WriteDespawn(networkObject, ref everyoneWriter);
                else
                    continue;

                /* Only use ownerWriter if an add, and if owner. Owner
                 * doesn't matter if not being added because no owner specific
                 * information would be included. */
                PooledWriter writerToUse = (osc == ObserverStateChange.Added && networkObject.Owner == conn) ?
                    ownerWriter : everyoneWriter;

                if (writerToUse.Length > 0)
                {
                    NetworkManager.TransportManager.SendToClient(
                        (byte)Channel.Reliable,
                        writerToUse.GetArraySegment(), conn);

                    //If a spawn is being sent.
                    if (osc == ObserverStateChange.Added)
                        networkObject.InvokePostOnServerStart(conn);
                }

            }

            //Dispose of writers created in this method.
            everyoneWriter.Dispose();
            ownerWriter.Dispose();
        }


        /// <summary>
        /// Rebuilds observers for all connections for a NetworkObject.
        /// </summary>
        /// <param name="nob">NetworkObject to rebuild on.</param>
        internal void RebuildObservers(NetworkObject nob)
        {
            ListCache<NetworkConnection> cache = ListCaches.GetNetworkConnectionCache();
            foreach (NetworkConnection item in NetworkManager.ServerManager.Clients.Values)
                cache.AddValue(item);

            RebuildObservers(nob, cache);
            ListCaches.StoreCache(cache);
        }
        /// <summary>
        /// Rebuilds observers for a connection on NetworkObject.
        /// </summary>
        /// <param name="nob">NetworkObject to rebuild on.</param>
        /// <param name="conn">Connection to rebuild for.</param>
        internal void RebuildObservers(NetworkObject nob, NetworkConnection conn)
        {
            ListCache<NetworkConnection> cache = ListCaches.GetNetworkConnectionCache();
            cache.AddValue(conn);

            RebuildObservers(nob, cache);
            ListCaches.StoreCache(cache);
        }
        /// <summary>
        /// Rebuilds observers for connections on NetworkObject.
        /// </summary>
        /// <param name="networkObject">NetworkObject to rebuild on.</param>
        /// <param name="connections">Connections to rebuild for.
        public void RebuildObservers(NetworkObject networkObject, NetworkConnection[] connections)
        {
            ListCache<NetworkConnection> cache = ListCaches.GetNetworkConnectionCache();
            cache.AddValues(connections);
            RebuildObservers(networkObject, cache);
            ListCaches.StoreCache(cache);
        }

        /// <summary>
        /// Rebuilds observers for connections on NetworkObject.
        /// </summary>
        /// <param name="networkObject">NetworkObject to rebuild on.</param>
        /// <param name="connections">Connections to rebuild for.
        public void RebuildObservers(NetworkObject networkObject, List<NetworkConnection> connections)
        {
            ListCache<NetworkConnection> cache = ListCaches.GetNetworkConnectionCache();
            cache.AddValues(connections);
            RebuildObservers(networkObject, cache);
            ListCaches.StoreCache(cache);
        }



    }

}