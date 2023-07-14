using System;
using System.Collections.Generic;

namespace GameKit.Utilities
{
    /// <summary>
    /// Implement to use type with Caches.
    /// </summary>
    public interface IResettable
    {
        /// <summary>
        /// Resets values when being placed in a cache.
        /// </summary>
        void ResetState();
        /// <summary>
        /// Initializes values after being retrieved from a cache.
        /// </summary>
        void InitializeState();
    }

    #region Resettable caches.
    /// <summary>
    /// Holds cached Lists of value types.
    /// </summary>
    public static class ResettableCollectionCaches<T> where T : IResettable
    {
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static List<T> RetrieveList() => CollectionCaches<T>.RetrieveList();
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static HashSet<T> RetrieveHashSet() => CollectionCaches<T>.RetrieveHashSet();

        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(List<T> value)
        {
            foreach (T item in value)
                item.ResetState();
            CollectionCaches<T>.Store(value);
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(HashSet<T> value)
        {
            foreach (T item in value)
                item.ResetState();
            CollectionCaches<T>.Store(value);
        }
    }

    /// <summary>
    /// Holds cached diposable types.
    /// </summary>
    public static class ResettableObjectCaches<T> where T : IResettable
    {
        /// <summary>
        /// Retrieves an instance of T.
        /// </summary>
        public static T Retrieve()
        {
            T result = ObjectCaches<T>.Retrieve();
            result.InitializeState();
            return result;
        }
        /// <summary>
        /// Stores an instance of T.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(T value)
        {
            value.ResetState();
            ObjectCaches<T>.Store(value);
        }
    }
    #endregion

    #region NonResettable caches.
    /// <summary>
    /// Holds cached Lists of value types.
    /// </summary>
    public static class CollectionCaches<T>
    {

        /// <summary>
        /// Cache for lists.
        /// </summary>
        private readonly static Stack<List<T>> _listCache = new Stack<List<T>>();
        /// <summary>
        /// Cache for hashset.
        /// </summary>
        private readonly static Stack<HashSet<T>> _hashsetCache = new Stack<HashSet<T>>();

        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static List<T> RetrieveList()
        {
            if (_listCache.Count == 0)
                return new List<T>();
            else
                return _listCache.Pop();
        }
        /// <summary>
        /// Retrieves a collection adding one entry.
        /// </summary>
        /// <returns></returns>
        public static List<T> RetrieveList(T entry)
        {
            List<T> result;
            if (_listCache.Count == 0)
                result = new List<T>();
            else
                result = _listCache.Pop();

            result.Add(entry);
            return result;
        }

        /// <summary>
        /// Retrieves a HashSet<T>.
        /// </summary>
        /// <returns></returns>
        public static HashSet<T> RetrieveHashSet()
        {
            if (_hashsetCache.Count == 0)
                return new HashSet<T>();
            else
                return _hashsetCache.Pop();
        }
        /// <summary>
        /// Retrieves a collection adding one entry.
        /// </summary>
        /// <returns></returns>
        public static HashSet<T> RetrieveHashSet(T entry)
        {
            HashSet<T> result;
            if (_hashsetCache.Count == 0)
                result = new HashSet<T>();
            else
                result = _hashsetCache.Pop();

            result.Add(entry);
            return result;
        }

        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(List<T> value)
        {
            value.Clear();
            _listCache.Push(value);
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(HashSet<T> value)
        {
            value.Clear();
            _hashsetCache.Push(value);
        }

    }

    /// <summary>
    /// Holds cached types.
    /// </summary>
    public static class ObjectCaches<T>
    {
        /// <summary>
        /// Stack to use.
        /// </summary>
        private readonly static Stack<T> _stack = new Stack<T>();

        /// <summary>
        /// Returns a value from the stack or creates an instance when the stack is empty.
        /// </summary>
        /// <returns></returns>
        public static T Retrieve()
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
        public static void Store(T value)
        {
            _stack.Push(value);
        }
    }
    #endregion


}
