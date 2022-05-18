using FishNet.Documenting;
using FishNet.Managing.Logging;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Utility.Performance;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Synchronizing
{

    public class SyncHashSet<T> : SyncBase, ISet<T>
    {
        #region Types.
        /// <summary>
        /// Information needed to invoke a callback.
        /// </summary>
        private struct CachedOnChange
        {
            internal readonly SyncHashSetOperation Operation;
            internal readonly T Item;

            public CachedOnChange(SyncHashSetOperation operation, T item)
            {
                Operation = operation;
                Item = item;
            }
        }

        /// <summary>
        /// Information about how the collection has changed.
        /// </summary>
        private struct ChangeData
        {
            internal readonly SyncHashSetOperation Operation;
            internal readonly T Item;

            public ChangeData(SyncHashSetOperation operation, T item)
            {
                Operation = operation;

                Item = item;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Implementation from List<T>. Not used.
        /// </summary>
        [APIExclude]
        public bool IsReadOnly => false;
        /// <summary>
        /// Delegate signature for when SyncList changes.
        /// </summary>
        /// <param name="op">Type of change.</param>
        /// <param name="item">Item which was modified.</param>
        /// <param name="asServer">True if callback is occuring on the server.</param>
        [APIExclude]
        public delegate void SyncHashSetChanged(SyncHashSetOperation op, T item, bool asServer);
        /// <summary>
        /// Called when the SyncList changes.
        /// </summary>
        public event SyncHashSetChanged OnChange;
        /// <summary>
        /// Collection of objects.
        /// </summary>
        public readonly ISet<T> Collection;
        /// <summary>
        /// Copy of objects on client portion when acting as a host.
        /// </summary>
        public readonly ISet<T> ClientHostCollection = new HashSet<T>();
        /// <summary>
        /// Number of objects in the collection.
        /// </summary>
        public int Count => Collection.Count;
        #endregion

        #region Private.        
        /// <summary>
        /// ListCache for comparing.
        /// </summary>
        private ListCache<T> _listCache;
        /// <summary>
        /// Values upon initialization.
        /// </summary>
        private ISet<T> _initialValues = new HashSet<T>();
        /// <summary>
        /// Comparer to see if entries change when calling public methods.
        /// </summary>
        private readonly IEqualityComparer<T> _comparer;
        /// <summary>
        /// Changed data which will be sent next tick.
        /// </summary>
        private readonly List<ChangeData> _changed = new List<ChangeData>();
        /// <summary>
        /// Server OnChange events waiting for start callbacks.
        /// </summary>
        private readonly List<CachedOnChange> _serverOnChanges = new List<CachedOnChange>();
        /// <summary>
        /// Client OnChange events waiting for start callbacks.
        /// </summary>
        private readonly List<CachedOnChange> _clientOnChanges = new List<CachedOnChange>();
        /// <summary>
        /// True if values have changed since initialization.
        /// The only reasonable way to reset this during a Reset call is by duplicating the original list and setting all values to it on reset.
        /// </summary>
        private bool _valuesChanged;
        #endregion

        [APIExclude]
        public SyncHashSet() : this(new HashSet<T>(), EqualityComparer<T>.Default) { }
        [APIExclude]
        public SyncHashSet(IEqualityComparer<T> comparer) : this(new HashSet<T>(), (comparer == null) ? EqualityComparer<T>.Default : comparer) { }
        [APIExclude]
        public SyncHashSet(ISet<T> collection, IEqualityComparer<T> comparer = null)
        {
            this._comparer = (comparer == null) ? EqualityComparer<T>.Default : comparer;
            this.Collection = collection;
            //Add each in collection to clienthostcollection.
            foreach (T item in collection)
                ClientHostCollection.Add(item);
        }

        /// <summary>
        /// Called when the SyncType has been registered, but not yet initialized over the network.
        /// </summary>
        protected override void Registered()
        {
            base.Registered();
            foreach (T item in Collection)
                _initialValues.Add(item);
        }

        /// <summary>
        /// Gets the collection being used within this SyncList.
        /// </summary>
        /// <returns></returns>
        public HashSet<T> GetCollection(bool asServer)
        {
            bool asClientAndHost = (!asServer && base.NetworkManager.IsServer);
            ISet<T> collection = (asClientAndHost) ? ClientHostCollection : Collection;
            return (collection as HashSet<T>);
        }

        /// <summary>
        /// Adds an operation and invokes locally.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddOperation(SyncHashSetOperation operation, T item)
        {
            if (!base.IsRegistered)
                return;

            if (base.NetworkManager != null && base.Settings.WritePermission == WritePermission.ServerOnly && !base.NetworkBehaviour.IsServer)
            {
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Cannot complete operation as server when server is not active.");
                return;
            }

            /* Set as changed even if cannot dirty.
            * Dirty is only set when there are observers,
            * but even if there are not observers
            * values must be marked as changed so when
            * there are observers, new values are sent. */
            _valuesChanged = true;

            /* If unable to dirty then do not add to changed.
             * A dirty may fail if the server is not started
             * or if there's no observers. Changed doesn't need
             * to be populated in this situations because clients
             * will get the full collection on spawn. If we
             * were to also add to changed clients would get the full
             * collection as well the changed, which would double results. */
            if (base.Dirty())
            {
                ChangeData change = new ChangeData(operation, item);
                _changed.Add(change);
            }

            bool asServer = true;
            InvokeOnChange(operation, item, asServer);
        }

        /// <summary>
        /// Called after OnStartXXXX has occurred.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        protected internal override void OnStartCallback(bool asServer)
        {
            base.OnStartCallback(asServer);
            List<CachedOnChange> collection = (asServer) ? _serverOnChanges : _clientOnChanges;
            if (OnChange != null)
            {
                foreach (CachedOnChange item in collection)
                    OnChange.Invoke(item.Operation, item.Item, asServer);
            }

            collection.Clear();
        }

        /// <summary>
        /// Writes all changed values.
        /// </summary>
        /// <param name="writer"></param>
        ///<param name="resetSyncTick">True to set the next time data may sync.</param>
        public override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            base.WriteDelta(writer, resetSyncTick);
            writer.WriteUInt32((uint)_changed.Count);

            for (int i = 0; i < _changed.Count; i++)
            {
                ChangeData change = _changed[i];
                writer.WriteByte((byte)change.Operation);

                //Clear does not need to write anymore data so it is not included in checks.
                if (change.Operation == SyncHashSetOperation.Add || change.Operation == SyncHashSetOperation.Remove)
                {
                    writer.Write(change.Item);
                }
            }

            _changed.Clear();
        }

        /// <summary>
        /// Writes all values if not initial values.
        /// </summary>
        /// <param name="writer"></param>
        public override void WriteFull(PooledWriter writer)
        {
            if (!_valuesChanged)
                return;

            base.WriteHeader(writer, false);
            int count = Collection.Count;
            writer.WriteUInt32((uint)count);
            foreach (T item in Collection)
            {
                writer.WriteByte((byte)SyncHashSetOperation.Add);
                writer.Write(item);
            }
        }

        /// <summary>
        /// Sets current values.
        /// </summary>
        /// <param name="reader"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [APIExclude]
        public override void Read(PooledReader reader)
        {
            bool asServer = false;
            /* When !asServer don't make changes if server is running.
            * This is because changes would have already been made on
            * the server side and doing so again would result in duplicates
            * and potentially overwrite data not yet sent. */
            bool asClientAndHost = (!asServer && base.NetworkManager.IsServer);
            ISet<T> collection = (asClientAndHost) ? ClientHostCollection : Collection;

            int changes = (int)reader.ReadUInt32();
            for (int i = 0; i < changes; i++)
            {
                SyncHashSetOperation operation = (SyncHashSetOperation)reader.ReadByte();
                T next = default;

                //Add.
                if (operation == SyncHashSetOperation.Add)
                {
                    next = reader.Read<T>();
                    collection.Add(next);
                }
                //Clear.
                else if (operation == SyncHashSetOperation.Clear)
                {
                    collection.Clear();
                }
                //Remove.
                else if (operation == SyncHashSetOperation.Remove)
                {
                    next = reader.Read<T>();
                    collection.Remove(next);
                }

                InvokeOnChange(operation, next, false);
            }

            //If changes were made invoke complete after all have been read.
            if (changes > 0)
                InvokeOnChange(SyncHashSetOperation.Complete, default, false);
        }

        /// <summary>
        /// Invokes OnChanged callback.
        /// </summary>
        private void InvokeOnChange(SyncHashSetOperation operation, T item, bool asServer)
        {
            if (OnChange != null)
            {
                if (asServer)
                {
                    if (base.NetworkBehaviour.OnStartServerCalled)
                        OnChange.Invoke(operation, item, asServer);
                    else
                        _serverOnChanges.Add(new CachedOnChange(operation, item));
                }
                else
                {
                    if (base.NetworkBehaviour.OnStartClientCalled)
                        OnChange.Invoke(operation, item, asServer);
                    else
                        _clientOnChanges.Add(new CachedOnChange(operation, item));
                }

            }
        }

        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _changed.Clear();
            Collection.Clear();
            ClientHostCollection.Clear();

            foreach (T item in _initialValues)
            {
                Collection.Add(item);
                ClientHostCollection.Add(item);
            }
        }

        /// <summary>
        /// Adds value.
        /// </summary>
        /// <param name="item"></param>
        public bool Add(T item)
        {
            return Add(item, true);
        }
        private bool Add(T item, bool asServer)
        {
            bool result = Collection.Add(item);
            //Only process if remove was successful.
            if (result && asServer)
            {
                if (base.NetworkManager == null)
                    ClientHostCollection.Add(item);
                AddOperation(SyncHashSetOperation.Add, item);
            }

            return result;
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
            Collection.Clear();
            if (asServer)
            {
                if (base.NetworkManager == null)
                    ClientHostCollection.Clear();
                AddOperation(SyncHashSetOperation.Clear, default);
            }
        }

        /// <summary>
        /// Returns if value exist.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item)
        {
            return Collection.Contains(item);
        }

        /// <summary>
        /// Removes a value.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(T item)
        {
            return Remove(item, true);
        }
        private bool Remove(T item, bool asServer)
        {
            bool result = Collection.Remove(item);
            //Only process if remove was successful.
            if (result && asServer)
            {
                if (base.NetworkManager == null)
                    ClientHostCollection.Remove(item);
                AddOperation(SyncHashSetOperation.Remove, item);
            }

            return result;
        }


        /// <summary>
        /// Returns Enumerator for collection.
        /// </summary>
        /// <returns></returns>
        public IEnumerator GetEnumerator() => Collection.GetEnumerator();
        [APIExclude]
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Collection.GetEnumerator();
        [APIExclude]
        IEnumerator IEnumerable.GetEnumerator() => Collection.GetEnumerator();

        public void ExceptWith(IEnumerable<T> other)
        {
            //Again, removing from self is a clear.
            if (other == Collection)
            {
                Clear();
            }
            else
            {
                foreach (T item in other)
                    Remove(item);
            }
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            ISet<T> set;
            if (other is ISet<T> setA)
                set = setA;
            else
                set = new HashSet<T>(other);

            IntersectWith(set);
        }

        private void IntersectWith(ISet<T> other)
        {
            Intersect(Collection);
            if (base.NetworkManager == null)
                Intersect(ClientHostCollection);

            void Intersect(ISet<T> collection)
            {
                if (_listCache == null)
                    _listCache = new ListCache<T>();
                else
                    _listCache.Reset();

                _listCache.AddValues(collection);

                int count = _listCache.Written;
                for (int i = 0; i < count; i++)
                {
                    T entry = _listCache.Collection[i];
                    if (!other.Contains(entry))
                        Remove(entry);
                }
            }

        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return Collection.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return Collection.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return Collection.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return Collection.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            bool result = Collection.Overlaps(other);
            return result;
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return Collection.SetEquals(other);
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            //If calling except on self then that is the same as a clear.
            if (other == Collection)
            {
                Clear();
            }
            else
            {
                foreach (T item in other)
                    Remove(item);
            }
        }

        public void UnionWith(IEnumerable<T> other)
        {
            if (other == Collection)
                return;

            foreach (T item in other)
                Add(item);
        }

        /// <summary>
        /// Adds an item.
        /// </summary>
        /// <param name="item"></param>
        void ICollection<T>.Add(T item)
        {
            Add(item, true);
        }

        /// <summary>
        /// Copies values to an array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        public void CopyTo(T[] array, int index)
        {
            Collection.CopyTo(array, index);
            if (base.NetworkManager == null)
                ClientHostCollection.CopyTo(array, index);
        }
    }
}
