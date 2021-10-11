using System;
using System.Collections.Generic;

namespace FishNet.Utility.Performance
{

    internal static class ByteArrayPool
    {
        /// <summary>
        /// Current buffers.
        /// </summary>
        private static Queue<byte[]> _buffers = new Queue<byte[]>();

        /// <summary>
        /// Tries to return a buffer of the specified length. If one cannot be found, a new one will be made.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] GetArray(int length)
        {
            byte[] result;

            if (_buffers.Count > 0)
            {
                result = _buffers.Dequeue();
                /* Rather than try to find a buffer of appropriate size
                 * grab the first one and if it's not needed length then
                 * resize it. This will create garbage but as this is
                 * done more often it decreases the chances of having
                 * to resize buffers. It also saves performance loss
                 * of removing from the middle of a list when a buffer of
                 * appropriate size is found. */
                if (result.Length < length)
                    Array.Resize(ref result, length);
            }
            else
            {
                result = new byte[length];
            }

            return result;
        }

        /// <summary>
        /// Stores a buffer for re-use.
        /// </summary>
        /// <param name="buffer"></param>
        public static void StoreArray(byte[] buffer)
        {
            _buffers.Enqueue(buffer);
        }
    }


}