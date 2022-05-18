using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Observing
{
    /// <summary>
    /// When this observer condition is placed on an object, a client must be within the same match to view the object.
    /// </summary>
    [CreateAssetMenu(menuName = "FishNet/Observers/Match Condition", fileName = "New Match Condition")]
    public class MatchCondition : ObserverCondition
    {
        #region Private.
        /// <summary>
        /// 
        /// </summary>
        private static Dictionary<int, HashSet<NetworkConnection>> _matchConnections = new Dictionary<int, HashSet<NetworkConnection>>();
        /// <summary>
        /// Matches and connections in each match.
        /// </summary>
        public static IReadOnlyDictionary<int, HashSet<NetworkConnection>> MatchConnections => _matchConnections;
        /// <summary>
        /// 
        /// </summary>
        /// //todo this needs to hold hashset so conns can be in multiple matches.
        private static Dictionary<NetworkConnection, int> _connectionMatch = new Dictionary<NetworkConnection, int>();
        /// <summary>
        /// Match a connection is in.
        /// </summary>
        public static IReadOnlyDictionary<NetworkConnection, int> ConnectionMatch => _connectionMatch;
        /// <summary>
        /// 
        /// </summary>
        private static Dictionary<int, HashSet<NetworkObject>> _matchObjects = new Dictionary<int, HashSet<NetworkObject>>();
        /// <summary>
        /// Matches and connections in each match.
        /// </summary>
        public static IReadOnlyDictionary<int, HashSet<NetworkObject>> MatchObjects => _matchObjects;
        /// <summary>
        /// 
        /// </summary>
        /// //todo this needs to hold hashset so conns can be in multiple matches.
        private static Dictionary<NetworkObject, int> _objectMatch = new Dictionary<NetworkObject, int>();
        /// <summary>
        /// Match a connection is in.
        /// </summary>
        public static IReadOnlyDictionary<NetworkObject, int> ObjectMatch => _objectMatch;
        #endregion

        public void ConditionConstructor() { }

        #region Add to match NetworkConnection.
        /// <summary>
        /// Adds conn to match.
        /// </summary>
        /// <param name="match">Match to add conn to.</param>
        /// <param name="conn">Connection to add to match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        /// <param name="replaceMatch">True to replace other matches with the new match.</param>
        public static void AddToMatch(int match, NetworkConnection conn, NetworkManager manager = null, bool replaceMatch = false)
        {
            if (replaceMatch)
                RemoveFromMatchWithoutRebuild(conn, manager);

            HashSet<NetworkConnection> results;
            if (!_matchConnections.TryGetValueIL2CPP(match, out results))
            {
                results = new HashSet<NetworkConnection>();
                _matchConnections.Add(match, results);
            }

            bool r = results.Add(conn);
            _connectionMatch[conn] = match;
            if (r)
                FinalizeChange(match, results, manager);
        }
        /// <summary>
        /// Adds conns to match.
        /// </summary>
        /// <param name="match">Match to add conns to.</param>
        /// <param name="conns">Connections to add to match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        /// <param name="replaceMatch">True to replace other matches with the new match.</param>
        public static void AddToMatch(int match, NetworkConnection[] conns, NetworkManager manager = null, bool replaceMatch = false)
        {
            AddToMatch(match, conns.ToList(), manager, replaceMatch);
        }
        /// <summary>
        /// Adds conns to match.
        /// </summary>
        /// <param name="match">Match to add conns to.</param>
        /// <param name="conns">Connections to add to match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        /// <param name="replaceMatch">True to replace other matches with the new match.</param>
        public static void AddToMatch(int match, List<NetworkConnection> conns, NetworkManager manager = null, bool replaceMatch = false)
        {
            if (replaceMatch)
            {
                foreach (NetworkConnection nc in conns)
                    RemoveFromMatchWithoutRebuild(nc, manager);
            }

            HashSet<NetworkConnection> results;
            if (!_matchConnections.TryGetValueIL2CPP(match, out results))
            {
                results = new HashSet<NetworkConnection>();
                _matchConnections.Add(match, results);
            }

            bool r = false;
            for (int i = 0; i < conns.Count; i++)
            {
                NetworkConnection c = conns[i];
                r |= results.Add(c);
                _connectionMatch[c] = match;
            }

            if (r)
                FinalizeChange(match, results, manager);
        }
        #endregion

        #region Add to match NetworkObject.
        /// <summary>
        /// Adds conn to match.
        /// </summary>
        /// <param name="match">Match to add conn to.</param>
        /// <param name="nob">Connection to add to match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        /// <param name="replaceMatch">True to replace other matches with the new match.</param>
        public static void AddToMatch(int match, NetworkObject nob, NetworkManager manager = null, bool replaceMatch = false)
        {
            if (replaceMatch)
                RemoveFromMatchWithoutRebuild(nob, manager);

            HashSet<NetworkObject> results;
            if (!_matchObjects.TryGetValueIL2CPP(match, out results))
            {
                results = new HashSet<NetworkObject>();
                _matchObjects.Add(match, results);
            }

            bool r = results.Add(nob);
            _objectMatch[nob] = match;

            if (r)
                FinalizeChange(match, results, nob, manager);
        }
        /// <summary>
        /// Adds conns to match.
        /// </summary>
        /// <param name="match">Match to add conns to.</param>
        /// <param name="nobs">Connections to add to match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        /// <param name="replaceMatch">True to replace other matches with the new match.</param>
        public static void AddToMatch(int match, NetworkObject[] nobs, NetworkManager manager = null, bool replaceMatch = false)
        {
            AddToMatch(match, nobs.ToList(), manager, replaceMatch);
        }
        /// <summary>
        /// Adds conns to match.
        /// </summary>
        /// <param name="match">Match to add conns to.</param>
        /// <param name="nobs">Connections to add to match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        /// <param name="replaceMatch">True to replace other matches with the new match.</param>
        public static void AddToMatch(int match, List<NetworkObject> nobs, NetworkManager manager = null, bool replaceMatch = false)
        {
            if (replaceMatch)
            {
                foreach (NetworkObject n in nobs)
                    RemoveFromMatchWithoutRebuild(n, manager);
            }

            HashSet<NetworkObject> results;
            if (!_matchObjects.TryGetValueIL2CPP(match, out results))
            {
                results = new HashSet<NetworkObject>();
                _matchObjects.Add(match, results);
            }

            bool r = false;
            for (int i = 0; i < nobs.Count; i++)
            {
                NetworkObject n = nobs[i];
                r |= results.Add(n);
                _objectMatch[n] = match;
            }

            if (r)
                FinalizeChange(match, results, nobs, manager);
        }
        #endregion

        #region Remove from match NetworkConnection.

        /// <summary>
        /// Removes conn from any match without rebuilding observers.
        /// </summary>
        /// <param name="conn"></param>
        internal static bool RemoveFromMatchWithoutRebuild(NetworkConnection conn, NetworkManager manager)
        {
            bool removed = false;
            //If found to be in a match.
            if (_connectionMatch.TryGetValueIL2CPP(conn, out int match))
            {
                //If match is found.
                if (_matchConnections.TryGetValue(match, out HashSet<NetworkConnection> conns))
                {
                    removed |= conns.Remove(conn);
                    //If no more in hashset remove match.
                    if (conns.Count == 0)
                        _matchConnections.Remove(match);
                }
            }

            //Remove from connectionMatch.
            _connectionMatch.Remove(conn);
            return removed;
        }
        /// <summary>
        /// Removes conn from all matches.
        /// </summary>
        /// <param name="conn">NetworkConnection to remove.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        public static void RemoveFromMatch(NetworkConnection conn, NetworkManager manager)
        {
            bool removed = RemoveFromMatchWithoutRebuild(conn, manager);
            if (removed)
                GetServerObjects(manager).RebuildObservers();
        }
        /// <summary>
        /// Removes conn from match.
        /// </summary>
        /// <param name="match">Match to remove conn from.</param>
        /// <param name="conn">Connection to remove from match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromMatch(int match, NetworkConnection conn, NetworkManager manager)
        {
            if (_matchConnections.TryGetValueIL2CPP(match, out HashSet<NetworkConnection> results))
            {
                bool r = results.Remove(conn);
                _connectionMatch.Remove(conn);
                if (r)
                    FinalizeChange(match, results, manager);
            }
        }
        /// <summary>
        /// Removes conns from match.
        /// </summary>
        /// <param name="match">Match to remove conns from.</param>
        /// <param name="conns">Connections to remove from match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromMatch(int match, NetworkConnection[] conns, NetworkManager manager)
        {
            if (_matchConnections.TryGetValueIL2CPP(match, out HashSet<NetworkConnection> results))
            {
                bool r = false;
                for (int i = 0; i < conns.Length; i++)
                {
                    NetworkConnection c = conns[i];
                    r |= results.Remove(c);
                    _connectionMatch.Remove(c);
                }

                if (r)
                    FinalizeChange(match, results, manager);
            }
        }
        /// <summary>
        /// Removes conns from match.
        /// </summary>
        /// <param name="match">Match to remove conns from.</param>
        /// <param name="conns">Connections to remove from match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromMatch(int match, List<NetworkConnection> conns, NetworkManager manager)
        {
            if (_matchConnections.TryGetValueIL2CPP(match, out HashSet<NetworkConnection> results))
            {
                bool r = false;
                for (int i = 0; i < conns.Count; i++)
                {
                    NetworkConnection c = conns[i];
                    r |= results.Remove(c);
                    _connectionMatch.Remove(c);
                }

                if (r)
                    FinalizeChange(match, results, manager);
            }
        }
        #endregion

        #region Remove from match NetworkObject.
        /// <summary>
        /// Removes nob from any match without rebuilding observers.
        /// </summary>
        /// <param name="nob"></param>
        internal static bool RemoveFromMatchWithoutRebuild(NetworkObject nob, NetworkManager manager)
        {
            bool removed = false;
            //If found to be in a match.
            if (_objectMatch.TryGetValueIL2CPP(nob, out int match))
            {
                //If match is found.
                if (_matchObjects.TryGetValue(match, out HashSet<NetworkObject> nobs))
                {
                    removed |= nobs.Remove(nob);
                    //If no more in hashset remove match.
                    if (nobs.Count == 0)
                        _matchObjects.Remove(match);
                }
            }

            //Remove from connectionMatch.
            _objectMatch.Remove(nob);
            return removed;
        }
        /// <summary>
        /// Removes nob from all matches.
        /// </summary>
        /// <param name="nob">NetworkObject to remove.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        public static void RemoveFromMatch(NetworkObject nob, NetworkManager manager)
        {
            bool removed = RemoveFromMatchWithoutRebuild(nob, manager);
            if (removed)
                GetServerObjects(manager).RebuildObservers(nob);
        }
        /// <summary>
        /// Removes nob from all matches.
        /// </summary>
        /// <param name="nobs">NetworkObjects to remove.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        public static void RemoveFromMatch(NetworkObject[] nobs, NetworkManager manager)
        {
            RemoveFromMatch(nobs.ToList(), manager);
        }
        /// <summary>
        /// Removes nob from all matches.
        /// </summary>
        /// <param name="nobs">NetworkObjects to remove.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        public static void RemoveFromMatch(List<NetworkObject> nobs, NetworkManager manager)
        {
            bool removed = false;
            foreach (NetworkObject n in nobs)
                removed |= RemoveFromMatchWithoutRebuild(n, manager);

            if (removed)
                GetServerObjects(manager).RebuildObservers(nobs);
        }
        /// <summary>
        /// Removes conn from match.
        /// </summary>
        /// <param name="match">Match to remove conn from.</param>
        /// <param name="nob">NetworkObject to remove from match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromMatch(int match, NetworkObject nob, NetworkManager manager)
        {
            if (_matchObjects.TryGetValueIL2CPP(match, out HashSet<NetworkObject> results))
            {
                bool r = results.Remove(nob);
                _objectMatch.Remove(nob);
                if (r)
                    FinalizeChange(match, results, nob, manager);
            }
        }
        /// <summary>
        /// Removes conns from match.
        /// </summary>
        /// <param name="match">Match to remove conns from.</param>
        /// <param name="nobs">NetworkObjects to remove from match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromMatch(int match, NetworkObject[] nobs, NetworkManager manager)
        {
            if (_matchObjects.TryGetValueIL2CPP(match, out HashSet<NetworkObject> results))
            {
                bool r = false;
                for (int i = 0; i < nobs.Length; i++)
                {
                    NetworkObject n = nobs[i];
                    r |= results.Remove(n);
                    _objectMatch.Remove(n);
                }

                if (r)
                    FinalizeChange(match, results, nobs, manager);
            }
        }
        /// <summary>
        /// Removes conns from match.
        /// </summary>
        /// <param name="match">Match to remove conns from.</param>
        /// <param name="nobs">NetworkObjects to remove from match.</param>
        /// <param name="manager">NetworkManager to rebuild observers on. If null InstanceFinder.NetworkManager will be used.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveFromMatch(int match, List<NetworkObject> nobs, NetworkManager manager)
        {
            if (_matchObjects.TryGetValueIL2CPP(match, out HashSet<NetworkObject> results))
            {
                bool r = false;
                for (int i = 0; i < nobs.Count; i++)
                {
                    NetworkObject n = nobs[i];
                    r |= results.Remove(n);
                    _objectMatch.Remove(n);
                }

                if (r)
                    FinalizeChange(match, results, nobs, manager);
            }
        }
        #endregion


        #region FinalizeChange NetworkConnection.
        /// <summary>
        /// Finalizes changes to observers.
        /// </summary>
        private static void FinalizeChange(int match, HashSet<NetworkConnection> remainingConnsInMatch, NetworkManager manager)
        {
            if (remainingConnsInMatch.Count == 0)
                _matchConnections.Remove(match);

            /* Observers on all objects and all conditions have to be rebuilt.
             * This is because the connection changing matches could
             * require the connection to be visible for other players in the match,
             * as well make other connections in the same match visible.
             * But also make all the objects not associated with connections
             * of that match visible. In result to tick all of those boxes
             * all objects need to be rebuilt for all connections. */
            GetServerObjects(manager).RebuildObservers();
        }
        #endregion

        #region FinalizeChange NetworkObject.
        /// <summary>
        /// Finalizes changes to observers.
        /// </summary>
        private static void FinalizeChange(int match, HashSet<NetworkObject> results, List<NetworkObject> nobs, NetworkManager manager)
        {
            ListCache<NetworkObject> cache = ListCaches.GetNetworkObjectCache();
            cache.AddValues(nobs);
            FinalizeChange(match, results, cache, manager);
            ListCaches.StoreCache(cache);
        }
        /// <summary>
        /// Finalizes changes to observers.
        /// </summary>
        private static void FinalizeChange(int match, HashSet<NetworkObject> results, NetworkObject[] nobs, NetworkManager manager)
        {
            ListCache<NetworkObject> cache = ListCaches.GetNetworkObjectCache();
            cache.AddValues(nobs);
            FinalizeChange(match, results, cache, manager);
            ListCaches.StoreCache(cache);
        }
        /// <summary>
        /// Finalizes changes to observers.
        /// </summary>
        private static void FinalizeChange(int match, HashSet<NetworkObject> results, NetworkObject nob, NetworkManager manager)
        {
            ListCache<NetworkObject> cache = ListCaches.GetNetworkObjectCache();
            cache.AddValue(nob);
            FinalizeChange(match, results, cache, manager);
            ListCaches.StoreCache(cache);
        }
        /// <summary>
        /// Finalizes changes to observers.
        /// </summary>
        private static void FinalizeChange(int match, HashSet<NetworkObject> results, ListCache<NetworkObject> nobs, NetworkManager manager)
        {
            if (results.Count == 0)
                _matchConnections.Remove(match);

            GetServerObjects(manager).RebuildObservers(nobs);
        }
        #endregion

        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection">Connection which the condition is being checked for.</param>
        /// <param name="currentlyAdded">True if the connection currently has visibility of this object.</param>
        /// <param name="notProcessed">True if the condition was not processed. This can be used to skip processing for performance. While output as true this condition result assumes the previous ConditionMet value.</param>
        public override bool ConditionMet(NetworkConnection connection, bool alreadyAdded, out bool notProcessed)
        {
            //If here then checks are being processed.
            notProcessed = false;
            NetworkConnection owner = base.NetworkObject.Owner;
            /* If object is owned then check if owner
            * and connection share a match. */
            if (owner.IsValid)
            {
                //Connection isn't in a match.
                if (!_connectionMatch.TryGetValueIL2CPP(connection, out int match))
                {
                    //Return if this owner is also not in a match.
                    return !_connectionMatch.TryGetValueIL2CPP(owner, out int _);
                }
                //Match isn't found.
                if (!_matchConnections.TryGetValueIL2CPP(match, out HashSet<NetworkConnection> conns))
                    return false;
                //If owner is in same match return true.
                return conns.Contains(owner);
            }
            /* If no owner see if the object is in a match and if so
             * then compare that. */
            else
            {
                //Object isn't in a match.
                if (!_objectMatch.TryGetValueIL2CPP(base.NetworkObject, out int objectMatch))
                    return true;
                /* See if connection is in the same match as the object.
                 * If connection isn't in a match then it fails. */
                if (!_connectionMatch.TryGetValueIL2CPP(connection, out int connectionMatch))
                    return false;
                return (connectionMatch == objectMatch);
            }
        }


        /// <summary>
        /// Returns which ServerObjects to rebuild observers on.
        /// </summary>
        /// <param name="nm"></param>
        /// <returns></returns>
        private static ServerObjects GetServerObjects(NetworkManager manager)
        {
            return (manager == null) ? InstanceFinder.ServerManager.Objects : manager.ServerManager.Objects;
        }


        /* //todo this needs to be passing in the network manager to clear on,
         * otherwise only a single instance of NM is supported.
         * Users are already forced to specify which NM to add
         * matches for but the functionality separating different NMs in relation
         * to such isn't done yet. */
        /// <summary>
        /// Clears all match information without rebuilding.
        /// </summary>
        internal static void ClearMatchesWithoutRebuilding()
        {
            _connectionMatch.Clear();
            _matchConnections.Clear();
            _objectMatch.Clear();
            _matchObjects.Clear();
        }


        /// <summary>
        /// True if the condition requires regular updates.
        /// </summary>
        /// <returns></returns>
        public override bool Timed()
        {
            return false;
        }

        /// <summary>
        /// Clones referenced ObserverCondition. This must be populated with your conditions settings.
        /// </summary>
        /// <returns></returns>
        public override ObserverCondition Clone()
        {
            MatchCondition copy = ScriptableObject.CreateInstance<MatchCondition>();
            copy.ConditionConstructor();
            return copy;
        }
    }
}
