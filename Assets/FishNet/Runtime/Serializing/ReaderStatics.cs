using FishNet.Documenting;
using System;
using System.Text;

namespace FishNet.Serializing
{

    /// <summary>
    /// Writes data to a buffer.
    /// </summary>
    [APIExclude]
    internal class ReaderStatics
    {
        /* Since serializing occurs on the main thread this value may
        * be shared among all readers. //multithread
        */

        #region Private.
        /// <summary>
        /// Buffer to copy Guids into.
        /// </summary>
        private static byte[] _guidBuffer = new byte[16];
        /// <summary>
        /// Used to encode strings.
        /// </summary>
        private static readonly UTF8Encoding _encoding = new UTF8Encoding(false, true);
        #endregion

        /// <summary>
        /// Gets the GUID Buffer.
        /// </summary>
        /// <returns></returns>
        public static byte[] GetGuidBuffer()
        {
            return _guidBuffer;
        }

        /// <summary>
        /// Returns a string from data.
        /// </summary>
        public static string GetString(ArraySegment<byte> data)
        {
            return _encoding.GetString(data.Array, data.Offset, data.Count);
        }
   
    }
}