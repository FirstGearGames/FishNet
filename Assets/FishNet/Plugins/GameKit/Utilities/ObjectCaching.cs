using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
    /// Caches collections of multiple generics.
    /// </summary>
    public static class ResettableCollectionCaches<T1, T2> where T1 : IResettable where T2 : IResettable
    {
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<T1, T2> RetrieveDictionary() => CollectionCaches<T1, T2>.RetrieveDictionary();

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref Dictionary<T1, T2> value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(Dictionary<T1, T2> value)
        {
            foreach (KeyValuePair<T1, T2> kvp in value)
            {
                kvp.Key.ResetState();
                ObjectCaches<T1>.Store(kvp.Key);
                kvp.Value.ResetState();
                ObjectCaches<T2>.Store(kvp.Value);
            }
            value.Clear();
            CollectionCaches<T1, T2>.Store(value);
        }
    }

    /// <summary>
    /// Caches collections of multiple generics.
    /// </summary>
    public static class ResettableT1CollectionCaches<T1, T2> where T1 : IResettable
    {
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<T1, T2> RetrieveDictionary() => CollectionCaches<T1, T2>.RetrieveDictionary();

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref Dictionary<T1, T2> value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(Dictionary<T1, T2> value)
        {
            foreach (T1 item in value.Keys)
            {
                item.ResetState();
                ObjectCaches<T1>.Store(item);
            }
            value.Clear();
            CollectionCaches<T1, T2>.Store(value);
        }
    }

    /// <summary>
    /// Caches collections of multiple generics.
    /// </summary>
    public static class ResettableT2CollectionCaches<T1, T2> where T2 : IResettable
    {
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<T1, T2> RetrieveDictionary() => CollectionCaches<T1, T2>.RetrieveDictionary();

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref Dictionary<T1, T2> value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(Dictionary<T1, T2> value)
        {
            foreach (T2 item in value.Values)
            {
                item.ResetState();
                ObjectCaches<T2>.Store(item);
            }
            value.Clear();
            CollectionCaches<T1, T2>.Store(value);
        }
    }



    /// <summary>
    /// Caches collections of a single generic.
    /// </summary>
    public static class ResettableCollectionCaches<T> where T : IResettable
    {
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static T[] RetrieveArray() => CollectionCaches<T>.RetrieveArray();
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
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        /// <param name="count">Number of entries in the array from the beginning.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref T[] value, int count)
        {
            if (value == null)
                return;
            Store(value, count);
            value = default;
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        /// <param name="count">Number of entries in the array from the beginning.</param>
        public static void Store(T[] value, int count)
        {
            for (int i = 0; i < count; i++)
            {
                value[i].ResetState();
                ObjectCaches<T>.Store(value[i]);
            }
            CollectionCaches<T>.Store(value, count);
        }

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref List<T> value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(List<T> value)
        {
            for (int i = 0; i < value.Count; i++)
            {
                value[i].ResetState();
                ObjectCaches<T>.Store(value[i]);
            }
            value.Clear();
            CollectionCaches<T>.Store(value);
        }

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref HashSet<T> value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(HashSet<T> value)
        {
            foreach (T item in value)
            {
                item.ResetState();
                ObjectCaches<T>.Store(item);
            }
            value.Clear();
            CollectionCaches<T>.Store(value);
        }
    }

    /// <summary>
    /// Caches objects of a single generic.
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
        /// Stores an instance of T and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref T value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
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
    /// Caches collections of multiple generics.
    /// </summary>
    public static class CollectionCaches<T1, T2>
    {
        /// <summary>
        /// Cache for dictionaries.
        /// </summary>
        private readonly static Stack<Dictionary<T1, T2>> _dictionaryCache = new Stack<Dictionary<T1, T2>>();

        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static Dictionary<T1, T2> RetrieveDictionary()
        {
            if (_dictionaryCache.Count == 0)
                return new Dictionary<T1, T2>();
            else
                return _dictionaryCache.Pop();
        }

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref Dictionary<T1, T2> value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        public static void Store(Dictionary<T1, T2> value)
        {
            value.Clear();
            _dictionaryCache.Push(value);
        }
    }

    /// <summary>
    /// Caches collections of a single generic.
    /// </summary>
    public static class CollectionCaches<T>
    {
        /// <summary>
        /// Cache for arrays.
        /// </summary>
        private readonly static Stack<T[]> _arrayCache = new Stack<T[]>();
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
        public static T[] RetrieveArray()
        {
            if (_arrayCache.Count == 0)
                return new T[0];
            else
                return _arrayCache.Pop();
        }
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
        /// Stores a collection and sets the original reference to default.\
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        /// <param name="count">Number of entries in the array from the beginning.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref T[] value, int count)
        {
            if (value == null)
                return;
            Store(value, count);
            value = default;
        }
        /// <summary>
        /// Stores a collection.
        /// </summary>
        /// <param name="value">Value to store.</param>
        /// <param name="count">Number of entries in the array from the beginning.</param>
        public static void Store(T[] value, int count)
        {
            for (int i = 0; i < count; i++)
                value[i] = default;

            _arrayCache.Push(value);
        }

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref List<T> value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
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
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref HashSet<T> value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
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
    /// Caches objects of a single generic.
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
        /// Stores an instance of T and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref T value)
        {
            if (value == null)
                return;
            Store(value);
            value = default;
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
