using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Serializing.Helping;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Utility.Performance
{
    /// <summary>
    /// Various ListCache instances that may be used on the Unity thread.
    /// </summary>
    public static class HashSets
    {
        /// <summary>
        /// Cache collection for NetworkObjects.
        /// </summary>
        private static Stack<HashSet<NetworkObject>> _networkObjectsCaches = new Stack<HashSet<NetworkObject>>();
        /// <summary>
        /// Cache collection for NetworkConnections.
        /// </summary>
        private static Stack<HashSet<NetworkConnection>> _networkConnectionCaches = new Stack<HashSet<NetworkConnection>>();
        /// <summary>
        /// Cache collection for ints.
        /// </summary>
        private static Stack<HashSet<int>> _intCaches = new Stack<HashSet<int>>();
        /// <summary>
        /// Cache collection for Vector2Int.
        /// </summary>
        private static Stack<HashSet<Vector2Int>> _vector2IntCaches = new Stack<HashSet<Vector2Int>>();

        #region GetCache.
        /// <summary>
        /// Returns a Vector2Int cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static HashSet<Vector2Int> RetrieveVector2IntCache()
        {
            return (_vector2IntCaches.Count == 0) ?
                new HashSet<Vector2Int>() : _vector2IntCaches.Pop();
        }
        /// <summary>
        /// Returns a NetworkObject cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static HashSet<NetworkObject> RetrieveNetworkObjectCache()
        {
            return (_networkObjectsCaches.Count == 0) ?
                new HashSet<NetworkObject>() : _networkObjectsCaches.Pop();
        }
        /// <summary>
        /// Returns a NetworkConnection cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static HashSet<NetworkConnection> RetrieveNetworkConnectionCache()
        {
            return (_networkConnectionCaches.Count == 0) ?
                new HashSet<NetworkConnection>() : _networkConnectionCaches.Pop();
        }
        /// <summary>
        /// Returns an int cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static HashSet<int> RetrieveIntCache()
        {
            return (_intCaches.Count == 0) ?
                new HashSet<int>() : _intCaches.Pop();
        }
        #endregion

        #region StoreCache.
        /// <summary>
        /// Stores a Vector2Int cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(HashSet<Vector2Int> cache)
        {
            cache.Clear();
            _vector2IntCaches.Push(cache);
        }
        /// <summary>
        /// Stores a NetworkObject cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(HashSet<NetworkObject> cache)
        {
            cache.Clear();
            _networkObjectsCaches.Push(cache);
        }
        /// <summary>
        /// Stores a NetworkConnection cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(HashSet<NetworkConnection> cache)
        {
            cache.Clear();
            _networkConnectionCaches.Push(cache);
        }
        /// <summary>
        /// Stores an int cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(HashSet<int> cache)
        {
            cache.Clear();
            _intCaches.Push(cache);
        }
        #endregion

    }
}
