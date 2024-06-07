using FishNet.Managing;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FishNet.Serializing
{
    /// <summary>
    /// Reader which is reused to save on garbage collection and performance.
    /// </summary>
    public sealed class PooledReader : Reader
    {
        internal PooledReader(byte[] bytes, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset) : base(bytes, networkManager, null, source) { }
        internal PooledReader(ArraySegment<byte> segment, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset) : base(segment, networkManager, null, source) { }
        public void Store() => ReaderPool.Store(this);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledReader Retrieve(byte[] bytes, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset)
        {
            return Retrieve(new ArraySegment<byte>(bytes), networkManager, source);
        }

        /// <summary>
        /// Get the next reader in the pool or creates a new one if none are available.
        /// </summary>
        public static PooledReader Retrieve(ArraySegment<byte> segment, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset)
        {
            PooledReader result;
            if (_pool.TryPop(out result))
                result.Initialize(segment, networkManager, source);
            else
                result = new PooledReader(segment, networkManager, source);

            return result;
        }


        /// <summary>
        /// Puts reader back into pool
        /// </summary>
        public static void Store(PooledReader reader)
        {
            _pool.Push(reader);
        }

        /// <summary>
        /// Puts reader back into pool if not null, and nullifies source reference.
        /// </summary>
        public static void StoreAndDefault(ref PooledReader reader)
        {
            if (reader != null)
            {
                _pool.Push(reader);
                reader = null;
            }
        }
    }
}
