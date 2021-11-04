using System;
using System.Collections.Generic;

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
        /// Get the next writer in the pool or creates a new one if none are available.
        /// </summary>
        public static PooledWriter GetWriter()
        {
            PooledWriter result = (_pool.Count > 0) ? _pool.Pop() : new PooledWriter();
            result.Reset();
            return result;
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
