using FishNet.Managing;
using FishNet.Serializing.Helping;
using System;
using System.Collections.Generic;

namespace FishNet.Serializing
{
    /// <summary>
    /// NetworkReader to be used with <see cref="ReaderPool">ReaderPool</see>
    /// </summary>
    [CodegenIncludeInternal]
    public sealed class PooledReader : Reader, IDisposable
    {
        internal PooledReader() { }
        internal PooledReader(byte[] bytes, NetworkManager networkManager) : base(bytes, networkManager) { }
        internal PooledReader(ArraySegment<byte> segment, NetworkManager networkManager) : base(segment, networkManager) { }
        public void Dispose() => ReaderPool.Recycle(this);
    }

    /// <summary>
    /// Pool of NetworkReaders
    /// <para>Use this pool instead of <see cref="Reader">NetworkReader</see> to reduce memory allocation</para>
    /// </summary>
    [CodegenIncludeInternal]
    public static class ReaderPool
    {
        #region Private.
        /// <summary>
        /// Pool of readers.
        /// </summary>
        private static readonly Stack<PooledReader> _pool = new Stack<PooledReader>();
        #endregion

        /// <summary>
        /// Get the next reader in the pool
        /// <para>If pool is empty, creates a new Reader</para>
        /// </summary>
        public static PooledReader GetReader(byte[] bytes, NetworkManager networkManager)
        {
            return GetReader(new ArraySegment<byte>(bytes), networkManager);
        }

        /// <summary>
        /// Get the next reader in the pool or creates a new one if none are available.
        /// </summary>
        public static PooledReader GetReader(ArraySegment<byte> segment, NetworkManager networkManager)
        {
            PooledReader result = (_pool.Count > 0) ? _pool.Pop() : new PooledReader();
            result.Initialize(segment, networkManager);
            return result;
        }

        /// <summary>
        /// Puts reader back into pool
        /// <para>When pool is full, the extra reader is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledReader reader)
        {
            _pool.Push(reader);
        }
    }
}
