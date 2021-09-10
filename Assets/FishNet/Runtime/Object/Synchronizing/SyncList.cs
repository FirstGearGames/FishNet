using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object.Synchronizing
{

    public class SyncList<T> : SyncBase, IList<T>, IReadOnlyList<T>
    {
        #region Types.
        /// <summary>
        /// Information about how the collection has changed.
        /// </summary>
        private struct ChangeData
        {
            internal SyncListOperation Operation;
            internal int Index;
            internal T Item;

            public ChangeData(SyncListOperation operation, int index, T item)
            {
                Operation = operation;
                Index = index;
                Item = item;
            }
        }

        /// <summary>
        /// Custom enumerator to prevent garbage collection.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            public T Current { get; private set; }
            private readonly SyncList<T> _list;
            private int _index;

            public Enumerator(SyncList<T> list)
            {
                this._list = list;
                _index = -1;
                Current = default;
            }

            public bool MoveNext()
            {
                if (++_index >= _list.Count)
                {
                    return false;
                }
                Current = _list[_index];
                return true;
            }

            public void Reset() => _index = -1;
            object IEnumerator.Current => Current;
            public void Dispose() { }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Implementation from List<T>. Not used.
        /// </summary>
        public bool IsReadOnly => false;
        /// <summary>
        /// Delegate signature for when SyncList changes.
        /// </summary>
        /// <param name="op"></param>
        /// <param name="index"></param>
        /// <param name="oldItem"></param>
        /// <param name="newItem"></param>
        public delegate void SyncListChanged(SyncListOperation op, int index, T oldItem, T newItem, bool asServer);
        /// <summary>
        /// Called when the SyncList changes.
        /// </summary>
        public event SyncListChanged OnChange;
        /// <summary>
        /// Number of objects in the collection.
        /// </summary>
        public int Count => _objects.Count;
        #endregion

        #region Private.        
        /// <summary>
        /// Collection of objects.
        /// </summary>
        private readonly IList<T> _objects = new List<T>();
        /// <summary>
        /// Copy of objects on client portion when acting as a host.
        /// </summary>
        private readonly IList<T> _clientHostObjects = new List<T>();
        /// <summary>
        /// Comparer to see if entries change when calling public methods.
        /// </summary>
        private readonly IEqualityComparer<T> _comparer;
        /// <summary>
        /// Changed data which will be sent next tick.
        /// </summary>
        private readonly List<ChangeData> _changed = new List<ChangeData>();
        /// <summary>
        /// True if values have changed since initialization.
        /// The only reasonable way to reset this during a Reset call is by duplicating the original list and setting all values to it on reset.
        /// </summary>
        private bool _valuesChanged = false;
        #endregion

        public SyncList() : this(EqualityComparer<T>.Default) { }

        public SyncList(IEqualityComparer<T> comparer)
        {
            this._comparer = (comparer == null) ? EqualityComparer<T>.Default : comparer;
        }

        public SyncList(IList<T> objects, IEqualityComparer<T> comparer = null)
        {
            this._comparer = (comparer == null) ? EqualityComparer<T>.Default : comparer;
            this._objects = objects;
            this._clientHostObjects = objects;
        }

        /// <summary>
        /// Adds an operation and invokes locally.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="index"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        private void AddOperation(SyncListOperation operation, int index, T prev, T next)
        {
            /* Only check this if NetworkManager is set.
             * It may not be set if results are being populated
             * before initialization. */
            if (base.NetworkManager == null)
                return;

            if (base.Settings.WritePermission == WritePermission.ServerOnly && !base.NetworkManager.IsServer)
            {
                Debug.LogWarning($"Cannot complete operation {operation} as server when server is not active.");
                return;
            }

            ChangeData change = new ChangeData(operation, index, next);
            _changed.Add(change);

            _valuesChanged = true;
            bool asServer = true;
            OnChange?.Invoke(operation, index, prev, next, asServer);

            base.Dirty();
        }

        /// <summary>
        /// Writes all changed values.
        /// </summary>
        /// <param name="writer"></param>
        ///<param name="resetSyncTick">True to set the next time data may sync.</param>
        public override void Write(PooledWriter writer, bool resetSyncTick = true)
        {
            base.Write(writer, resetSyncTick);
            writer.WriteUInt32((uint)_changed.Count);

            for (int i = 0; i < _changed.Count; i++)
            {
                ChangeData change = _changed[i];
                writer.WriteByte((byte)change.Operation);

                //Clear does not need to write anymore data so it is not included in checks.
                if (change.Operation == SyncListOperation.Add)
                {
                    writer.Write(change.Item);
                }
                else if (change.Operation == SyncListOperation.RemoveAt)
                {
                    writer.WriteUInt32((uint)change.Index);
                }
                else if (change.Operation == SyncListOperation.Insert || change.Operation == SyncListOperation.Set)
                {
                    writer.WriteUInt32((uint)change.Index);
                    writer.Write(change.Item);
                }
            }

            _changed.Clear();
        }

        /// <summary>
        /// Writers all values if not initial values.
        /// </summary>
        /// <param name="writer"></param>
        public override void WriteIfChanged(PooledWriter writer)
        {
            if (!_valuesChanged)
                return;

            base.Write(writer, false);
            writer.WriteUInt32((uint)_objects.Count);
            for (int i = 0; i < _objects.Count; i++)
            {
                writer.WriteByte((byte)SyncListOperation.Add);
                writer.Write(_objects[i]);
            }
        }

        /// <summary>
        /// Sets current values.
        /// </summary>
        /// <param name="reader"></param>
        public override void Read(PooledReader reader)
        {
            bool asServer = false;
            /* When !asServer don't make changes if server is running.
            * This is because changes would have already been made on
            * the server side and doing so again would result in duplicates
            * and potentially overwrite data not yet sent. */
            bool asClientAndHost = (!asServer && base.NetworkManager.IsServer);
            IList<T> objects = (asClientAndHost) ? _clientHostObjects : _objects;

            int changes = (int)reader.ReadUInt32();
            for (int i = 0; i < changes; i++)
            {
                SyncListOperation operation = (SyncListOperation)reader.ReadByte();
                int index = -1;
                T prev = default;
                T next = default;

                //Add.
                if (operation == SyncListOperation.Add)
                {
                    next = reader.Read<T>();
                    index = objects.Count;
                    objects.Add(next);
                }
                //Clear.
                else if (operation == SyncListOperation.Clear)
                {
                    objects.Clear();
                }
                //Insert.
                else if (operation == SyncListOperation.Insert)
                {
                    index = (int)reader.ReadUInt32();
                    next = reader.Read<T>();
                    objects.Insert(index, next);
                }
                //RemoveAt.
                else if (operation == SyncListOperation.RemoveAt)
                {
                    index = (int)reader.ReadUInt32();
                    prev = objects[index];
                    objects.RemoveAt(index);
                }
                //Set
                else if (operation == SyncListOperation.Set)
                {
                    index = (int)reader.ReadUInt32();
                    next = reader.Read<T>();
                    prev = objects[index];
                    objects[index] = next;
                }


                OnChange?.Invoke(operation, index, prev, next, false);
            }

            //If changes were made invoke complete after all have been read.
            if (changes > 0)
                OnChange?.Invoke(SyncListOperation.Complete, -1, default, default, false);
        }

        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _changed.Clear();
            _clientHostObjects.Clear();
        }

        /// <summary>
        /// Adds value.
        /// </summary>
        /// <param name="item"></param>
        public void Add(T item)
        {
            Add(item, true);
        }
        private void Add(T item, bool asServer)
        {
            _objects.Add(item);
            if (asServer)
            {
                if (base.NetworkManager == null)
                    _clientHostObjects.Add(item);
                AddOperation(SyncListOperation.Add, _objects.Count - 1, default, item);
            }
        }
        /// <summary>
        /// Adds a range of values.
        /// </summary>
        /// <param name="range"></param>
        public void AddRange(IEnumerable<T> range)
        {
            foreach (T entry in range)
                Add(entry, true);
        }

        /// <summary>
        /// Clears all values.
        /// </summary>
        public void Clear()
        {
            Clear(true);
        }
        private void Clear(bool asServer)
        {
            _objects.Clear();
            if (asServer)
            {
                if (base.NetworkManager == null)
                    _clientHostObjects.Clear();
                AddOperation(SyncListOperation.Clear, -1, default, default);
            }
        }

        /// <summary>
        /// Returns if value exist.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item)
        {
            return (IndexOf(item) >= 0);
        }

        /// <summary>
        /// Copies values to an array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        public void CopyTo(T[] array, int index)
        {
            _objects.CopyTo(array, index);
        }

        /// <summary>
        /// Gets the index of value.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(T item)
        {
            for (int i = 0; i < _objects.Count; ++i)
                if (_comparer.Equals(item, _objects[i]))
                    return i;
            return -1;
        }

        /// <summary>
        /// Finds index using match.
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        public int FindIndex(Predicate<T> match)
        {
            for (int i = 0; i < _objects.Count; ++i)
                if (match(_objects[i]))
                    return i;
            return -1;
        }

        /// <summary>
        /// Finds value using match.
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        public T Find(Predicate<T> match)
        {
            int i = FindIndex(match);
            return (i != -1) ? _objects[i] : default;
        }

        /// <summary>
        /// Finds all values using match.
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        public List<T> FindAll(Predicate<T> match)
        {
            List<T> results = new List<T>();
            for (int i = 0; i < _objects.Count; ++i)
                if (match(_objects[i]))
                    results.Add(_objects[i]);
            return results;
        }

        /// <summary>
        /// Inserts value at index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, T item)
        {
            Insert(index, item, true);
        }
        private void Insert(int index, T item, bool asServer)
        {
            _objects.Insert(index, item);
            if (asServer)
            {
                if (base.NetworkManager == null)
                    _clientHostObjects.Insert(index, item);
                AddOperation(SyncListOperation.Insert, index, default, item);
            }
        }

        /// <summary>
        /// Inserts a range of values.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="range"></param>
        public void InsertRange(int index, IEnumerable<T> range)
        {
            foreach (T entry in range)
            {
                Insert(index, entry);
                index++;
            }
        }

        /// <summary>
        /// Removes a value.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(T item)
        {
            int index = IndexOf(item);
            bool result = index >= 0;
            if (result)
                RemoveAt(index);

            return result;
        }

        /// <summary>
        /// Removes value at index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="asServer"></param>
        public void RemoveAt(int index)
        {
            RemoveAt(index, true);
        }
        private void RemoveAt(int index, bool asServer)
        {
            T oldItem = _objects[index];
            _objects.RemoveAt(index);
            if (asServer)
            {
                if (base.NetworkManager == null)
                    _clientHostObjects.RemoveAt(index);
                AddOperation(SyncListOperation.RemoveAt, index, oldItem, default);
            }
        }

        /// <summary>
        /// Removes all values within the collection.
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        public int RemoveAll(Predicate<T> match)
        {
            List<T> toRemove = new List<T>();
            for (int i = 0; i < _objects.Count; ++i)
                if (match(_objects[i]))
                    toRemove.Add(_objects[i]);

            foreach (T entry in toRemove)
            {
                Remove(entry);
            }

            return toRemove.Count;
        }

        /// <summary>
        /// Gets or sets value at an index.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public T this[int i]
        {
            get => _objects[i];
            set => Set(i, value, true, true);
        }

        /// <summary>
        /// Sets value at index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void Set(int index, T value)
        {
            Set(index, value, true, true);
        }
        private void Set(int index, T value, bool asServer, bool force)
        {
            bool sameValue = (!force && !_comparer.Equals(_objects[index], value));
            if (!sameValue)
            {
                T prev = _objects[index];
                _objects[index] = value;
                if (asServer)
                {
                    if (base.NetworkManager == null)
                        _clientHostObjects[index] = value;
                        AddOperation(SyncListOperation.Set, index, prev, value);
                }
            }
        }
        /// <summary>
        /// Returns Enumerator for collection.
        /// </summary>
        /// <returns></returns>
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    }
}
