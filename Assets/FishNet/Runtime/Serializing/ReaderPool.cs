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
        internal PooledReader(byte[] bytes, NetworkManager networkManager) : base(bytes, networkManager) { }
        internal PooledReader(ArraySegment<byte> segment, NetworkManager networkManager) : base(segment, networkManager) { }
        public void Dispose() => ReaderPool.Recycle(this);
    }

    /// <summary>
    /// Reader which is reused to save on garbage collection and performance. Specialy made for substream - re-uses existing readers so no copying is done
    /// </summary>
    public sealed class PooledSubReader : Reader, IDisposable
    {
        internal PooledSubReader(Reader parentReader, int length) : base(parentReader, length) { }
        public void Dispose()
        {
            this._buffer = null;
            this._guidBuffer = null;
            SubReaderPool.Recycle(this);
        }
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
        public static PooledReader GetReader(byte[] bytes, NetworkManager networkManager)
        {
            return GetReader(new ArraySegment<byte>(bytes), networkManager);
        }

        /// <summary>
        /// Get the next reader in the pool or creates a new one if none are available.
        /// </summary>
        public static PooledReader GetReader(ArraySegment<byte> segment, NetworkManager networkManager)
        {
            PooledReader result;
            if (_pool.Count > 0)
            {
                result = _pool.Pop();
                result.Initialize(segment, networkManager);
            }
            else
            {
                result = new PooledReader(segment, networkManager);
            }

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

    /// <summary>
    /// Collection of PooledSubReader used for SubStreams. Stores and gets PooledSubReader.
    /// </summary>
    public static class SubReaderPool
    {
        #region Private.
        /// <summary>
        /// Pool of subreaders.
        /// </summary>
        private static readonly Stack<PooledSubReader> _pool = new Stack<PooledSubReader>();
        #endregion

        /// <summary>
        /// Get the next subreader in the pool or creates a new one if none are available.
        /// </summary>
        public static PooledSubReader GetSubReader(Reader reader, int length)
        {
            PooledSubReader result;
            if (_pool.Count > 0)
            {
                result = _pool.Pop();
                result.InitializeSubStream(reader, length);
            }
            else
            {
                result = new PooledSubReader(reader, length);
            }

            return result;
        }

        /// <summary>
        /// Puts subreader back into pool
        /// <para>When pool is full, the extra reader is left for the GC</para>
        /// </summary>
        public static void Recycle(PooledSubReader reader)
        {

            _pool.Push(reader);
        }
    }
}
