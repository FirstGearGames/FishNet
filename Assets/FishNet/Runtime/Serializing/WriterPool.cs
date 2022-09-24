using FishNet.Managing;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FishNet.Serializing
{
    /// <summary>
    /// Writer which is reused to save on garbage collection and performance.
    /// </summary>
    public sealed class PooledWriter : Writer, IDisposable
    {
        public void Dispose() => WriterPool.Recycle(this);
        public void DisposeLength() => WriterPool.RecycleLength(this);
    }

    /// <summary>
    /// Collection of PooledWriter. Stores and gets PooledWriter.
    /// </summary>
    public static class WriterPool
    {
        #region Private.
        /// <summary>
        /// Pool of writers where length is the minimum and increased at runtime.
        /// </summary>
        private static readonly Stack<PooledWriter> _pool = new Stack<PooledWriter>();
        /// <summary>
        /// Pool of writers where length is of minimum key and may be increased at runtime.
        /// </summary>
        private static readonly Dictionary<int, Stack<PooledWriter>> _lengthPool = new Dictionary<int, Stack<PooledWriter>>();
        #endregion

        #region Const.
        /// <summary>
        /// Length of each bracket when using the length based writer pool.
        /// </summary>
        internal const int LENGTH_BRACKET = 1000;
        #endregion

        /// <summary>
        /// Gets a writer from the pool.
        /// </summary>
        public static PooledWriter GetWriter(NetworkManager networkManager)
        {
            PooledWriter result = (_pool.Count > 0) ? _pool.Pop() : new PooledWriter();
            result.Reset(networkManager);
            return result;
        }
        /// <summary>
        /// Gets a writer from the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledWriter GetWriter()
        {
            return GetWriter(null);
        }

        /// <summary>
        /// Gets which index to use for length based pooled readers based on length.
        /// </summary>
        private static int GetDictionaryIndex(int length)
        {
            return (length / LENGTH_BRACKET);
        }
        /// <summary>
        /// Gets the next writer in the pool of minimum length.
        /// </summary>
        /// <param name="length">Minimum length the writer buffer must be.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledWriter GetWriter(int length)
        {
            return GetWriter(null, length);
        }
        /// <summary>
        /// Gets the next writer in the pool of minimum length.
        /// </summary>
        /// <param name="length">Minimum length the writer buffer must be.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledWriter GetWriter(NetworkManager networkManager, int length)
        {
            //Ensure length is the minimum.
            if (length < LENGTH_BRACKET)
                length = LENGTH_BRACKET;

            /* The index returned will be for writers which have
             * length as a minimum capacity.
             * EG: if length is 1200 / 1000 (length_bracket) result
             * will be index 1. Index 0 will be up to 1000, while
             * index 1 will be up to 2000. */
            int dictIndex = GetDictionaryIndex(length);
            Stack<PooledWriter> stack;
            //There is already one pooled.
            if (_lengthPool.TryGetValue(dictIndex, out stack) && stack.Count > 0)
            {
                PooledWriter result = stack.Pop();
                result.Reset(networkManager);
                return result;
            }
            //Not pooled yet.
            else
            {
                //Get any ol' writer.
                PooledWriter writer = GetWriter(networkManager);
                /* Ensure length to fill it's bracket.
                 * Increase index by 1 since 0 index would
                 * just return 0 as the capacity. */
                int requiredCapacity = (dictIndex + 1) * LENGTH_BRACKET;
                writer.EnsureBufferCapacity(requiredCapacity);
                return writer;
            }
        }

        /// <summary>
        /// Returns a writer to the appropriate length pool.
        /// Writers must be a minimum of 1000 bytes in length to be sorted by length.
        /// Writers which do not meet the minimum will be resized to 1000 bytes.
        /// </summary>
        public static void RecycleLength(PooledWriter writer)
        {
            int capacity = writer.Capacity;
            /* If capacity is less than 1000 then the writer
             * does not meet the minimum length bracket. This should never
             * be the case unless the user perhaps manually calls this method. */
            if (capacity < LENGTH_BRACKET)
            {
                capacity = LENGTH_BRACKET;
                writer.EnsureBufferCapacity(LENGTH_BRACKET);
            }

            /* When getting the recycle index subtract one from
             * the dictionary index. This is because the writer being
             * recycled must meet the minimum for that index.
             * EG: if LENGTH_BRACKET is 1000....
             * 1200 / 1000 = 1(after flooring).
             * However, each incremeent in index should have a capacity
             * of 1000, so index 1 should have a minimum capacity of 2000,
             * which 1200 does not meet. By subtracting 1 from the index
             * 1200 will now be placed in index 0 meeting the capacity for that index. */
            int dictIndex = GetDictionaryIndex(capacity) - 1;
            Stack<PooledWriter> stack;
            if (!_lengthPool.TryGetValue(dictIndex, out stack))
            {
                stack = new Stack<PooledWriter>();
                _lengthPool[dictIndex] = stack;
            }

            stack.Push(writer);
        }


        /// <summary>
        /// Returns a writer to the pool.
        /// </summary>
        public static void Recycle(PooledWriter writer)
        {
            _pool.Push(writer);
        }
    }
}
