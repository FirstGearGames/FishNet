using FishNet.Serializing.Helping;
using System;
using System.Collections.Generic;

namespace FishNet.Serializing
{
    /// <summary>
    /// NetworkWriter to be used with <see cref="WriterPool">NetworkWriterPool</see>
    /// </summary>
    //[CodegenIncludeInternal]
    public sealed class PooledWriter : Writer, IDisposable
    {
        public void Dispose() => WriterPool.Recycle(this);
    }

    /// <summary>
    /// Pool of NetworkWriters
    /// <para>Use this pool instead of <see cref="Writer">NetworkWriter</see> to reduce memory allocation</para>
    /// </summary>
    //[CodegenIncludeInternal]
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
