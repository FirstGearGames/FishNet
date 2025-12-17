using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GameKit.Dependencies.Utilities
{
    /// <summary>
    /// A striped ring-buffer that stores N independent queues addressed by i = 0..N-1.
    /// Backing storage uses NativeList-based stripes with a fixed per-queue capacity.
    /// Designed for job-friendly, per-index parallel work without cross contention.
    /// </summary>
    public struct StripedRingQueue<T> : IDisposable
        where T : unmanaged
    {
        /// <summary>
        /// Backing storage for all stripes; length equals _queueCount * _capacity.
        /// </summary>
        [NativeDisableParallelForRestriction] private NativeList<T> _data;
        /// <summary>
        /// Per-queue head (read index)
        /// Advances on dequeue operations modulo _capacity.
        /// </summary>
        [NativeDisableParallelForRestriction]  private NativeList<int> _head;
        /// <summary>
        /// Per-queue item count.
        /// Always clamped to the range [0.._capacity].
        /// </summary>
        [NativeDisableParallelForRestriction] private NativeList<int> _count;
        /// <summary>
        /// Compact metadata buffer stored in native memory:
        /// [0] = fixed per-queue capacity, [1] = current queue count.
        /// </summary>
        [NativeDisableParallelForRestriction] private NativeArray<int> _meta;
        
        /// <summary>
        /// True when internal lists are allocated and usable.
        /// </summary>
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _data.IsCreated && _head.IsCreated && _count.IsCreated && _meta.IsCreated;
        }
        /// <summary>
        /// Fixed capacity per queue (ring size).
        /// </summary>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _meta[0];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => _meta[0] = value;
        }
        /// <summary>
        /// Number of independent queues (stripes).
        /// </summary>
        public int QueueCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _meta[1];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set => _meta[1] = value;
        }
        /// <summary>
        /// Total addressable storage, equal to QueueCount * Capacity.
        /// </summary>
        public int TotalCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => QueueCount * Capacity;
        }
        
        /// <summary>
        /// Indexer for direct access by queue index and raw ring index (0..Capacity-1).
        /// Does not account for head/count; use GetCount/Clear/Enqueue/Dequeue for logical queue semantics.
        /// </summary>
        public T this[int queueIndex, int simulatedIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                int offset = GetRealOffset(queueIndex, simulatedIndex);
                return _data[offset];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                int offset = GetRealOffset(queueIndex, simulatedIndex);
                _data[offset] = value;
            }
        }
        
        /// <summary>
        /// Constructs the striped ring with an initial queue count and per-queue capacity.
        /// Allocates NativeList storage and zeroes head/count for all stripes.
        /// </summary>
        public StripedRingQueue(int initialQueueCount, int capacity, Allocator allocator)
        {
            if (initialQueueCount < 0) throw new ArgumentOutOfRangeException(nameof(initialQueueCount));
            if (capacity <= 0)         throw new ArgumentOutOfRangeException(nameof(capacity));

            _meta = new NativeArray<int>(2, allocator, NativeArrayOptions.UninitializedMemory);
            _meta[0] = capacity;
            _meta[1] = initialQueueCount;

            _data  = new NativeList<T>(math.max(1, initialQueueCount * capacity), allocator);
            _head  = new NativeList<int>(math.max(1, initialQueueCount), allocator);
            _count = new NativeList<int>(math.max(1, initialQueueCount), allocator);

            _data.ResizeUninitialized(initialQueueCount * capacity);
            _head.ResizeUninitialized(initialQueueCount);
            _count.ResizeUninitialized(initialQueueCount);

            for (int i = 0; i < initialQueueCount; i++)
            {
                _head[i]  = 0;
                _count[i] = 0;
            }
        }

        /// <summary>
        /// Disposes all internal lists synchronously.
        /// Ensure that no jobs are accessing this storage when disposing.
        /// </summary>
        public void Dispose()
        {
            if (_data.IsCreated)  _data.Dispose();
            if (_head.IsCreated)  _head.Dispose();
            if (_count.IsCreated) _count.Dispose();
            if (_meta.IsCreated)  _meta.Dispose();
        }

        /// <summary>
        /// Schedules disposal of internal lists and returns a combined JobHandle.
        /// Use this to free storage once dependent jobs have completed.
        /// </summary>
        public JobHandle Dispose(JobHandle inputDeps)
        {
            JobHandle h = inputDeps;
            if (_data.IsCreated)  h = _data.Dispose(h);
            if (_head.IsCreated)  h = _head.Dispose(h);
            if (_count.IsCreated) h = _count.Dispose(h);
            if (_meta.IsCreated)  h = _meta.Dispose(h);
            return h;
        }

        /// <summary>
        /// Adds a new empty queue (stripe) and returns its index.
        /// Grows the data buffer by Capacity and zeroes the stripe's head/count.
        /// </summary>
        public int AddQueue()
        {
            int capacity   = Capacity;
            int queueCount = QueueCount;

            int newIndex = queueCount;

            int newDataLen = (newIndex + 1) * capacity;
            if (_data.Capacity < newDataLen) _data.Capacity = newDataLen;
            _data.ResizeUninitialized(newDataLen);

            _head.Add(0);
            _count.Add(0);

            QueueCount = newIndex + 1;
            return newIndex;
        }

        /// <summary>
        /// Removes the queue at the given index by swapping with the last stripe,
        /// then shrinking storage by one stripe. Data swap is O(Capacity).
        /// </summary>
        public void RemoveQueueAtSwapBack(int index)
        {
            int queueCount = QueueCount;
            int capacity   = Capacity;

            int last = queueCount - 1;
            if ((uint)index >= (uint)queueCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (last < 0)
                return;

            if (index != last)
            {
                int a = index * capacity;
                int b = last  * capacity;

                for (int k = 0; k < capacity; k++)
                {
                    (_data[a + k], _data[b + k]) = (_data[b + k], _data[a + k]);
                }
            }

            _data.ResizeUninitialized(_data.Length - capacity);
            _head.RemoveAtSwapBack(index);
            _count.RemoveAtSwapBack(index);

            QueueCount = last;
        }
        
        /// <summary>
        /// Returns the current number of items in queue i.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCount(int i) => _count[i];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int BaseOffset(int i) => i * Capacity;

        /// <summary>
        /// Returns the real index of the collection using a simulated index.
        /// </summary>
        /// <param name="queueIndex"></param>
        /// <param name="simulatedIndex"></param>
        /// <param name="allowUnusedBuffer">True to allow an index be returned from an unused portion of the buffer so long as it is within bounds.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetRealOffset(int queueIndex, int simulatedIndex, bool allowUnusedBuffer = false)
        {
            int capacity   = Capacity;
            int queueCount = QueueCount;

            if ((uint)queueIndex >= (uint)queueCount)
                throw new ArgumentOutOfRangeException(nameof(queueIndex));
            if ((uint)simulatedIndex >= (uint)capacity)
                throw new ArgumentOutOfRangeException(nameof(simulatedIndex));

            int count = _count[queueIndex];
            if (simulatedIndex >= count && !allowUnusedBuffer)
                throw new ArgumentOutOfRangeException(
                    nameof(simulatedIndex),
                    $"Index {simulatedIndex} >= item count {count} in queue {queueIndex}");

            int head   = _head[queueIndex];
            int offset = (head + simulatedIndex) % capacity;
            return BaseOffset(queueIndex) + offset;
        }
        
        /// <summary>
        /// Clears queue i by resetting head and count to zero.
        /// Stored values remain but are considered invalid.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(int i)
        {
            _head[i]  = 0;
            _count[i] = 0;
        }

        /// <summary>
        /// Enqueues 'value' into queue i; overwrites the oldest item when full.
        /// Main-thread only unless no concurrent access to the same i is guaranteed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(int i, in T value)
        {
            int capacity = Capacity;

            int h = _head[i];
            int c = _count[i];
            int baseOff = BaseOffset(i);
            int tail = (h + c) % capacity;

            _data[baseOff + tail] = value;

            if (c < capacity)
            {
                _count[i] = c + 1;
            }
            else
            {
                _head[i] = (h + 1) % capacity; // overwrite oldest
            }
        }

        /// <summary>
        /// Tries to dequeue one item from queue i into 'value'.
        /// Returns false when the queue is empty.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(int i, out T value)
        {
            int c = _count[i];
            if (c == 0)
            {
                value = default;
                return false;
            }

            int capacity = Capacity;

            int h = _head[i];
            int baseOff = BaseOffset(i);
            value = _data[baseOff + h];

            _head[i] = (h + 1) % capacity;
            _count[i] = c - 1;
            return true;
        }

        /// <summary>
        /// Dequeues up to 'n' items from queue i and returns how many were removed.
        /// The last removed item (if any) is written to 'last'.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DequeueUpTo(int i, int n, out T last)
        {
            int c = _count[i];
            int drop = math.clamp(n, 0, c);
            if (drop == 0)
            {
                last = default;
                return 0;
            }

            int capacity = Capacity;

            int h = _head[i];
            int baseOff = BaseOffset(i);
            int lastIdx = (h + drop - 1) % capacity;

            last = _data[baseOff + lastIdx];

            _head[i]  = (h + drop) % capacity;
            _count[i] = c - drop;
            return drop;
        }
        
        /// <summary>
        /// Peeks the next entry from i queue.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek(int i)
        {
            int c = _count[i];
            if (c == 0)
                throw new InvalidOperationException($"{nameof(StripedRingQueue<T>)} of type {typeof(T).Name} is empty.");

            int h = _head[i];
            int baseOff = BaseOffset(i);
            return _data[baseOff + h];
        }

        /// <summary>
        /// Tries to peek the next entry from queue i.
        /// </summary>
        /// <param name="i"></param>
        /// <param name = "result">Peeked entry.</param>
        /// <returns>True if an entry existed to peek.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(int i, out T result)
        {
            int c = _count[i];
            if (c == 0)
            {
                result = default;
                return false;
            }

            int h = _head[i];
            int baseOff = BaseOffset(i);
            result = _data[baseOff + h];
            return true;
        }
    }
}