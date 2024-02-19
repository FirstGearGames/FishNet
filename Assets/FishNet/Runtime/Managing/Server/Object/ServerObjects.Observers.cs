using FishNet.Component.Observing;
using FishNet.Connection;
using FishNet.Managing.Object;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility;
using FishNet.Utility.Performance;
using GameKit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private PooledWriter _writer = new PooledWriter();
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<NetworkObject> RetrieveOrderedSpawnedObjects()
        {
            List<NetworkObject> cache = CollectionCaches<NetworkObject>.RetrieveList();

            bool initializationOrderChanged = false;
            //First order root objects.
            foreach (NetworkObject item in Spawned.Values)
                OrderRootByInitializationOrder(item, cache, ref initializationOrderChanged);

            OrderNestedByInitializationOrder(cache);

            return cache;
        }

        /// <summary>
        /// Orders a NetworkObject into a cache based on it's initialization order.
        /// Only non-nested NetworkObjects will be added.
        /// </summary>
        /// <param name="nob">NetworkObject to check.</param>
        /// <param name="cache">Cache to sort into.</param>
        /// <param name="initializationOrderChanged">Boolean to indicate if initialization order is specified for one or more objects.</param>
        private void OrderRootByInitializationOrder(NetworkObject nob, List<NetworkObject> cache, ref bool initializationOrderChanged)
        {
            if (nob.IsNested)
                return;

            sbyte currentItemInitOrder = nob.GetInitializeOrder();
            initializationOrderChanged |= (currentItemInitOrder != 0);
            int count = cache.Count;

            /* If initialize order has not changed or count is
             * 0 then add normally. */
            if (!initializationOrderChanged || count == 0)
            {
                cache.Add(nob);
            }
            else
            {
                /* If current item init order is larger or equal than
                 * the last entry in copy then add to the end.
                 * Otherwise check where to add from the beginning. */
                if (currentItemInitOrder >= cache[count - 1].GetInitializeOrder())
                {
                    cache.Add(nob);
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        /* If item being sorted is lower than the one in already added.
                         * then insert it before the one already added. */
                        if (currentItemInitOrder <= cache[i].GetInitializeOrder())
                        {
                            cache.Insert(i, nob);
                            break;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Orders nested NetworkObjects of cache by initialization order.
        /// </summary>
        /// <param name="cache">Cache to sort.</param>
        private void OrderNestedByInitializationOrder(List<NetworkObject> cache)
        {
            //After everything is sorted by root only insert children.
            for (int i = 0; i < cache.Count; i++)
            {
                NetworkObject nob = cache[i];
                //Skip root.
                if (nob.IsNested)
                    continue;

                int startingIndex = i;
                AddChildNetworkObjects(nob, ref startingIndex);
            }

            void AddChildNetworkObjects(NetworkObject n, ref int index)
            {
                foreach (NetworkObject childObject in n.ChildNetworkObjects)
                {
                    cache.Insert(++index, childObject);
                    AddChildNetworkObjects(childObject, ref index);
                }
            }
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

        #region Obsolete RebuildObservers.
        /// <summary>
        /// Rebuilds observers on NetworkObjects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use RebuildObservers IList variant instead.")]
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
        [Obsolete("Use RebuildObservers IList variant instead.")]
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
        [Obsolete("Use RebuildObservers IList variant instead.")]
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
        [Obsolete("Use RebuildObservers IList variant instead.")]
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
        [Obsolete("Use RebuildObservers IList variant instead.")]
        public void RebuildObservers(IEnumerable<NetworkObject> nobs, IEnumerable<NetworkConnection> conns, bool timedOnly = false)
        {
            List<NetworkObject> nobCache = CollectionCaches<NetworkObject>.RetrieveList();

            foreach (NetworkConnection nc in conns)
            {
                nobCache.Clear();

                foreach (NetworkObject nob in nobs)
                    RebuildObservers(nob, nc, nobCache, timedOnly);

                //Send if change.
                if (_writer.Length > 0)
                {
                    NetworkManager.TransportManager.SendToClient(
                        (byte)Channel.Reliable, _writer.GetArraySegment(), nc);
                    _writer.Reset();

                    foreach (NetworkObject n in nobCache)
                        n.OnSpawnServer(nc);
                }
            }

            CollectionCaches<NetworkObject>.Store(nobCache);
        }
        #endregion

        /// <summary>
        /// Rebuilds observers on NetworkObjects.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(IList<NetworkObject> nobs, bool timedOnly = false)
        {
            List<NetworkConnection> conns = RetrieveAuthenticatedConnections();

            RebuildObservers(nobs, conns, timedOnly);

            CollectionCaches<NetworkConnection>.Store(conns);
        }
        /// <summary>
        /// Rebuilds observers on all objects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(IList<NetworkConnection> connections, bool timedOnly = false)
        {
            List<NetworkObject> nobCache = RetrieveOrderedSpawnedObjects();

            RebuildObservers(nobCache, connections, timedOnly);

            CollectionCaches<NetworkObject>.Store(nobCache);
        }

        /// <summary>
        /// Rebuilds observers on NetworkObjects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(IList<NetworkObject> nobs, NetworkConnection conn, bool timedOnly = false)
        {
            List<NetworkConnection> connCache = CollectionCaches<NetworkConnection>.RetrieveList(conn);

            RebuildObservers(nobs, connCache, timedOnly);

            CollectionCaches<NetworkConnection>.Store(connCache);
        }

        /// <summary>
        /// Rebuilds observers for connections on NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkObject networkObject, IList<NetworkConnection> connections, bool timedOnly = false)
        {
            List<NetworkObject> nobCache = CollectionCaches<NetworkObject>.RetrieveList(networkObject);

            RebuildObservers(nobCache, connections, timedOnly);

            CollectionCaches<NetworkObject>.Store(nobCache);
        }

        /// <summary>
        /// Rebuilds observers on NetworkObjects for connections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(IList<NetworkObject> nobs, IList<NetworkConnection> conns, bool timedOnly = false)
        {
            List<NetworkObject> nobCache = CollectionCaches<NetworkObject>.RetrieveList();
            NetworkConnection nc;

            int connsCount = conns.Count;
            for (int i = 0; i < connsCount; i++)
            {
                nobCache.Clear();

                nc = conns[i];
                int nobsCount = nobs.Count;
                for (int z = 0; z < nobsCount; z++)
                    RebuildObservers(nobs[z], nc, nobCache, timedOnly);

                //Send if change.
                if (_writer.Length > 0)
                {
                    NetworkManager.TransportManager.SendToClient(
                        (byte)Channel.Reliable, _writer.GetArraySegment(), nc);
                    _writer.Reset();

                    foreach (NetworkObject n in nobCache)
                        n.OnSpawnServer(nc);
                }
            }

            CollectionCaches<NetworkObject>.Store(nobCache);
        }


        /// <summary>
        /// Rebuilds observers for a connection on NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RebuildObservers(NetworkObject nob, NetworkConnection conn, bool timedOnly = false)
        {
            if (ApplicationState.IsQuitting())
                return;
            _writer.Reset();

            /* When not using a timed rebuild such as this connections must have
             * hashgrid data rebuilt immediately. */
            //            if (!timedOnly)
            conn.UpdateHashGridPositions(!timedOnly);

            //If observer state changed then write changes.
            ObserverStateChange osc = nob.RebuildObservers(conn, timedOnly);
            if (osc == ObserverStateChange.Added)
            {
                base.WriteSpawn_Server(nob, conn, _writer);
            }
            else if (osc == ObserverStateChange.Removed)
            {
                if (conn.LevelOfDetails.TryGetValue(nob, out NetworkConnection.LevelOfDetailData lodData))
                    ObjectCaches<NetworkConnection.LevelOfDetailData>.Store(lodData);
                conn.LevelOfDetails.Remove(nob);
                WriteDespawn(nob, nob.GetDefaultDespawnType(), _writer);
            }
            else
            {
                return;
            }

            NetworkManager.TransportManager.SendToClient(
                (byte)Channel.Reliable,
                _writer.GetArraySegment(), conn);

            /* If spawning then also invoke server
             * start events, such as buffer last
             * and onspawnserver. */
            if (osc == ObserverStateChange.Added)
                nob.OnSpawnServer(conn);

            /* If there is change then also rebuild on any runtime children.
             * This is to ensure runtime children have visibility updated
             * in relation to parent. 
             *
             * If here there is change. */
            foreach (NetworkObject item in nob.RuntimeChildNetworkObjects)
                RebuildObservers(item, conn, timedOnly);
        }

        /// <summary>
        /// Rebuilds observers for a connection on NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RebuildObservers(NetworkObject nob, NetworkConnection conn, List<NetworkObject> addedNobs, bool timedOnly = false)
        {
            if (ApplicationState.IsQuitting())
                return;

            /* When not using a timed rebuild such as this connections must have
             * hashgrid data rebuilt immediately. */
            //if (!timedOnly)
            //conn.UpdateHashGridPositions(true);
            conn.UpdateHashGridPositions(!timedOnly);

            //If observer state changed then write changes.
            ObserverStateChange osc = nob.RebuildObservers(conn, timedOnly);
            if (osc == ObserverStateChange.Added)
            {
                base.WriteSpawn_Server(nob, conn, _writer);
                addedNobs.Add(nob);
            }
            else if (osc == ObserverStateChange.Removed)
            {
                if (conn.LevelOfDetails.TryGetValue(nob, out NetworkConnection.LevelOfDetailData lodData))
                    ObjectCaches<NetworkConnection.LevelOfDetailData>.Store(lodData);
                conn.LevelOfDetails.Remove(nob);
                WriteDespawn(nob, nob.GetDefaultDespawnType(), _writer);
            }
            else
            {
                return;
            }

            /* If there is change then also rebuild on any runtime children.
             * This is to ensure runtime children have visibility updated
             * in relation to parent. 
             *
             * If here there is change. */
            foreach (NetworkObject item in nob.RuntimeChildNetworkObjects)
                RebuildObservers(item, conn, addedNobs, timedOnly);
        }




    }

}