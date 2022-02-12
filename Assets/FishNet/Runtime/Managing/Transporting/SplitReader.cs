using FishNet.Serializing;
using System;
using UnityEngine;

namespace FishNet.Managing.Transporting
{

    internal class SplitReader
    {
        #region Private.
        /// <summary>
        /// Tick split is for.
        /// Tick must be a negative value so that it's impossible for the first tick to align.
        /// </summary>
        private long _tick = -1;
        /// <summary>
        /// Expected number of splits.
        /// </summary>
        private int _expectedMessages;
        /// <summary>
        /// Number of splits received so far.
        /// </summary>
        private ushort _receivedMessages;
        /// <summary>
        /// Writer containing split packet combined.
        /// </summary>
        private PooledWriter _writer = WriterPool.GetWriter();
        #endregion

        /// <summary>
        /// Gets split header values.
        /// </summary>
        internal void GetHeader(PooledReader reader, out int expectedMessages)
        {
            expectedMessages = reader.ReadInt32();
        }

        /// <summary>
        /// Combines split data.
        /// </summary>
        internal void Write(uint tick, PooledReader reader, int expectedMessages)
        {
            //New tick which means new split.
            if (tick != _tick)
                Reset(tick, expectedMessages);

            /* Empty remainder of reader into the writer.
             * It does not matter if parts of the reader
             * contain data added after the split because
             * once the split is fully combined the data
             * is parsed as though it came in as one message,
             * which is how data is normally read. */
            ArraySegment<byte> data = reader.ReadArraySegment(reader.Remaining);
            _writer.WriteArraySegment(data);
            _receivedMessages++;
        }

        /// <summary>
        /// Returns if all split messages have been received.
        /// </summary>
        /// <returns></returns>
        internal ArraySegment<byte> GetFullMessage()
        {
            if (_receivedMessages < _expectedMessages)
            {
                return default(ArraySegment<byte>);
            }
            else
            {
                ArraySegment<byte> segment = _writer.GetArraySegment();
                Reset();
                return segment;
            }
        }

        private void Reset(uint tick = 0, int expectedMessages = 0)
        {
            _tick = tick;
            _receivedMessages = 0;
            _expectedMessages = expectedMessages;
            _writer.Reset();
        }

    }


}