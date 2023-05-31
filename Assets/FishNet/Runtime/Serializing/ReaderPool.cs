using FishNet.Managing;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FishNet.Serializing
{
    /// <summary>
    /// Reader which is reused to save on garbage collection and performance.
    /// </summary>
    public sealed class PooledReader : Reader, IDisposable
    {
        internal PooledReader(byte[] bytes, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset) : base(bytes, networkManager, null, source) { }
        internal PooledReader(ArraySegment<byte> segment, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset) : base(segment, networkManager, null, source) { }
        public void Store() => ReaderPool.Store(this);
        [Obsolete("Use Store().")] //Remove on 2024/01/01.
        public void Dispose() => this.Store();
    }

    /// <summary>
    /// Collection of PooledReader. Stores and gets PooledReader.
    /// </summary>
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
        [Obsolete("Use Retrieve(byte[], NetworkManager, DataSource)")] //Remove on 2024/01/01
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledReader GetReader(byte[] bytes, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset) => Retrieve(bytes, networkManager, source); 
        /// <summary>
        /// Get the next reader in the pool
        /// <para>If pool is empty, creates a new Reader</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledReader Retrieve(byte[] bytes, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset)
        {
            return Retrieve(new ArraySegment<byte>(bytes), networkManager, source);
        }

        /// <summary>
        /// Get the next reader in the pool or creates a new one if none are available.
        /// </summary>
        [Obsolete("Use Retrieve(ArraySegment, NetworkManager, DataSource)")] //Remove on 2024/01/01
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledReader GetReader(ArraySegment<byte> segment, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset) => Retrieve(segment, networkManager, source);
        /// <summary>
        /// Get the next reader in the pool or creates a new one if none are available.
        /// </summary>
        public static PooledReader Retrieve(ArraySegment<byte> segment, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset)
        {
            PooledReader result;
            if (_pool.Count > 0)
            {
                result = _pool.Pop();
                result.Initialize(segment, networkManager, source);
            }
            else
            {
                result = new PooledReader(segment, networkManager, source);
            }

            return result;
        }

        /// <summary>
        /// Puts reader back into pool
        /// <para>When pool is full, the extra reader is left for the GC</para>
        /// </summary>
        [Obsolete("Use Store(PooledReader)")] //Remove on 2024/01/01
        public static void Recycle(PooledReader reader) => Store(reader);
        /// <summary>
        /// Puts reader back into pool
        /// <para>When pool is full, the extra reader is left for the GC</para>
        /// </summary>
        public static void Store(PooledReader reader)
        {
            _pool.Push(reader);
        }
    }
}
