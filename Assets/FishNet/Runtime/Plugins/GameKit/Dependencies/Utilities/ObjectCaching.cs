using GameKit.Dependencies.Utilities.Types;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace GameKit.Dependencies.Utilities
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
    public static class ResettableCollectionCaches<T1, T2> where T1 : IResettable, new() where T2 : IResettable, new()
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
    public static class ResettableT1CollectionCaches<T1, T2> where T1 : IResettable, new()
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
    public static class ResettableT2CollectionCaches<T1, T2> where T2 : IResettable, new()
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
    public static class ResettableCollectionCaches<T> where T : IResettable, new()
    {
        /// <summary>
        /// Cache for ResettableRingBuffer.
        /// </summary>
        private readonly static Stack<ResettableRingBuffer<T>> _resettableRingBufferCache = new Stack<ResettableRingBuffer<T>>();

        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        public static ResettableRingBuffer<T> RetrieveRingBuffer()
        {
            ResettableRingBuffer<T> result;
            if (!_resettableRingBufferCache.TryPop(out result))
                result = new();

            return result;
        }
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
        public static void StoreAndDefault(ref ResettableRingBuffer<T> value)
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
        /// <param name="count">Number of entries in the array from the beginning.</param>
        public static void Store(ResettableRingBuffer<T> value)
        {
            value.ResetState();
            _resettableRingBufferCache.Push(value);
        }

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
    public static class ResettableObjectCaches<T> where T : IResettable, new()
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
            Dictionary<T1, T2> result;
            if (!_dictionaryCache.TryPop(out result))
                result = new();

            return result;
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
        /// Cache for queues.
        /// </summary>
        private readonly static Stack<Queue<T>> _queueCache = new Stack<Queue<T>>();
        /// <summary>
        /// Cache for queues.
        /// </summary>
        private readonly static Stack<BasicQueue<T>> _basicQueueCache = new Stack<BasicQueue<T>>();
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
            T[] result;
            if (!_arrayCache.TryPop(out result))
                result = new T[0];

            return result;
        }
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static List<T> RetrieveList()
        {
            List<T> result;
            if (!_listCache.TryPop(out result))
                result = new();

            return result;
        }
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static Queue<T> RetrieveQueue()
        {
            Queue<T> result;
            if (!_queueCache.TryPop(out result))
                result = new();

            return result;
        }
        /// <summary>
        /// Retrieves a collection.
        /// </summary>
        /// <returns></returns>
        public static BasicQueue<T> RetrieveBasicQueue()
        {
            BasicQueue<T> result;
            if (!_basicQueueCache.TryPop(out result))
                result = new();

            return result;
        }
        /// <summary>
        /// Retrieves a collection adding one entry.
        /// </summary>
        /// <returns></returns>
        public static Queue<T> RetrieveQueue(T entry)
        {
            Queue<T> result;
            if (!_queueCache.TryPop(out result))
                result = new();

            result.Enqueue(entry);
            return result;
        }
        /// <summary>
        /// Retrieves a collection adding one entry.
        /// </summary>
        /// <returns></returns>
        public static List<T> RetrieveList(T entry)
        {
            List<T> result;
            if (!_listCache.TryPop(out result))
                result = new();

            result.Add(entry);
            return result;
        }

        /// <summary>
        /// Retrieves a HashSet<T>.
        /// </summary>
        /// <returns></returns>
        public static HashSet<T> RetrieveHashSet()
        {
            HashSet<T> result;
            if (!_hashsetCache.TryPop(out result))
                result = new();

            return result;
        }
        /// <summary>
        /// Retrieves a collection adding one entry.
        /// </summary>
        /// <returns></returns>
        public static HashSet<T> RetrieveHashSet(T entry)
        {
            HashSet<T> result;
            if (!_hashsetCache.TryPop(out result))
                return new();

            result.Add(entry);
            return result;
        }

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        /// <param name="count">Number of entries in the array set default, from the beginning.</param>
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
        public static void StoreAndDefault(ref Queue<T> value)
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
        public static void Store(Queue<T> value)
        {
            value.Clear();
            _queueCache.Push(value);
        }

        /// <summary>
        /// Stores a collection and sets the original reference to default.
        /// Method will not execute if value is null.
        /// </summary>
        /// <param name="value">Value to store.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreAndDefault(ref BasicQueue<T> value)
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
        public static void Store(BasicQueue<T> value)
        {
            value.Clear();
            _basicQueueCache.Push(value);
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
    public static class ObjectCaches<T> where T : new()
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
            T result;
            if (!_stack.TryPop(out result))
                result = new();// Activator.CreateInstance<T>();

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
