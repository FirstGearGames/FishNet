using FishNet.Serializing;
using System;
using UnityEngine;

namespace FishNet.Managing.Transporting
{

    public class SplitReader
    {
        #region Private.
        /// <summary>
        /// Tick split is for.
        /// </summary>
        private uint _tick = uint.MaxValue;
        /// <summary>
        /// Buffer for all split packets.
        /// </summary>
        private byte[] _buffer;
        /// <summary>
        /// Expected number of splits.
        /// </summary>
        private ushort _expected;
        /// <summary>
        /// Called number of splits.
        /// </summary>
        private ushort _received;
        /// <summary>
        /// Write position of split.
        /// </summary>
        private int _position = 0;
        #endregion
      
        /// <summary>
        /// Writes to buffer.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="mtu"></param>
        /// <returns></returns>
        internal ArraySegment<byte> Write(PooledReader reader, int mtu)
        {
            uint tick;
            ushort expected;
            ReadHeader(reader, false, out tick, out expected);

            /* If tick is difference than stored tick
             * then this is a new split. Reset everything. */
            if (_tick != tick)
            {
                _position = 0;
                _received = 0;
                _tick = tick;
                _expected = expected;

                /* Maximum size can not be more than MTU times
                * expected splits. Therefor it's quick and easy
                * to resize only once, if needed. */
                int maximumSize = (mtu * _expected);
                if (_buffer == null || _buffer.Length < maximumSize)
                    Array.Resize(ref _buffer, maximumSize);
            }

            /* Bytes left in the reader. This should
             * always be more than unless data
             * came in corrupt. */
            int remaining = reader.Length - reader.Position;
            //Copy data to buffer.
            if (remaining > 0)
            {
                ArraySegment<byte> readerBuffer = reader.GetArraySegmentBuffer();
                Buffer.BlockCopy(readerBuffer.Array, reader.Position + readerBuffer.Offset, _buffer, _position, remaining);
            }

            //Increase position and received.
            _position += remaining;
            _received += 1;

            //If received all expected then return a new array segment with buffer.
            if (_received == _expected)
                return new ArraySegment<byte>(_buffer, 0, _position);
            //Have not received all, return empty array segment.
            else
                return new ArraySegment<byte>();
        }

        /// <summary>
        /// Readers header data of split packet.
        /// </summary>
        /// <param name="reader"></param>
        internal void ReadHeader(PooledReader reader, bool resetReaderPosition, out uint tick, out ushort expected)
        {
            int startPosition = reader.Position;
            //Skip past packetId for split.
            reader.ReadByte();
            /* Get tick and split and expected
             * split messages. This is included in every
             * split message. */
            tick = reader.ReadUInt32(AutoPackType.Unpacked);
            expected = reader.ReadUInt16();

            if (resetReaderPosition)
                reader.Position = startPosition;
        }
    }


}