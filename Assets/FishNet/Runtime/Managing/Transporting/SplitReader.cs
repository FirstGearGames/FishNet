using FishNet.Serializing;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing.Transporting
{
    internal sealed class SplitReader
    {
        #region Private.
        /// <summary>
        /// Pending splits keyed by split identifier.
        /// </summary>
        private readonly Dictionary<int, SplitData> _pendingSplits = new();

        /// <summary>
        /// How long in seconds a split may remain incomplete before being discarded.
        /// </summary>
        private const float SPLIT_TIMEOUT = 3;
        #endregion

        private sealed class SplitData
        {
            private const int ArrayThreshold = 64;

            private readonly byte[][] _array;
            private readonly Dictionary<int, byte[]> _dict;

            public readonly int ExpectedMessages;
            public int ReceivedCount;
            public float LastReceivedTime;

            public SplitData(int expectedMessages, float now)
            {
                ExpectedMessages = expectedMessages;
                LastReceivedTime = now;

                if (expectedMessages <= ArrayThreshold)
                    _array = new byte[expectedMessages][];
                else
                    _dict = new Dictionary<int, byte[]>(expectedMessages);
            }

            public bool Contains(int part)
            {
                if (_array != null)
                    return _array[part] != null;

                return _dict.ContainsKey(part);
            }

            public void Set(int part, byte[] data)
            {
                if (_array != null)
                    _array[part] = data;
                else
                    _dict[part] = data;
            }

            public bool TryGet(int part, out byte[] data)
            {
                if (_array != null)
                {
                    data = _array[part];
                    return data != null;
                }

                return _dict.TryGetValue(part, out data);
            }

            public ArraySegment<byte> Assemble()
            {
                byte[][] orderedParts = new byte[ExpectedMessages][];
                int totalSize = 0;

                for (int i = 0; i < ExpectedMessages; i++)
                {
                    if (!TryGet(i, out byte[] part))
                        return default;

                    orderedParts[i] = part;
                    totalSize += part.Length;
                }

                byte[] result = new byte[totalSize];
                int position = 0;

                for (int i = 0; i < orderedParts.Length; i++)
                {
                    byte[] part = orderedParts[i];
                    Buffer.BlockCopy(part, 0, result, position, part.Length);
                    position += part.Length;
                }

                return new ArraySegment<byte>(result);
            }
        }

        internal void GetHeader(PooledReader reader, out int splitId, out int expectedMessages, out int partIndex)
        {
            splitId = reader.ReadInt32();
            expectedMessages = reader.ReadInt32();
            partIndex = reader.ReadInt32();
        }

        internal void Write(int splitId, PooledReader reader, int expectedMessages, int partIndex)
        {
            if (expectedMessages <= 0)
                return;

            float now = Time.unscaledTime;

            if (!_pendingSplits.TryGetValue(splitId, out SplitData splitData))
            {
                splitData = new SplitData(expectedMessages, now);
                _pendingSplits[splitId] = splitData;
            }
            else if (splitData.ExpectedMessages != expectedMessages)
            {
                _pendingSplits.Remove(splitId);
                return;
            }

            if (partIndex < 0 || partIndex >= splitData.ExpectedMessages)
            {
                _pendingSplits.Remove(splitId);
                return;
            }

            if (splitData.Contains(partIndex))
                return;

            byte[] data = reader.ReadArraySegment(reader.Remaining).ToArray();
            splitData.Set(partIndex, data);
            splitData.ReceivedCount++;
            splitData.LastReceivedTime = now;
        }

        internal ArraySegment<byte> GetFullMessage(int splitId)
        {
            if (!_pendingSplits.TryGetValue(splitId, out SplitData splitData))
                return default;

            if (splitData.ReceivedCount < splitData.ExpectedMessages)
                return default;

            ArraySegment<byte> result = splitData.Assemble();
            _pendingSplits.Remove(splitId);
            return result;
        }

        internal void CheckSplitTimeout()
        {
            if (_pendingSplits.Count == 0)
                return;

            float now = Time.unscaledTime;
            List<int> expired = null;

            foreach (KeyValuePair<int, SplitData> kvp in _pendingSplits)
            {
                if (now - kvp.Value.LastReceivedTime > SPLIT_TIMEOUT)
                {
                    expired ??= new List<int>();
                    expired.Add(kvp.Key);
                }
            }

            if (expired == null)
                return;

            for (int i = 0; i < expired.Count; i++)
                _pendingSplits.Remove(expired[i]);
        }

        internal void Reset()
        {
            _pendingSplits.Clear();
        }
    }
}