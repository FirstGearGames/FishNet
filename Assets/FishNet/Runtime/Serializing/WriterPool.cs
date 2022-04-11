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
    }

    /// <summary>
    /// Collection of PooledWriter. Stores and gets PooledWriter.
    /// </summary>
    public static class WriterPool
    {
        #region Private.
        /// <summary>
        /// Pool of writers.
        /// </summary>
        private static readonly Stack<PooledWriter> _pool = new Stack<PooledWriter>();
        #endregion

        /// <summary>
        /// Get the next writer in the pool.
        /// <para>If pool is empty, creates a new Reader</para>
        /// </summary>
        public static PooledWriter GetWriter(NetworkManager networkManager)
        {
            PooledWriter result = (_pool.Count > 0) ? _pool.Pop() : new PooledWriter();
            result.Reset(networkManager);
            return result;
        }
        /// <summary>
        /// Get the next writer in the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledWriter GetWriter()
        {
            return GetWriter(null);
        }

        /// <summary>
        /// Puts writer back into pool
        /// <para>When pool is full, the extra writer is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledWriter writer)
        {
            _pool.Push(writer);
        }
    }
}
