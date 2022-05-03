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

        private const int SizeThreshold = 1023;
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
                        result.EnsureBufferLength(SizeThreshold+1);
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
}
