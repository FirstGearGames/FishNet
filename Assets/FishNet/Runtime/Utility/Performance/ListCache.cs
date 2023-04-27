using FishNet.Connection;
using FishNet.Object;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Utility.Performance
{
    #region Disposable caches.
    /// <summary>
    /// Holds cached Lists of value types.
    /// </summary>
    public static class DisposableListCaches<T> where T : IDisposable
    {

        /// <summary>
        /// Cache.
        /// </summary>
        private static Stack<List<T>> _listCache = new Stack<List<T>>();
        
        /// <summary>
        /// Retrieves a List<T>.
        /// </summary>
        /// <returns></returns>
        public static List<T> Retrieve()
        {
            if (_listCache.Count == 0)
                return new List<T>();
            else
                return _listCache.Pop();
        }

        /// <summary>
        /// Stores an instance of List<T>.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(List<T> value)
        {
            foreach (T item in value)
                item.Dispose();
            value.Clear();
            _listCache.Push(value);
        }

    }

    /// <summary>
    /// Holds cached diposable types.
    /// </summary>
    public static class DisposableObjectCaches<T> where T : IDisposable
    {
        /// <summary>
        /// Cache.
        /// </summary>
        private static DisposableObjectCache<T> _objectCache = new DisposableObjectCache<T>();

        /// <summary>
        /// Retrieves an instance of T.
        /// </summary>
        public static T Retrieve() => _objectCache.Retrieve();
        /// <summary>
        /// Stores an instance of T.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(T value) => _objectCache.Store(value);
    }

    /// <summary>
    /// A cache for a disposable type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DisposableObjectCache<T> where T : IDisposable
    {
        /// <summary>
        /// Stack to use.
        /// </summary>
        private Stack<T> _stack = new Stack<T>();

        /// <summary>
        /// Returns a value from the stack or creates an instance when the stack is empty.
        /// </summary>
        /// <returns></returns>
        public T Retrieve()
        {
            if (_stack.Count == 0)
                return Activator.CreateInstance<T>();
            else
                return _stack.Pop();
        }

        /// <summary>
        /// Stores a value to the stack.
        /// </summary>
        /// <param name="value"></param>
        public void Store(T value)
        {
            value.Dispose();
            _stack.Push(value);
        }
    }
    #endregion

    #region NonDisposable caches.
    /// <summary>
    /// Holds cached Lists of value types.
    /// </summary>
    public static class CollectionCaches<T>
    {

        /// <summary>
        /// Cache.
        /// </summary>
        private static Stack<List<T>> _listCache = new Stack<List<T>>();

        /// <summary>
        /// Retrieves a List<T>.
        /// </summary>
        /// <returns></returns>
        public static List<T> Retrieve()
        {
            if (_listCache.Count == 0)
                return new List<T>();
            else
                return _listCache.Pop();
        }

        /// <summary>
        /// Stores an instance of List<T>.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(List<T> value)
        {
            value.Clear();
            _listCache.Push(value);
        }

    }

    /// <summary>
    /// Holds cached diposable types.
    /// </summary>
    public static class ObjectCaches<T>
    {
        /// <summary>
        /// Cache.
        /// </summary>
        private static ObjectCache<T> _objectCache = new ObjectCache<T>();

        /// <summary>
        /// Retrieves an instance of T.
        /// </summary>
        public static T Retrieve() => _objectCache.Retrieve();
        /// <summary>
        /// Stores an instance of T.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(T value) => _objectCache.Store(value);
    }

    /// <summary>
    /// A cache for a disposable type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectCache<T>
    {
        /// <summary>
        /// Stack to use.
        /// </summary>
        private Stack<T> _stack = new Stack<T>();

        /// <summary>
        /// Returns a value from the stack or creates an instance when the stack is empty.
        /// </summary>
        /// <returns></returns>
        public T Retrieve()
        {
            if (_stack.Count == 0)
                return Activator.CreateInstance<T>();
            else
                return _stack.Pop();
        }

        /// <summary>
        /// Stores a value to the stack.
        /// </summary>
        /// <param name="value"></param>
        public void Store(T value)
        {
            _stack.Push(value);
        }
    }
    #endregion

    /// <summary>
    /// Various ListCache instances that may be used on the Unity thread.
    /// </summary>
    [Obsolete("ListCache has been discovered potentially contain a small memory leak depending on the type being cached. Use ObjectCaches, DisposableObjectCaches, CollectionCaches, DisposableCollectionCaches instead.")] //remove on 2023/01/01
    public static class ListCaches
    {

        /// <summary>
        /// Cache collection for NetworkObjects.
        /// </summary>
        private static Stack<ListCache<NetworkObject>> _networkObjectCaches = new Stack<ListCache<NetworkObject>>();
        /// <summary>
        /// Cache collection for NetworkBehaviours.
        /// </summary>
        private static Stack<ListCache<NetworkBehaviour>> _networkBehaviourCaches = new Stack<ListCache<NetworkBehaviour>>();
        /// <summary>
        /// Cache collection for NetworkObjects.
        /// </summary>
        private static Stack<ListCache<Transform>> _transformCaches = new Stack<ListCache<Transform>>();
        /// <summary>
        /// Cache collection for NetworkConnections.
        /// </summary>
        private static Stack<ListCache<NetworkConnection>> _networkConnectionCaches = new Stack<ListCache<NetworkConnection>>();
        /// <summary>
        /// Cache collection for ints.
        /// </summary>        
        private static Stack<ListCache<int>> _intCaches = new Stack<ListCache<int>>();


        #region GetCache.
        [Obsolete("Use RetrieveNetworkObjectCache().")] //Remove on 2024/01/01
        public static ListCache<NetworkObject> GetNetworkObjectCache() => RetrieveNetworkObjectCache();
        /// <summary>
        /// Returns a NetworkObject cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static ListCache<NetworkObject> RetrieveNetworkObjectCache()
        {
            ListCache<NetworkObject> result;
            if (_networkObjectCaches.Count == 0)
                result = new ListCache<NetworkObject>();
            else
                result = _networkObjectCaches.Pop();

            return result;
        }
        [Obsolete("Use RetrieveNetworkConnectionCache().")] //Remove on 2024/01/01
        public static ListCache<NetworkConnection> GetNetworkConnectionCache() => RetrieveNetworkConnectionCache();
        /// <summary>
        /// Returns a NetworkConnection cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static ListCache<NetworkConnection> RetrieveNetworkConnectionCache()
        {
            ListCache<NetworkConnection> result;
            if (_networkConnectionCaches.Count == 0)
                result = new ListCache<NetworkConnection>();
            else
                result = _networkConnectionCaches.Pop();

            return result;
        }
        [Obsolete("Use RetrieveTransformCache().")] //Remove on 2024/01/01
        public static ListCache<Transform> GetTransformCache() => RetrieveTransformCache();
        /// <summary>
        /// Returns a Transform cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static ListCache<Transform> RetrieveTransformCache()
        {
            ListCache<Transform> result;
            if (_transformCaches.Count == 0)
                result = new ListCache<Transform>();
            else
                result = _transformCaches.Pop();

            return result;
        }
        [Obsolete("Use RetrieveNetworkBehaviourCache().")] //Remove on 2024/01/01
        public static ListCache<NetworkBehaviour> GetNetworkBehaviourCache() => RetrieveNetworkBehaviourCache();
        /// <summary>
        /// Returns a NetworkBehaviour cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static ListCache<NetworkBehaviour> RetrieveNetworkBehaviourCache()
        {
            ListCache<NetworkBehaviour> result;
            if (_networkBehaviourCaches.Count == 0)
                result = new ListCache<NetworkBehaviour>();
            else
                result = _networkBehaviourCaches.Pop();

            return result;
        }
        [Obsolete("Use RetrieveGetIntCache().")] //Remove on 2024/01/01
        public static ListCache<int> GetIntCache() => RetrieveIntCache();
        /// <summary>
        /// Returns an int cache. Use StoreCache to return the cache.
        /// </summary>
        /// <returns></returns>
        public static ListCache<int> RetrieveIntCache()
        {
            ListCache<int> result;
            if (_intCaches.Count == 0)
                result = new ListCache<int>();
            else
                result = _intCaches.Pop();

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
        /// <summary>
        /// Stores a NetworkBehaviour cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(ListCache<NetworkBehaviour> cache)
        {
            cache.Reset();
            _networkBehaviourCaches.Push(cache);
        }
        /// <summary>
        /// Stores an int cache.
        /// </summary>
        /// <param name="cache"></param>
        public static void StoreCache(ListCache<int> cache)
        {
            cache.Reset();
            _intCaches.Push(cache);
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
        public List<T> Collection = new List<T>();
        /// <summary>
        /// Entries currently written.
        /// </summary>
        public int Written => Collection.Count;
        #endregion

        #region Private.
        /// <summary>
        /// Cache for type.
        /// </summary>
        private Stack<T> _cache = new Stack<T>();
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
        /// Returns T from cache when possible, or creates a new object when not.
        /// </summary>
        /// <returns></returns>
        private T Retrieve()
        {
            if (_cache.Count > 0)
                return _cache.Pop();
            else
                return Activator.CreateInstance<T>();
        }
        /// <summary>
        /// Stores value into the cache.
        /// </summary>
        /// <param name="value"></param>
        private void Store(T value)
        {
            _cache.Push(value);
        }

        /// <summary>
        /// Adds a new value to Collection and returns it.
        /// </summary>
        /// <param name="value"></param>
        public T AddReference()
        {
            T next = Retrieve();
            Collection.Add(next);
            return next;
        }

        /// <summary>
        /// Inserts an bject into Collection and returns it.
        /// </summary>
        /// <param name="value"></param>
        public T InsertReference(int index)
        {
            //Would just be at the end anyway.
            if (index >= Collection.Count)
                return AddReference();

            T next = Retrieve();
            Collection.Insert(index, next);
            return next;
        }

        /// <summary>
        /// Adds value to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValue(T value)
        {
            Collection.Add(value);
        }

        /// <summary>
        /// Inserts value into Collection.
        /// </summary>
        /// <param name="value"></param>

        public void InsertValue(int index, T value)
        {
            //Would just be at the end anyway.
            if (index >= Collection.Count)
                AddValue(value);
            else
                Collection.Insert(index, value);
        }

        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="values"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValues(ListCache<T> values)
        {
            int w = values.Written;
            List<T> c = values.Collection;
            for (int i = 0; i < w; i++)
                AddValue(c[i]);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValues(T[] values)
        {
            for (int i = 0; i < values.Length; i++)
                AddValue(values[i]);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValues(List<T> values)
        {
            for (int i = 0; i < values.Count; i++)
                AddValue(values[i]);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValues(HashSet<T> values)
        {
            foreach (T item in values)
                AddValue(item);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddValues(ISet<T> values)
        {
            foreach (T item in values)
                AddValue(item);
        }

        /// <summary>
        /// Adds values to Collection.
        /// </summary> 
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            foreach (T item in Collection)
                Store(item);
            Collection.Clear();
        }
    }


}
