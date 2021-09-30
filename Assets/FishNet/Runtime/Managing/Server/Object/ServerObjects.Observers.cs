using FishNet.Managing.Object;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using FishNet.Utility;
using UnityEngine;

namespace FishNet.Managing.Server.Object
{
    public partial class ServerObjects : ManagedObjects
    {
        #region Private.
        /// <summary>
        /// Cache for NetworkConnections.
        /// </summary>
        private ListCache<NetworkConnection> _networkConnectionListCache = new ListCache<NetworkConnection>();
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
        private int _nextTimedObserversIndex = 0;
        #endregion

        /// <summary>
        /// Called when MonoBehaviours call Update.
        /// </summary>
        private void TimeManager_OnUpdate()
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
            int observersCount = _timedNetworkObservers.Count;
            if (observersCount == 0)
                return;

            int targetFps = 60;
            /* Multiply required frames based on connection count. This will
             * reduce how quickly observers update slightly but will drastically
             * improve performance. */
            float fpsMultiplier = 1f + (float)(base.NetworkManager.ServerManager.Clients.Count * 0.01f);
            /* Performing one additional iteration would
            * likely be quicker than casting two ints
            * to a float. */
            int iterations = (observersCount / (int)(targetFps * fpsMultiplier)) + 1;
            if (iterations > observersCount)
                iterations = observersCount;


            PooledWriter everyoneWriter = WriterPool.GetWriter();
            PooledWriter ownerWriter = WriterPool.GetWriter();

            //Index to perform a check on.
            int observerIndex = 0;
            foreach (NetworkConnection conn in base.NetworkManager.ServerManager.Clients.Values)
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
                        ObserverStateChange osc = nob.RebuildObservers(conn);
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
                        NetworkManager.TransportManager.SendToClient(
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
        /// Adds networkObserver to be updated regularly.
        /// </summary>
        /// <param name="networkObject"></param>
        public void AddTimedNetworkObserver(NetworkObject networkObject)
        {
            _timedNetworkObservers.Add(networkObject);
        }

        /// <summary>
        /// Removes networkObserver from being updated regularly.
        /// </summary>
        /// <param name="networkObject"></param>
        public void RemoveTimedNetworkObserver(NetworkObject networkObject)
        {
            _timedNetworkObservers.Remove(networkObject);
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        private void InitializeObservers()
        {
            base.NetworkManager.TimeManager.OnUpdate += TimeManager_OnUpdate;
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
        /// Rebuilds observers on all objects for a connection.
        /// </summary>
        /// <param name="connection"></param>
        internal void RebuildObservers(NetworkConnection connection)
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
                    ObserverStateChange osc = nob.RebuildObservers(connection);
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
                    /* Only use ownerWriter if an add, and if owner. Owner //todo see if rebuild timed and this can be joined or reuse methods.
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
        /// Rebuilds observers on all connections for a NetworkObject.
        /// </summary>
        /// <param name="nob">NetworkObject to rebuild on.</param>
        internal void RebuildObservers(NetworkObject nob)
        {
            RebuildObservers(nob, null);
        }

        /// <summary>
        /// Rebuilds observers on all connections for a NetworkObject.
        /// </summary>
        /// <param name="networkObject">NetworkObject to rebuild on.</param>
        /// <param name="connections">Connections to rebuild for. If null networkObject will rebuild for all connections.</param>
        internal void RebuildObservers(NetworkObject networkObject, NetworkConnection[] connections)
        {
            PooledWriter everyoneWriter = WriterPool.GetWriter();
            PooledWriter ownerWriter = WriterPool.GetWriter();

            _networkConnectionListCache.Reset();
            if (connections == null)
            {
                foreach (NetworkConnection item in NetworkManager.ServerManager.Clients.Values)
                    _networkConnectionListCache.AddValue(item);
            }
            else
            {
                _networkConnectionListCache.AddValues(connections);
            }


            for (int i = 0; i < _networkConnectionListCache.Written; i++)
            {
                NetworkConnection conn = _networkConnectionListCache.Collection[i];
                /* If connection is nobs owner then skip it. Owner will always
                 * have visibility of the object and they will be given
                 * visibility when they gain ownership. By checking here
                 * as well it would cause an object spawn to be sent twice.
                 * This is a bug because the visibility should be unchanged when giving
                 * ownership, assuming conditions are met here. Need to look into that. 
                 *
                 * This code has been commented out because the issue was resolved by moving the
                 * OnClientAuthenticated event after Objects.ClientAuthenticated.
                 * The reason it was creating problems is because the PlayerSpawner demo was spawning
                 * using OnClientAuthenticated which was occurring before networked objects were
                 * spawned for the authenticating client. Therefor PlayerSpawner was spawning an object
                 * immediately, then Objects.ClientAuthenticated was spawning it again for the player. */

                everyoneWriter.Reset();
                ownerWriter.Reset();
                //If observer state changed then write changes.
                ObserverStateChange osc = networkObject.RebuildObservers(conn);
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



    }

}