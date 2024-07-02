#if FISHNET_STABLE_MODE
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace GameKit.Dependencies.Utilities.Types
{

    /// <summary>
    /// Writes values to a collection of a set size, overwriting old values as needed.
    /// </summary>
    public class RingBuffer<T>
    {
#region Types.
        /// <summary>
        /// Custom enumerator to prevent garbage collection.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
#region Public.
            /// <summary>
            /// Current entry in the enumerator. 
            /// </summary>
            public T Current { get; private set; }
            /// <summary>
            /// Actual index of the last enumerated value.
            /// </summary>
            public int ActualIndex
            {
                get
                {
                    int total = (_startIndex + (_read - 1));
                    int capacity = _rollingCollection.Capacity;
                    if (total >= capacity)
                        total -= capacity;

                    return total;
                }
            }
            /// <summary>
            /// Simulated index of the last enumerated value.
            /// </summary>
            public int SimulatedIndex => (_read - 1);
#endregion

#region Private.
            /// <summary>
            /// RollingCollection to use.
            /// </summary>
            private RingBuffer<T> _rollingCollection;
            /// <summary>
            /// Collection to iterate.
            /// </summary>
            private readonly T[] _collection;
            /// <summary>
            /// Number of entries read during the enumeration.
            /// </summary>
            private int _read;
            /// <summary>
            /// Start index of enumerations.
            /// </summary>
            private int _startIndex;
#endregion

            public Enumerator(RingBuffer<T> c)
            {
                _read = 0;
                _startIndex = 0;
                _rollingCollection = c;
                _collection = c.Collection;
                Current = default;
            }

            public bool MoveNext()
            {
                int written = _rollingCollection.Count;
                if (_read >= written)
                {
                    ResetRead();
                    return false;
                }

                int index = (_startIndex + _read);
                int capacity = _rollingCollection.Capacity;
                if (index >= capacity)
                    index -= capacity;
                Current = _collection[index];

                _read++;

                return true;
            }

            /// <summary>
            /// Sets a new start index to begin reading at.
            /// </summary>
            public void SetStartIndex(int index)
            {
                _startIndex = index;
                ResetRead();
            }


            /// <summary>
            /// Sets a new start index to begin reading at.
            /// </summary>
            public void AddStartIndex(int value)
            {
                _startIndex += value;

                int cap = _rollingCollection.Capacity;
                if (_startIndex > cap)
                    _startIndex -= cap;
                else if (_startIndex < 0)
                    _startIndex += cap;

                ResetRead();
            }

            /// <summary>
            /// Resets number of entries read during the enumeration.
            /// </summary>
            public void ResetRead()
            {
                _read = 0;
            }

            /// <summary>
            /// Resets read count.
            /// </summary>
            public void Reset()
            {
                _startIndex = 0;
                ResetRead();
            }

            object IEnumerator.Current => Current;
            public void Dispose() { }
        }

#endregion

        #region Public.
        /// <summary>
        /// Current write index of the collection.
        /// </summary>
        public int WriteIndex { get; private set; }
        /// <summary>
        /// Number of entries currently written.
        /// </summary>
        public int Count => _written;
        /// <summary>
        /// Maximum size of the collection.
        /// </summary>
        public int Capacity => Collection.Length;
        /// <summary>
        /// Collection being used.
        /// </summary>
        public T[] Collection = new T[0];
        /// <summary>
        /// True if initialized.
        /// </summary>
        public bool Initialized { get; private set; }
#endregion

        #region Private.
        /// <summary>
        /// Number of entries written. This will never go beyond the capacity but will be less until capacity is filled.
        /// </summary>
        private int _written;
        /// <summary>
        /// Enumerator for the collection.
        /// </summary>
        private Enumerator _enumerator;
#endregion

        /// <summary>
        /// Initializes the collection at length.
        /// </summary>
        /// <param name="capacity">Size to initialize the collection as. This cannot be changed after initialized.</param>
        public void Initialize(int capacity)
        {
            if (capacity <= 0)
            {
                UnityEngine.Debug.LogError($"Collection length must be larger than 0.");
                return;
            }

            Collection = new T[capacity];
            _enumerator = new Enumerator(this);
            Initialized = true;
        }

        /// <summary>
        /// Clears the collection to default values and resets indexing.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < Collection.Length; i++)
                Collection[i] = default;

            Reset();
        }
        /// <summary>
        /// Resets the collection without clearing.
        /// </summary>
        public void Reset()
        {
            _written = 0;
            WriteIndex = 0;
            _enumerator.Reset();
        }


        /// <summary>
        /// Inserts an entry into the collection.
        /// This is can be an expensive operation on larger buffers.
        /// </summary>
        /// <param name="simulatedIndex">Simulated index to return. A value of 0 would return the first simulated index in the collection.</param>
        /// <param name="data">Data to insert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int simulatedIndex, T data)
        {
            if (!IsInitializedWithError())
                return;

            int realIndex = GetRealIndex(simulatedIndex);
            if (realIndex == -1)
                return;

            int written = _written;
            //If adding to the end.
            if (simulatedIndex == (written - 1))
            {
                Add(data);
                return;
            }

            int lastSimulatedIndex = (written == Capacity) ? (written - 1) : written;

            while (lastSimulatedIndex > simulatedIndex)
            {
                int lRealIndex = GetRealIndex(lastSimulatedIndex, true);
                int lPrevRealIndex = GetRealIndex(lastSimulatedIndex - 1);
                Collection[lRealIndex] = Collection[lPrevRealIndex];
                lastSimulatedIndex--;
            }

            Collection[realIndex] = data;
            //If written was not maxed out then increase it.
            if (written < Capacity)
                IncreaseWritten();
        }

        /// <summary>
        /// Adds an entry to the collection, returning a replaced entry.
        /// </summary>
        /// <param name="data">Data to add.</param>
        /// <returns>Replaced entry. Value will be default if no entry was replaced.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Add(T data)
        {
            if (!IsInitializedWithError())
                return default;

            T current = Collection[WriteIndex];
            Collection[WriteIndex] = data;
            IncreaseWritten();

            return current;
        }

        /// <summary>
        /// Returns value in actual index as it relates to simulated index.
        /// </summary>
        /// <param name="simulatedIndex">Simulated index to return. A value of 0 would return the first simulated index in the collection.</param>
        /// <returns></returns>
        public T this[int simulatedIndex]
        {
            get
            {
                int offset = GetRealIndex(simulatedIndex);
                if (offset >= 0)
                    return Collection[offset];
                else
                    return default;
            }
            set
            {
                int offset = GetRealIndex(simulatedIndex);
                if (offset >= 0)
                    Collection[offset] = value;
            }
        }


        /// <summary>
        /// Increases written count and handles offset changes.
        /// </summary>
        private void IncreaseWritten()
        {
            int capacity = Capacity;

            WriteIndex++;
            _written++;
            //If index would exceed next iteration reset it.
            if (WriteIndex >= capacity)
                WriteIndex = 0;

            /* If written has exceeded capacity
            * then the start index needs to be moved
            * to adjust for overwritten values. */
            if (_written > capacity)
            {
                _written = capacity;
                _enumerator.SetStartIndex(WriteIndex);
            }
        }


        /// <summary>
        /// Returns the real index of the collection using a simulated index.
        /// </summary>
        /// <param name="allowUnusedBuffer">True to allow an index be returned from an unused portion of the buffer so long as it is within bounds.</param>
        private int GetRealIndex(int simulatedIndex, bool allowUnusedBuffer = false)
        {
            if (simulatedIndex >= Capacity)
            {
                return ReturnError();
            }
            else
            {
                int written = _written;
                //May be out of bounds if allowUnusedBuffer is false.
                if (simulatedIndex >= written)
                {
                    if (!allowUnusedBuffer)
                        return ReturnError();
                }
                int offset = (Capacity - written) + simulatedIndex + WriteIndex;
                if (offset >= Capacity)
                    offset -= Capacity;

                return offset;
            }

            int ReturnError()
            {
                UnityEngine.Debug.LogError($"Index {simulatedIndex} is out of range. Collection count is {_written}, Capacity is {Capacity}");
                return -1;
            }
        }

        /// <summary>
        /// Returns Enumerator for the collection.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            if (!IsInitializedWithError())
                return default;

            _enumerator.ResetRead();
            return _enumerator;
        }

        /// <summary>
        /// Removes values from the simulated start of the collection.
        /// </summary>
        /// <param name="fromStart">True to remove from the start, false to remove from the end.</param>
        /// <param name="length">Number of entries to remove.</param>
        public void RemoveRange(bool fromStart, int length)
        {
            if (length == 0)
                return;
            if (length < 0)
            {
                UnityEngine.Debug.LogError($"Negative values cannot be removed.");
                return;
            }
            //Full reset if value is at or more than written.
            if (length >= _written)
            {
                Reset();
                return;
            }

            _written -= length;
            if (fromStart)
            {
                _enumerator.AddStartIndex(length);
            }
            else
            {

                WriteIndex -= length;
                if (WriteIndex < 0)
                    WriteIndex += Capacity;
            }
        }

        /// <summary>
        /// Returns if initialized and errors if not.
        /// </summary>
        /// <returns></returns>
        private bool IsInitializedWithError()
        {
            if (!Initialized)
            {
                UnityEngine.Debug.LogError($"RingBuffer has not yet been initialized.");
                return false;
            }

            return true;
        }

    }

}
#else

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace GameKit.Dependencies.Utilities.Types
{

    /// <summary>
    /// Writes values to a collection of a set size, overwriting old values as needed.
    /// </summary>
    public class RingBuffer<T>
    {
        #region Types.
        /// <summary>
        /// Custom enumerator to prevent garbage collection.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            #region Public.
            /// <summary>
            /// Current entry in the enumerator. 
            /// </summary>
            public T Current { get; private set; }
            /// <summary>
            /// Actual index of the last enumerated value.
            /// </summary>
            public int ActualIndex
            {
                get
                {
                    int total = (_startIndex + (_read - 1));
                    int capacity = _rollingCollection.Capacity;
                    if (total >= capacity)
                        total -= capacity;

                    return total;
                }
            }
            /// <summary>
            /// Simulated index of the last enumerated value.
            /// </summary>
            public int SimulatedIndex => (_read - 1);
            #endregion

            #region Private.
            /// <summary>
            /// RollingCollection to use.
            /// </summary>
            private RingBuffer<T> _rollingCollection;
            /// <summary>
            /// Collection to iterate.
            /// </summary>
            private readonly T[] _collection;
            /// <summary>
            /// Number of entries read during the enumeration.
            /// </summary>
            private int _read;
            /// <summary>
            /// Start index of enumerations.
            /// </summary>
            private int _startIndex;
            #endregion

            public Enumerator(RingBuffer<T> c)
            {
                _read = 0;
                _startIndex = 0;
                _rollingCollection = c;
                _collection = c.Collection;
                Current = default;
            }

            public bool MoveNext()
            {
                int written = _rollingCollection.Count;
                if (_read >= written)
                {
                    ResetRead();
                    return false;
                }

                int index = (_startIndex + _read);
                int capacity = _rollingCollection.Capacity;
                if (index >= capacity)
                    index -= capacity;
                Current = _collection[index];

                _read++;

                return true;
            }

            /// <summary>
            /// Sets a new start index to begin reading at.
            /// </summary>
            public void SetStartIndex(int index)
            {
                _startIndex = index;
                ResetRead();
            }


            /// <summary>
            /// Sets a new start index to begin reading at.
            /// </summary>
            public void AddStartIndex(int value)
            {
                _startIndex += value;

                int cap = _rollingCollection.Capacity;
                if (_startIndex > cap)
                    _startIndex -= cap;
                else if (_startIndex < 0)
                    _startIndex += cap;

                ResetRead();
            }

            /// <summary>
            /// Resets number of entries read during the enumeration.
            /// </summary>
            public void ResetRead()
            {
                _read = 0;
            }

            /// <summary>
            /// Resets read count.
            /// </summary>
            public void Reset()
            {
                _startIndex = 0;
                ResetRead();
            }

            object IEnumerator.Current => Current;
            public void Dispose() { }
        }

        #endregion

        #region Public.
        /// <summary>
        /// Current write index of the collection.
        /// </summary>
        public int WriteIndex { get; private set; }
        /// <summary>
        /// Number of entries currently written.
        /// </summary>
        public int Count => _written;
        /// <summary>
        /// Maximum size of the collection.
        /// </summary>
        public int Capacity;
        /// <summary>
        /// Collection being used.
        /// </summary>
        public T[] Collection = new T[0];
        /// <summary>
        /// True if initialized.
        /// </summary>
        public bool Initialized { get; private set; }
        #endregion

        #region Private.
        /// <summary>
        /// Number of entries written. This will never go beyond the capacity but will be less until capacity is filled.
        /// </summary>
        private int _written;
        /// <summary>
        /// Enumerator for the collection.
        /// </summary>
        private Enumerator _enumerator;
        #endregion

        /// <summary>
        /// Initializes the collection at length.
        /// </summary>
        /// <param name="capacity">Size to initialize the collection as. This cannot be changed after initialized.</param>
        public void Initialize(int capacity)
        {
            if (capacity <= 0)
            {
                UnityEngine.Debug.LogError($"Collection length must be larger than 0.");
                return;
            }

            if (Collection == null)
            {
                GetNewCollection();
            }
            else if (Collection.Length < capacity)
            {
                ArrayPool<T>.Shared.Return(Collection);
                GetNewCollection();
            }

            Capacity = capacity;
            Initialized = true;

            void GetNewCollection() => Collection = ArrayPool<T>.Shared.Rent(capacity);
        }

        /// <summary>
        /// Clears the collection to default values and resets indexing.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
                Collection[i] = default;

            _written = 0;
            WriteIndex = 0;
            _enumerator.Reset();
        }

        /// <summary>
        /// Resets the collection without clearing.
        /// </summary>
        [Obsolete("This method no longer functions. Use Clear() instead.")] //Remove on 2024/06/01.
        public void Reset() { }

        /// <summary>
        /// Inserts an entry into the collection.
        /// This is can be an expensive operation on larger buffers.
        /// </summary>
        /// <param name="simulatedIndex">Simulated index to return. A value of 0 would return the first simulated index in the collection.</param>
        /// <param name="data">Data to insert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int simulatedIndex, T data)
        {
            if (!IsInitializedWithError())
                return;

            int realIndex = GetRealIndex(simulatedIndex);
            if (realIndex == -1)
                return;

            int written = _written;
            //If adding to the end.
            if (simulatedIndex == (written - 1))
            {
                Add(data);
                return;
            }

            int lastSimulatedIndex = (written == Capacity) ? (written - 1) : written;

            while (lastSimulatedIndex > simulatedIndex)
            {
                int lRealIndex = GetRealIndex(lastSimulatedIndex, true);
                int lPrevRealIndex = GetRealIndex(lastSimulatedIndex - 1);
                Collection[lRealIndex] = Collection[lPrevRealIndex];
                lastSimulatedIndex--;
            }

            Collection[realIndex] = data;
            //If written was not maxed out then increase it.
            if (written < Capacity)
                IncreaseWritten();
        }

        /// <summary>
        /// Adds an entry to the collection, returning a replaced entry.
        /// </summary>
        /// <param name="data">Data to add.</param>
        /// <returns>Replaced entry. Value will be default if no entry was replaced.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Add(T data)
        {
            if (!IsInitializedWithError())
                return default;

            T current = Collection[WriteIndex];
            Collection[WriteIndex] = data;
            IncreaseWritten();

            return current;
        }

        /// <summary>
        /// Returns value in actual index as it relates to simulated index.
        /// </summary>
        /// <param name="simulatedIndex">Simulated index to return. A value of 0 would return the first simulated index in the collection.</param>
        /// <returns></returns>
        public T this[int simulatedIndex]
        {
            get
            {
                int offset = GetRealIndex(simulatedIndex);
                if (offset >= 0)
                    return Collection[offset];
                else
                    return default;
            }
            set
            {
                int offset = GetRealIndex(simulatedIndex);
                if (offset >= 0)
                    Collection[offset] = value;
            }
        }


        /// <summary>
        /// Increases written count and handles offset changes.
        /// </summary>
        private void IncreaseWritten()
        {
            int capacity = Capacity;

            WriteIndex++;
            _written++;
            //If index would exceed next iteration reset it.
            if (WriteIndex >= capacity)
                WriteIndex = 0;

            /* If written has exceeded capacity
            * then the start index needs to be moved
            * to adjust for overwritten values. */
            if (_written > capacity)
            {
                _written = capacity;
                _enumerator.SetStartIndex(WriteIndex);
            }
        }


        /// <summary>
        /// Returns the real index of the collection using a simulated index.
        /// </summary>
        /// <param name="allowUnusedBuffer">True to allow an index be returned from an unused portion of the buffer so long as it is within bounds.</param>
        private int GetRealIndex(int simulatedIndex, bool allowUnusedBuffer = false)
        {
            if (simulatedIndex >= Capacity)
            {
                return ReturnError();
            }
            else
            {
                int written = _written;
                //May be out of bounds if allowUnusedBuffer is false.
                if (simulatedIndex >= written)
                {
                    if (!allowUnusedBuffer)
                        return ReturnError();
                }
                int offset = (Capacity - written) + simulatedIndex + WriteIndex;
                if (offset >= Capacity)
                    offset -= Capacity;

                return offset;
            }

            int ReturnError()
            {
                UnityEngine.Debug.LogError($"Index {simulatedIndex} is out of range. Collection count is {_written}, Capacity is {Capacity}");
                return -1;
            }
        }

        /// <summary>
        /// Returns Enumerator for the collection.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            if (!IsInitializedWithError())
                return default;

            _enumerator.ResetRead();
            return _enumerator;
        }

        /// <summary>
        /// Removes values from the simulated start of the collection.
        /// </summary>
        /// <param name="fromStart">True to remove from the start, false to remove from the end.</param>
        /// <param name="length">Number of entries to remove.</param>
        public void RemoveRange(bool fromStart, int length)
        {
            if (length == 0)
                return;
            if (length < 0)
            {
                UnityEngine.Debug.LogError($"Negative values cannot be removed.");
                return;
            }
            //Full reset if value is at or more than written.
            if (length >= _written)
            {
                Clear();
                return;
            }

            _written -= length;
            if (fromStart)
            {
                _enumerator.AddStartIndex(length);
            }
            else
            {

                WriteIndex -= length;
                if (WriteIndex < 0)
                    WriteIndex += Capacity;
            }
        }

        /// <summary>
        /// Returns if initialized and errors if not.
        /// </summary>
        /// <returns></returns>
        private bool IsInitializedWithError()
        {
            if (!Initialized)
            {
                UnityEngine.Debug.LogError($"RingBuffer has not yet been initialized.");
                return false;
            }

            return true;
        }

    }

}


#endif