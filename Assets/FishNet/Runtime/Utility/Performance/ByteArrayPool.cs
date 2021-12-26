using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FishNet.Utility.Performance
{

    /// <summary>
    /// Retrieves and stores byte arrays using a pooling system.
    /// </summary>
    public static class ByteArrayPool
    {
        /// <summary>
        /// Stored byte arrays.
        /// </summary>
        private static Queue<byte[]> _byteArrays = new Queue<byte[]>();
        /// <summary>
        /// Stored concurrent byte arrays.
        /// </summary>
        private static ConcurrentQueue<byte[]> _concurrentByteArrays = new ConcurrentQueue<byte[]>();

        /// <summary>
        /// Returns a byte array which will be of at lesat minimum length. The returns array must manually be stored.
        /// </summary>
        public static byte[] Retrieve(int minimumLength, bool useConcurrent = false)
        {
            byte[] result = null;

            if (!useConcurrent)
            {
                if (_byteArrays.Count > 0)
                    result = _byteArrays.Dequeue();
            }
            else
            {
                if (_concurrentByteArrays.Count > 0)
                    _concurrentByteArrays.TryDequeue(out result);
            }

            int doubleMinimumLength = (minimumLength * 2);
            if (result == null)
                result = new byte[doubleMinimumLength];
            else if (result.Length < minimumLength)
                Array.Resize(ref result, doubleMinimumLength);

            return result;
        }

        /// <summary>
        /// Stores a byte array for re-use.
        /// </summary>
        public static void Store(byte[] buffer, bool useConcurrent = false)
        {
            if (!useConcurrent)
                _byteArrays.Enqueue(buffer);
            else
                _concurrentByteArrays.Enqueue(buffer);
        }

    }


}