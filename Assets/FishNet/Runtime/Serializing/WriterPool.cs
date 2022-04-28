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
        private static readonly Stack<PooledWriter> _largePool = new Stack<PooledWriter>();
        private static readonly Stack<PooledWriter> _smallPool = new Stack<PooledWriter>();

        public static int SizeThreshold = 512;
        #endregion

        /// <summary>
        /// Gets the next writer from the pool
        /// </summary>
        /// <param name="networkManager">Reference of network manager</param>
        /// <param name="permament">If writer will be (semi)permament (taken for a long time) which is not immediately recycled.
        /// Guideline: 
        /// if writer is used inside `using` statement, or gets disposed after block of code is finished, then it is not permament.
        /// if writer is used, not disposed and referenced somewhere, then it is permament.
        /// </param>
        /// <returns></returns>
        public static PooledWriter GetWriter(NetworkManager networkManager, bool permament)
        {
            PooledWriter result;

            if (permament)
            {
                // try small pool or create
                if (_smallPool.Count > 0)
                {
                    result = _smallPool.Pop();
                }
                else
                {
                    result = new PooledWriter();
                }
            }
            else
            {
                // try large pool, then try small pool, then create new
                if (_largePool.Count > 0)
                {
                    result = _largePool.Pop();
                }
                else
                {
                    if (_smallPool.Count > 0)
                    {
                        result = _smallPool.Pop();
                    }
                    else
                    {
                        result = new PooledWriter();
                    }
                }
            }

            result.Reset(networkManager);
            return result;
        }
        /// <summary>
        /// Gets the next writer from the pool
        /// </summary>
        /// <param name="permament">If writer will be (semi)permament (taken for a long time) which is not immediately recycled.
        /// Guideline: 
        /// if writer is used inside `using` statement, or gets disposed after block of code is finished, then it is not permament.
        /// if writer is used, not disposed and referenced somewhere, then it is permament.
        /// </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledWriter GetWriter(bool permament)
        {
            return GetWriter(null, permament);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledWriter GetWriter()
        {
            return GetWriter(null, false);
        }
        /// <summary>
        /// Puts writer back into pool
        /// <para>When pool is full, the extra writer is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledWriter writer)
        {
            if (writer.GetBufferSize() > SizeThreshold)
            {
                _largePool.Push(writer);
            }
            else
            {
                _smallPool.Push(writer);
            }

        }
        public static int SmallPoolSize => _smallPool.Count;

    }


    /// <summary>
    /// Writer which is reused to save on garbage collection and performance.
    /// </summary>
    public sealed class PooledSubWriter : Writer, IDisposable
    {
        public void Dispose() => SubWriterPool.Recycle(this);
    }

    /// <summary>
    /// Collection of PooledSubWriter. Stores and gets PooledWriter.
    /// It is separate, because substream writers can get big (user can write bigger data at will), and if main pool is used (where small sized writers are), lots of GC heavy resizing can occur. 
    /// </summary>
    public static class SubWriterPool
    {
        #region Private.
        /// <summary>
        /// Pool of writers.
        /// </summary>
        private static readonly Stack<PooledSubWriter> _pool = new Stack<PooledSubWriter>();
        #endregion

        public static int PoolSize => _pool.Count;

        /// <summary>
        /// Get the next writer in the pool.
        /// <para>If pool is empty, creates a new Reader</para>
        /// </summary>
        public static PooledSubWriter GetWriter(NetworkManager networkManager)
        {
            PooledSubWriter result = (_pool.Count > 0) ? _pool.Pop() : new PooledSubWriter();
            result.Reset(networkManager);
            return result;
        }
        /// <summary>
        /// Get the next writer in the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledSubWriter GetSubWriter()
        {
            return GetWriter(null);
        }

        /// <summary>
        /// Puts writer back into pool
        /// <para>When pool is full, the extra writer is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledSubWriter writer)
        {
            _pool.Push(writer);
        }
    }
}
