using FishNet.Component.Observing;
using FishNet.Connection;
using FishNet.Managing.Object;
using FishNet.Managing.Timing;
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
        /// Used to write spawns for everyone. This writer will exclude owner only information.
        /// </summary>
        private PooledWriter _everyoneWriter = new PooledWriter();
        /// <summary>
        /// Used to write spawns for owner. This writer may contain owner only information.
        /// </summary>
        private PooledWriter _ownerWriter = new PooledWriter();
        #endregion


        /// <summary>
        /// Called when MonoBehaviours call Update.
        /// </summary>
        private void Observers_OnUpdate()
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
            int networkObserversCount = _timedNetworkObservers.Count;
            if (networkObserversCount == 0)
                return;

            /* Try to iterate all timed observers every half a second.
            * This value will increase as there's more observers or timed conditions. */
            double timeMultiplier = 1d + (float)((base.NetworkManager.ServerManager.Clients.Count * 0.005d) + (_timedNetworkObservers.Count * 0.0005d));
            double completionTime = (0.5d * timeMultiplier);
            uint completionTicks = base.NetworkManager.TimeManager.TimeToTicks(completionTime, TickRounding.RoundUp);
            /* Iterations will be the number of objects
             * to iterate to be have completed all objects by
             * the end of completionTicks. */
            int iterations = Mathf.CeilToInt((float)networkObserversCount / (float)completionTicks);
            if (iterations > _timedNetworkObservers.Count)
                iterations = _timedNetworkObservers.Count;

            List<NetworkConnection> connCache = RetrieveAuthenticatedConnections();
            //Build nob cache.
            List<NetworkObject> nobCache = CollectionCaches<NetworkObject>.RetrieveList();
            for (int i = 0; i < iterations; i++)
            {
                if (_nextTimedObserversIndex >= _timedNetworkObservers.Count)
                    _nextTimedObserversIndex = 0;
                nobCache.Add(_timedNetworkObservers[_nextTimedObserversIndex++]);
            }

            RebuildObservers(nobCache, connCache, true);

            CollectionCaches<NetworkConnection>.Store(connCache);
            CollectionCaches<NetworkObject>.Store(nobCache);
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
        /// Gets all NetworkConnections which are authenticated.
        /// </summary>
        /// <returns></returns>
        private List<NetworkConnection> RetrieveAuthenticatedConnections()
        {
            List<NetworkConnection> cache = CollectionCaches<NetworkConnection>.RetrieveList();
            foreach (NetworkConnection item in NetworkManager.ServerManager.Clients.Values)
            {
                if (item.Authenticated)
                    cache.Add(item);
            }

            return cache;
        }

        /// <summary>
        /// Gets all spawned objects with root objects first.
        /// </summary>
        /// <returns></returns>
        private List<NetworkObject> RetrieveOrderedSpawnedObjects()
        {
            List<NetworkObject> cache = CollectionCaches<NetworkObject>.RetrieveList();
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
        public void RebuildObservers(bool timedOnly = false)
        {
            List<NetworkObject> nobCache = RetrieveOrderedSpawnedObjects();
            List<NetworkConnection> connCache = RetrieveAuthenticatedConnections();

            RebuildObservers(nobCache, connCache, timedOnly);

            CollectionCaches<NetworkObject>.Store(nobCache);
            CollectionCaches<NetworkConnection>.Store(connCache);
        }


        /// <summary>
        /// Rebuilds observers for all connections for a NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkObject nob, bool timedOnly = false)
        {
            List<NetworkObject> nobCache = CollectionCaches<NetworkObject>.RetrieveList(nob);
            List<NetworkConnection> connCache = RetrieveAuthenticatedConnections();

            RebuildObservers(nobCache, connCache, timedOnly);

            CollectionCaches<NetworkObject>.Store(nobCache);
            CollectionCaches<NetworkConnection>.Store(connCache);
        }
        /// <summary>
        /// Rebuilds observers on all NetworkObjects for a connection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkConnection connection, bool timedOnly = false)
        {
            List<NetworkObject> nobCache = RetrieveOrderedSpawnedObjects();
            List<NetworkConnection> connCache = CollectionCaches<NetworkConnection>.RetrieveList(connection);

            RebuildObservers(nobCache, connCache, timedOnly);

            CollectionCaches<NetworkObject>.Store(nobCache);
            CollectionCaches<NetworkConnection>.Store(connCache);
        }

        /// <summary>
        /// Rebuilds observers on NetworkObjects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(IEnumerable<NetworkObject> nobs, bool timedOnly = false)
        {
            List<NetworkConnection> conns = RetrieveAuthenticatedConnections();

            RebuildObservers(nobs, conns, timedOnly);

            CollectionCaches<NetworkConnection>.Store(conns);
        }
        /// <summary>
        /// Rebuilds observers on all objects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(IEnumerable<NetworkConnection> connections, bool timedOnly = false)
        {
            List<NetworkObject> nobCache = RetrieveOrderedSpawnedObjects();

            RebuildObservers(nobCache, connections, timedOnly);

            CollectionCaches<NetworkObject>.Store(nobCache);
        }

        /// <summary>
        /// Rebuilds observers on NetworkObjects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(IEnumerable<NetworkObject> nobs, NetworkConnection conn, bool timedOnly = false)
        {
            List<NetworkConnection> connCache = CollectionCaches<NetworkConnection>.RetrieveList(conn);

            RebuildObservers(nobs, connCache, timedOnly);

            CollectionCaches<NetworkConnection>.Store(connCache);
        }

        /// <summary>
        /// Rebuilds observers for connections on NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkObject networkObject, IEnumerable<NetworkConnection> connections, bool timedOnly = false)
        {
            List<NetworkObject> nobCache = CollectionCaches<NetworkObject>.RetrieveList(networkObject);

            RebuildObservers(nobCache, connections, timedOnly);

            CollectionCaches<NetworkObject>.Store(nobCache);
        }

        /// <summary>
        /// Rebuilds observers on NetworkObjects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(IEnumerable<NetworkObject> nobs, IEnumerable<NetworkConnection> conns, bool timedOnly = false)
        {
            foreach (NetworkObject nob in nobs)
            {
                foreach (NetworkConnection nc in conns)
                    RebuildObservers(nob, nc, timedOnly);
            }
        }

        /// <summary>
        /// Rebuilds observers for a connection on NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RebuildObservers(NetworkObject nob, NetworkConnection conn, bool timedOnly = false)
        {
            _everyoneWriter.Reset();
            _ownerWriter.Reset();

            /* When not using a timed rebuild such as this connections must have
             * hashgrid data rebuilt immediately. */
            if (!timedOnly)
                conn.UpdateHashGridPositions(true);

            //If observer state changed then write changes.
            ObserverStateChange osc = nob.RebuildObservers(conn, timedOnly);
            if (osc == ObserverStateChange.Added)
            {
                base.WriteSpawn_Server(nob, conn, _everyoneWriter, _ownerWriter);
            }
            else if (osc == ObserverStateChange.Removed)
            {
                conn.LevelOfDetails.Remove(nob);
                WriteDespawn(nob, nob.GetDefaultDespawnType(), _everyoneWriter);
            }
            else
            {
                return;
            }

            /* Only use ownerWriter if an add, and if owner. Owner
             * doesn't matter if not being added because no owner specific
             * information would be included. */
            PooledWriter writerToUse = (osc == ObserverStateChange.Added && nob.Owner == conn) ?
                _ownerWriter : _everyoneWriter;

            if (writerToUse.Length > 0)
            {
                NetworkManager.TransportManager.SendToClient(
                    (byte)Channel.Reliable,
                    writerToUse.GetArraySegment(), conn);

                //If a spawn is being sent.
                if (osc == ObserverStateChange.Added)
                    nob.InvokePostOnServerStart(conn);
            }

            /* If there is change then also rebuild on any runtime children.
             * This is to ensure runtime children have visibility updated
             * in relation to parent. 
             *
             * If here there is change. */
            foreach (NetworkObject item in nob.RuntimeChildNetworkObjects)
                RebuildObservers(item, conn, false);
        }



    }

}