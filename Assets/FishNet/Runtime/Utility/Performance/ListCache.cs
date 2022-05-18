using FishNet.Connection;
using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Utility.Performance
{
    /// <summary>
    /// Various ListCache instances that may be used on the Unity thread.
    /// </summary>
    public static class ListCaches
    {

        /// <summary>
        /// Cache collection for NetworkObjects.
        /// </summary>
        private static Stack<ListCache<NetworkObject>> _networkObjectCaches = new Stack<ListCache<NetworkObject>>();
        /// <summary>
        /// Cache for NetworkObjects.
        /// </summary>
        [Obsolete("Use GetNetworkObjectCache instead.")] //Remove on 2023/01/01
        public static ListCache<NetworkObject> NetworkObjectCache = new ListCache<NetworkObject>();
        /// <summary>
        /// Cache for NetworkBehaviours.
        /// </summary>
        public static ListCache<NetworkBehaviour> NetworkBehaviourCache = new ListCache<NetworkBehaviour>();
        /// <summary>
        /// Cache collection for NetworkObjects.
        /// </summary>
        private static Stack<ListCache<Transform>> _transformCaches = new Stack<ListCache<Transform>>();
        /// <summary>
        /// Cache for Transforms.
        /// </summary>
        [Obsolete("Use GetTransformCache instead.")] //Remove on 2023/01/01
        public static ListCache<Transform> TransformCache = new ListCache<Transform>();
        /// <summary>
        /// Cache collection for NetworkConnections.
        /// </summary>
        private static Stack<ListCache<NetworkConnection>> _networkConnectionCaches = new Stack<ListCache<NetworkConnection>>();
        /// <summary>
        /// Cache for NetworkConnectios.
        /// </summary>
        [Obsolete("Use GetNetworkConnectionCache instead.")] //Remove on 2023/01/01
        public static ListCache<NetworkConnection> NetworkConnectionCache = new ListCache<NetworkConnection>();
        /// <summary>
        /// Cache for ints.
        /// </summary>
        public static ListCache<int> IntCache = new ListCache<int>();


        #region GetCache.
        /// <summary>
        /// Returns a NetworkObject cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static ListCache<NetworkObject> GetNetworkObjectCache()
        {
            ListCache<NetworkObject> result;
            if (_networkObjectCaches.Count == 0)
                result = new ListCache<NetworkObject>();
            else
                result = _networkObjectCaches.Pop();

            return result;
        }
        /// <summary>
        /// Returns a NetworkConnection cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static ListCache<NetworkConnection> GetNetworkConnectionCache()
        {
            ListCache<NetworkConnection> result;
            if (_networkConnectionCaches.Count == 0)
                result = new ListCache<NetworkConnection>();
            else
                result = _networkConnectionCaches.Pop();

            return result;
        }
        /// <summary>
        /// Returns a Transform cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static ListCache<Transform> GetTransformCache()
        {
            ListCache<Transform> result;
            if (_transformCaches.Count == 0)
                result = new ListCache<Transform>();
            else
                result = _transformCaches.Pop();

            return result;
        }
        #endregion


        #region StoreCache.
        /// <summary>
        /// Stores a NetworkObject cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(ListCache<NetworkObject> cache)
        {
            cache.Reset();
            _networkObjectCaches.Push(cache);
        }
        /// <summary>
        /// Stores a NetworkConnection cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(ListCache<NetworkConnection> cache)
        {
            cache.Reset();
            _networkConnectionCaches.Push(cache);
        }
        /// <summary>
        /// Stores a Transform cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(ListCache<Transform> cache)
        {
            cache.Reset();
            _transformCaches.Push(cache);
        }
        #endregion

    }

    /// <summary>
    /// Creates a reusable cache of T which auto expands.
    /// </summary>
    public class ListCache<T>
    {
        #region Public.
        /// <summary>
        /// Collection cache is for.
        /// </summary>
        public List<T> Collection;
        /// <summary>
        /// Entries currently written.
        /// </summary>
        public int Written { get; private set; }
        #endregion

        public ListCache()
        {
            Collection = new List<T>();
        }
        public ListCache(int capacity)
        {
            Collection = new List<T>(capacity);
        }

        /// <summary>
        /// Adds a new value to Collection and returns it.
        /// </summary>
        /// <param name="value"></param>
        public T AddReference()
        {
            if (Collection.Count <= Written)
            {
                T next = Activator.CreateInstance<T>();
                Collection.Add(next);
                Written++;
                return next;
            }
            else
            {
                T next = Collection[Written];
                Written++;
                return next;
            }
        }


        /// <summary>
        /// Adds value to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValue(T value)
        {
            if (Collection.Count <= Written)
                Collection.Add(value);
            else
                Collection[Written] = value;

            Written++;
        }

        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(T[] values)
        {
            for (int i = 0; i < values.Length; i++)
                AddValue(values[i]);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(List<T> values)
        {
            for (int i = 0; i < values.Count; i++)
                AddValue(values[i]);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(HashSet<T> values)
        {
            foreach (T item in values)
                AddValue(item);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(ISet<T> values)
        {
            foreach (T item in values)
                AddValue(item);
        }

        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(IReadOnlyCollection<T> values)
        {
            foreach (T item in values)
                AddValue(item);
        }


        /// <summary>
        /// Resets cache.
        /// </summary>
        public void Reset()
        {
            Written = 0;
        }
    }


}
