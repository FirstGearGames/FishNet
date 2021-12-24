using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Utility.Performance;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Component.Observing
{
    /// <summary>
    /// When this observer condition is placed on an object, a client must be within the specified distance to view the object.
    /// </summary>
    [CreateAssetMenu(menuName = "FishNet/Observers/Match Condition", fileName = "New Match Condition")]
    public class MatchCondition : ObserverCondition
    {
        #region Private.
        /// <summary>
        /// 
        /// </summary>
        private static Dictionary<int, HashSet<NetworkConnection>> _matches = new Dictionary<int, HashSet<NetworkConnection>>();
        /// <summary>
        /// Current matches and connections in each.
        /// </summary>
        public static IReadOnlyDictionary<int, HashSet<NetworkConnection>> Matches => _matches;
        #endregion

        public void ConditionConstructor() { }

        #region Add to match
        /// <summary>
        /// Adds conn to match.
        /// </summary>
        /// <param name="match">Match to add conn to.</param>
        /// <param name="conn">Connection to add to match.</param>
        public void AddToMatch(int match, NetworkConnection conn)
        {
            HashSet<NetworkConnection> result;
            if (!Matches.TryGetValue(match, out result))
            {
                result = new HashSet<NetworkConnection>();
                _matches.Add(match, result);
            }

            result.Add(conn);
        }
        /// <summary>
        /// Adds conns to match.
        /// </summary>
        /// <param name="match">Match to add conns to.</param>
        /// <param name="conns">Connections to add to match.</param>
        public void AddToMatch(int match, NetworkConnection[] conns)
        {
            HashSet<NetworkConnection> result;
            if (!Matches.TryGetValue(match, out result))
            {
                result = new HashSet<NetworkConnection>();
                _matches.Add(match, result);
            }

            for (int i = 0; i < conns.Length; i++)
                result.Add(conns[i]);
        }
        /// <summary>
        /// Adds conns to match.
        /// </summary>
        /// <param name="match">Match to add conns to.</param>
        /// <param name="conns">Connections to add to match.</param>
        public void AddToMatch(int match, List<NetworkConnection> conns)
        {
            HashSet<NetworkConnection> result;
            if (!Matches.TryGetValue(match, out result))
            {
                result = new HashSet<NetworkConnection>();
                _matches.Add(match, result);
            }

            for (int i = 0; i < conns.Count; i++)
                result.Add(conns[i]);
        }
        #endregion

        #region GetCache.
        private ListCache<NetworkConnection> GetCache(NetworkConnection conn)
        {
            return null;
            //return a cache from conn/conns. iterate through one method.
        }
            #endregion

        #region Remove from match.
        /// <summary>
        /// Removes conn from match.
        /// </summary>
        /// <param name="match">Match to remove conn from.</param>
        /// <param name="conn">Connection to remove from match.</param>
        public void RemoveFromMatch(int match, NetworkConnection conn)
        {
            if (Matches.TryGetValue(match, out HashSet<NetworkConnection> results))
            {
                results.Remove(conn);

                if (results.Count == 0)
                    _matches.Remove(match);
            }
        }
        /// <summary>
        /// Removes conns from match.
        /// </summary>
        /// <param name="match">Match to remove conns from.</param>
        /// <param name="conns">Connections to remove from match.</param>
        public void RemoveFromMatch(int match, NetworkConnection[] conns)
        {
            if (Matches.TryGetValue(match, out HashSet<NetworkConnection> results))
            {
                for (int i = 0; i < conns.Length; i++)
                    results.Remove(conns[i]);

                if (results.Count == 0)
                    _matches.Remove(match);
            }
        }
        /// <summary>
        /// Removes conns from match.
        /// </summary>
        /// <param name="match">Match to remove conns from.</param>
        /// <param name="conns">Connections to remove from match.</param>
        public void RemoveFromMatch(int match, List<NetworkConnection> conns)
        {
            if (Matches.TryGetValue(match, out HashSet<NetworkConnection> results))
            {
                for (int i = 0; i < conns.Count; i++)
                    results.Remove(conns[i]);

                
                if (results.Count == 0)
                    _matches.Remove(match);
            }
        }

        /// <summary>
        /// Finalizes a remove update.
        /// </summary>
        private void FinalizeBulkRemove(int match, HashSet<NetworkConnection> results, NetworkConnection conn)
        {
            if (results.Count == 0)
                _matches.Remove(match);

            base.NetworkObject.NetworkManager.ServerManager.Objects.RebuildObservers(conn);
        }
        #endregion

        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="notProcessed">True if the condition was not processed. This can be used to skip processing for performance. While output as true this condition result assumes the previous ConditionMet value.</param>
        public override bool ConditionMet(NetworkConnection connection, out bool notProcessed)
        {
            //If here then checks are being processed.
            notProcessed = false;


            return false;
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
