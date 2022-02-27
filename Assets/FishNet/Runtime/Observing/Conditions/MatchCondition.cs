//WIP
/* Use biredirectioanl dictionary to connections can be removed
 * from matches when they disconnect. */

//using FishNet.Connection;
//using FishNet.Observing;
//using FishNet.Utility.Extension;
//using FishNet.Utility.Performance;
//using System.Collections.Generic;
//using System.Runtime.CompilerServices;
//using UnityEngine;

//namespace FishNet.Component.Observing
//{
//    /// <summary>
//    /// When this observer condition is placed on an object, a client must be within the same match to view the object.
//    /// </summary>
//    [CreateAssetMenu(menuName = "FishNet/Observers/Match Condition", fileName = "New Match Condition")]
//    public class MatchCondition : ObserverCondition
//    {
//        #region Private.
//        /// <summary>
//        /// 
//        /// </summary>
//        private static Dictionary<int, HashSet<NetworkConnection>> _matches = new Dictionary<int, HashSet<NetworkConnection>>();
//        /// <summary>
//        /// Current matches and connections in each.
//        /// </summary>
//        public static IReadOnlyDictionary<int, HashSet<NetworkConnection>> Matches => _matches;
//        #endregion

//        public void ConditionConstructor() { }

//        #region Add to match
//        /// <summary>
//        /// Adds conn to match.
//        /// </summary>
//        /// <param name="match">Match to add conn to.</param>
//        /// <param name="conn">Connection to add to match.</param>
//        public void AddToMatch(int match, NetworkConnection conn)
//        {
//            HashSet<NetworkConnection> results;
//            if (!_matches.TryGetValueIL2CPP(match, out results))
//            {
//                results = new HashSet<NetworkConnection>();
//                _matches.Add(match, results);
//            }

//            bool r = results.Add(conn);
//            if (r)
//                FinalizeChange(match, results, conn);
//        }
//        /// <summary>
//        /// Adds conns to match.
//        /// </summary>
//        /// <param name="match">Match to add conns to.</param>
//        /// <param name="conns">Connections to add to match.</param>
//        public void AddToMatch(int match, NetworkConnection[] conns)
//        {
//            HashSet<NetworkConnection> results;
//            if (!_matches.TryGetValueIL2CPP(match, out results))
//            {
//                results = new HashSet<NetworkConnection>();
//                _matches.Add(match, results);
//            }

//            bool r = false;
//            for (int i = 0; i < conns.Length; i++)
//                r |= results.Add(conns[i]);

//            if (r)
//                FinalizeChange(match, results, conns);
//        }
//        /// <summary>
//        /// Adds conns to match.
//        /// </summary>
//        /// <param name="match">Match to add conns to.</param>
//        /// <param name="conns">Connections to add to match.</param>
//        public void AddToMatch(int match, List<NetworkConnection> conns)
//        {
//            HashSet<NetworkConnection> results; 
//            if (!_matches.TryGetValueIL2CPP(match, out results))
//            {
//                results = new HashSet<NetworkConnection>();
//                _matches.Add(match, results);
//            }

//            bool r = false;
//            for (int i = 0; i < conns.Count; i++)
//                r |= results.Add(conns[i]);

//            if (r)
//                FinalizeChange(match, results, conns);
//        }
//        #endregion

//        #region Remove from match.
//        /// <summary>
//        /// Removes conn from match.
//        /// </summary>
//        /// <param name="match">Match to remove conn from.</param>
//        /// <param name="conn">Connection to remove from match.</param>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public void RemoveFromMatch(int match, NetworkConnection conn)
//        {
//            if (_matches.TryGetValueIL2CPP(match, out HashSet<NetworkConnection> results))
//            {
//                bool r = results.Remove(conn);

//                if (r)
//                    FinalizeChange(match, results, conn);
//            }
//        }
//        /// <summary>
//        /// Removes conns from match.
//        /// </summary>
//        /// <param name="match">Match to remove conns from.</param>
//        /// <param name="conns">Connections to remove from match.</param>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public void RemoveFromMatch(int match, NetworkConnection[] conns)
//        {
//            if (_matches.TryGetValueIL2CPP(match, out HashSet<NetworkConnection> results))
//            {
//                bool r = false;
//                for (int i = 0; i < conns.Length; i++)
//                    r |= results.Remove(conns[i]);

//                if (r)
//                    FinalizeChange(match, results, conns);
//            }
//        }
//        /// <summary>
//        /// Removes conns from match.
//        /// </summary>
//        /// <param name="match">Match to remove conns from.</param>
//        /// <param name="conns">Connections to remove from match.</param>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public void RemoveFromMatch(int match, List<NetworkConnection> conns)
//        {
//            if (_matches.TryGetValueIL2CPP(match, out HashSet<NetworkConnection> results))
//            {
//                bool r = false;
//                for (int i = 0; i < conns.Count; i++)
//                    r |= results.Remove(conns[i]);

//                if (r)
//                    FinalizeChange(match, results, conns);
//            }
//        }
//        /// <summary>
//        /// Finalizes changes to observers.
//        /// </summary>
//        private void FinalizeChange(int match, HashSet<NetworkConnection> results, List<NetworkConnection> conns)
//        {
//            ListCache<NetworkConnection> cache = ListCaches.NetworkConnectionCache;
//            cache.Reset();
//            cache.AddValues(conns);
//            FinalizeChange(match, results, cache);
//        }
//        /// <summary>
//        /// Finalizes changes to observers.
//        /// </summary>
//        private void FinalizeChange(int match, HashSet<NetworkConnection> results, NetworkConnection[] conns)
//        {
//            ListCache<NetworkConnection> cache = ListCaches.NetworkConnectionCache;
//            cache.Reset();
//            cache.AddValues(conns);
//            FinalizeChange(match, results, cache);
//        }
//        /// <summary>
//        /// Finalizes changes to observers.
//        /// </summary>
//        private void FinalizeChange(int match, HashSet<NetworkConnection> results, NetworkConnection conn)
//        {
//            ListCache<NetworkConnection> cache = ListCaches.NetworkConnectionCache;
//            cache.Reset();
//            cache.AddValue(conn);
//            FinalizeChange(match, results, cache);
//        }
//        /// <summary>
//        /// Finalizes changes to observers.
//        /// </summary>
//        private void FinalizeChange(int match, HashSet<NetworkConnection> results, ListCache<NetworkConnection> conns)
//        {
//            if (results.Count == 0)
//                _matches.Remove(match);

//            base.NetworkObject.NetworkManager.ServerManager.Objects.RebuildObservers(conns);
//        }
//        #endregion

//        /// <summary>
//        /// Returns if the object which this condition resides should be visible to connection.
//        /// </summary>
//        /// <param name="connection"></param>
//        /// <param name="notProcessed">True if the condition was not processed. This can be used to skip processing for performance. While output as true this condition result assumes the previous ConditionMet value.</param>
//        public override bool ConditionMet(NetworkConnection connection, out bool notProcessed)
//        {
//            //If here then checks are being processed.
//            notProcessed = false;


//            return false;
//        }

//        /// <summary>
//        /// True if the condition requires regular updates.
//        /// </summary>
//        /// <returns></returns>
//        public override bool Timed()
//        {
//            return false;
//        }

//        /// <summary>
//        /// Clones referenced ObserverCondition. This must be populated with your conditions settings.
//        /// </summary>
//        /// <returns></returns>
//        public override ObserverCondition Clone()
//        {
//            MatchCondition copy = ScriptableObject.CreateInstance<MatchCondition>();
//            copy.ConditionConstructor();
//            return copy;
//        }
//    }
//}
