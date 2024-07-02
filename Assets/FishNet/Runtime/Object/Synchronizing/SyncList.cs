using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Synchronizing
{
    [System.Serializable]
    public class SyncList<T> : SyncBase, IList<T>, IReadOnlyList<T>
    { 
        #region Types.
        /// <summary>
        /// Information needed to invoke a callback.
        /// </summary>
        private struct CachedOnChange
        {
            internal readonly SyncListOperation Operation;
            internal readonly int Index;
            internal readonly T Previous;
            internal readonly T Next;

            public CachedOnChange(SyncListOperation operation, int index, T previous, T next)
            {
                Operation = operation;
                Index = index;
                Previous = previous;
                Next = next;
            }
        }

        /// <summary>
        /// Information about how the collection has changed.
        /// </summary>
        private struct ChangeData
        {
            internal readonly SyncListOperation Operation;
            internal readonly int Index;
            internal readonly T Item;

            public ChangeData(SyncListOperation operation, int index, T item)
            {
                Operation = operation;
                Index = index;
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
        /// <param name="op"></param>
        /// <param name="index"></param>
        /// <param name="oldItem"></param>
        /// <param name="newItem"></param>
        [APIExclude]
        public delegate void SyncListChanged(SyncListOperation op, int index, T oldItem, T newItem, bool asServer);
        /// <summary>
        /// Called when the SyncList changes.
        /// </summary>
        public event SyncListChanged OnChange;
        /// <summary>
        /// Collection of objects.
        /// </summary>
        public List<T> Collection;
        /// <summary>
        /// Copy of objects on client portion when acting as a host.
        /// </summary>
        [HideInInspector]
        public List<T> ClientHostCollection;
        /// <summary>
        /// Number of objects in the collection.
        /// </summary>
        public int Count => Collection.Count;
        #endregion

        #region Private.        
        /// <summary>
        /// Values upon initialization.
        /// </summary>
        private List<T> _initialValues;
        /// <summary>
        /// Comparer to see if entries change when calling public methods.
        /// </summary>
        private readonly IEqualityComparer<T> _comparer;
        /// <summary>
        /// Changed data which will be sent next tick.
        /// </summary>
        private List<ChangeData> _changed;
        /// <summary>
        /// Server OnChange events waiting for start callbacks.
        /// </summary>
        private List<CachedOnChange> _serverOnChanges;
        /// <summary>
        /// Client OnChange events waiting for start callbacks.
        /// </summary>
        private List<CachedOnChange> _clientOnChanges;
        /// <summary>
        /// True if values have changed since initialization.
        /// The only reasonable way to reset this during a Reset call is by duplicating the original list and setting all values to it on reset.
        /// </summary>
        private bool _valuesChanged;
        /// <summary>
        /// True to send all values in the next WriteDelta.
        /// </summary>
        private bool _sendAll;
        #endregion

        #region Constructors.
        public SyncList(SyncTypeSettings settings = new SyncTypeSettings()) : this(CollectionCaches<T>.RetrieveList(), EqualityComparer<T>.Default, settings) { }
        public SyncList(IEqualityComparer<T> comparer, SyncTypeSettings settings = new SyncTypeSettings()) : this(new List<T>(), (comparer == null) ? EqualityComparer<T>.Default : comparer, settings) { }
        public SyncList(List<T> collection, IEqualityComparer<T> comparer = null, SyncTypeSettings settings = new SyncTypeSettings()) : base(settings)
        {
            _comparer = (comparer == null) ? EqualityComparer<T>.Default : comparer;
            Collection = collection;
            ClientHostCollection = CollectionCaches<T>.RetrieveList();

            _initialValues = CollectionCaches<T>.RetrieveList();
            _changed = CollectionCaches<ChangeData>.RetrieveList();
            _serverOnChanges = CollectionCaches<CachedOnChange>.RetrieveList();
            _clientOnChanges = CollectionCaches<CachedOnChange>.RetrieveList();

            //Add each in collection to clienthostcollection.
            foreach (T item in collection)
                ClientHostCollection.Add(item);
        }
        #endregion

        #region Deconstructor.
        ~SyncList()
        {
            CollectionCaches<T>.StoreAndDefault(ref Collection);
            CollectionCaches<T>.StoreAndDefault(ref ClientHostCollection);
            CollectionCaches<T>.StoreAndDefault(ref _initialValues);
            CollectionCaches<ChangeData>.StoreAndDefault(ref _changed);
            CollectionCaches<CachedOnChange>.StoreAndDefault(ref _serverOnChanges);
            CollectionCaches<CachedOnChange>.StoreAndDefault(ref _clientOnChanges);
        }
        #endregion

        /// <summary>
        /// Called when the SyncType has been registered, but not yet initialized over the network.
        /// </summary>
        protected override void Initialized()
        {
            base.Initialized();

            //Initialize collections if needed. OdinInspector can cause them to become deinitialized.
#if ODIN_INSPECTOR
            if (_initialValues == null) _initialValues = new();
            if (_changed == null) _changed = new();
            if (_serverOnChanges == null) _serverOnChanges = new();
            if (_clientOnChanges == null) _clientOnChanges = new();
#endif

            foreach (T item in Collection)
                _initialValues.Add(item);
        }

        /// <summary>
        /// Gets the collection being used within this SyncList.
        /// </summary>
        /// <param name="asServer">True if returning the server value, false if client value. The values will only differ when running as host. While asServer is true the most current values on server will be returned, and while false the latest values received by client will be returned.</param>
        /// <returns></returns>
        public List<T> GetCollection(bool asServer)
        {
            bool asClientAndHost = (!asServer && base.NetworkManager.IsServerStarted);
            List<T> collection = (asClientAndHost) ? ClientHostCollection : Collection;
            return (collection as List<T>);
        }

        /// <summary>
        /// Adds an operation and invokes locally.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="index"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddOperation(SyncListOperation operation, int index, T prev, T next)
        {
            if (!base.IsInitialized)
                return;

            /* asServer might be true if the client is setting the value
            * through user code. Typically synctypes can only be set
            * by the server, that's why it is assumed asServer via user code.
            * However, when excluding owner for the synctype the client should
            * have permission to update the value locally for use with
            * prediction. */
            bool asServerInvoke = (!base.IsNetworkInitialized || base.NetworkBehaviour.IsServerStarted);

            /* Only the adds asServer may set
             * this synctype as dirty and add
             * to pending changes. However, the event may still
             * invoke for clientside. */
            if (asServerInvoke)
            {
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
                    ChangeData change = new ChangeData(operation, index, next);
                    _changed.Add(change);
                }
            }

            InvokeOnChange(operation, index, prev, next, asServerInvoke);
        }

        /// <summary>
        /// Called after OnStartXXXX has occurred.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        internal protected override void OnStartCallback(bool asServer)
        {
            base.OnStartCallback(asServer);
            List<CachedOnChange> collection = (asServer) ? _serverOnChanges : _clientOnChanges;

            if (OnChange != null)
            {
                foreach (CachedOnChange item in collection)
                    OnChange.Invoke(item.Operation, item.Index, item.Previous, item.Next, asServer);
            }

            collection.Clear();
        }

        /// <summary>
        /// Writes all changed values.
        /// </summary>
        /// <param name="writer"></param>
        ///<param name="resetSyncTick">True to set the next time data may sync.</param>
        internal protected override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            //If sending all then clear changed and write full.
            if (_sendAll)
            {
                _sendAll = false;
                _changed.Clear();
                WriteFull(writer);
            }
            else
            {
                base.WriteDelta(writer, resetSyncTick);
                //False for not full write.
                writer.WriteBoolean(false);
                WriteChangeId(writer, false);
                //Number of entries expected.
                writer.WriteInt32(_changed.Count);

                for (int i = 0; i < _changed.Count; i++)
                {
                    ChangeData change = _changed[i];
                    writer.WriteUInt8Unpacked((byte)change.Operation);

                    //Clear does not need to write anymore data so it is not included in checks.
                    if (change.Operation == SyncListOperation.Add)
                    {
                        writer.Write(change.Item);
                    }
                    else if (change.Operation == SyncListOperation.RemoveAt)
                    {
                        writer.WriteInt32(change.Index);
                    }
                    else if (change.Operation == SyncListOperation.Insert || change.Operation == SyncListOperation.Set)
                    {
                        writer.WriteInt32(change.Index);
                        writer.Write(change.Item);
                    }
                }

                _changed.Clear();
            }
        }

        /// <summary>
        /// Writes all values if not initial values.
        /// </summary>
        /// <param name="writer"></param>
        internal protected override void WriteFull(PooledWriter writer)
        {
            if (!_valuesChanged)
                return;

            base.WriteHeader(writer, false);
            //True for full write.
            writer.WriteBoolean(true);
            WriteChangeId(writer, true);

            int count = Collection.Count;
            writer.WriteInt32(count);
            for (int i = 0; i < count; i++)
            {
                writer.WriteUInt8Unpacked((byte)SyncListOperation.Add);
                writer.Write(Collection[i]);
            }
        }

        /// <summary>
        /// Reads and sets the current values for server or client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [APIExclude]
        internal protected override void Read(PooledReader reader, bool asServer)
        {
            /* When !asServer don't make changes if server is running.
            * This is because changes would have already been made on
            * the server side and doing so again would result in duplicates
            * and potentially overwrite data not yet sent. */
            bool asClientAndHost = (!asServer && base.NetworkManager.IsServerStarted);
            //True to warn if this object was deinitialized on the server.
            bool deinitialized = (asClientAndHost && !base.OnStartServerCalled);
            if (deinitialized)
                base.NetworkManager.LogWarning($"SyncType {GetType().Name} received a Read but was deinitialized on the server. Client callback values may be incorrect. This is a ClientHost limitation.");

            List<T> collection = (asClientAndHost) ? ClientHostCollection : Collection;

            //Clear collection since it's a full write.
            bool fullWrite = reader.ReadBoolean();
            if (fullWrite)
                collection.Clear();

            bool ignoreReadChanges = base.ReadChangeId(reader);
            int changes = reader.ReadInt32();

            for (int i = 0; i < changes; i++)
            {
                SyncListOperation operation = (SyncListOperation)reader.ReadUInt8Unpacked();
                int index = -1;
                T prev = default;
                T next = default;

                //Add.
                if (operation == SyncListOperation.Add)
                {
                    next = reader.Read<T>();
                    if (!ignoreReadChanges)
                    {
                        index = collection.Count;
                        collection.Add(next);
                    }
                }
                //Clear.
                else if (operation == SyncListOperation.Clear)
                {
                    if (!ignoreReadChanges)
                        collection.Clear();
                }
                //Insert.
                else if (operation == SyncListOperation.Insert)
                {
                    index = reader.ReadInt32();
                    next = reader.Read<T>();
                    if (!ignoreReadChanges)
                        collection.Insert(index, next);
                }
                //RemoveAt.
                else if (operation == SyncListOperation.RemoveAt)
                {
                    index = reader.ReadInt32();
                    if (!ignoreReadChanges)
                    {
                        prev = collection[index];
                        collection.RemoveAt(index);
                    }
                }
                //Set
                else if (operation == SyncListOperation.Set)
                {
                    index = reader.ReadInt32();
                    next = reader.Read<T>();
                    if (!ignoreReadChanges)
                    {
                        prev = collection[index];
                        collection[index] = next;
                    }
                }

                if (!ignoreReadChanges)
                    InvokeOnChange(operation, index, prev, next, false);
            }

            //If changes were made invoke complete after all have been read.
            if (changes > 0)
                InvokeOnChange(SyncListOperation.Complete, -1, default, default, false);
        }

        /// <summary>
        /// Invokes OnChanged callback.
        /// </summary>
        private void InvokeOnChange(SyncListOperation operation, int index, T prev, T next, bool asServer)
        {
            if (asServer)
            {
                if (base.NetworkBehaviour.OnStartServerCalled)
                    OnChange?.Invoke(operation, index, prev, next, asServer);
                else
                    _serverOnChanges.Add(new CachedOnChange(operation, index, prev, next));
            }
            else
            {
                if (base.NetworkBehaviour.OnStartClientCalled)
                    OnChange?.Invoke(operation, index, prev, next, asServer);
                else
                    _clientOnChanges.Add(new CachedOnChange(operation, index, prev, next));
            }
        }

        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        internal protected override void ResetState(bool asServer)
        {
            base.ResetState(asServer);
            _sendAll = false;
            _changed.Clear();
            ClientHostCollection.Clear();
            Collection.Clear();

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
        public void Add(T item)
        {
            Add(item, true);
        }
        private void Add(T item, bool asServer)
        {
            if (!base.CanNetworkSetValues(true))
                return;

            Collection.Add(item);
            if (asServer)
            {
                if (base.NetworkManager == null)
                    ClientHostCollection.Add(item);
                AddOperation(SyncListOperation.Add, Collection.Count - 1, default, item);
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
            if (!base.CanNetworkSetValues(true))
                return;

            Collection.Clear();
            if (asServer)
            {
                if (base.NetworkManager == null)
                    ClientHostCollection.Clear();
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
            Collection.CopyTo(array, index);
        }

        /// <summary>
        /// Gets the index of value.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(T item)
        {
            for (int i = 0; i < Collection.Count; ++i)
            {
                if (_comparer.Equals(item, Collection[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Finds index using match.
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        public int FindIndex(Predicate<T> match)
        {
            for (int i = 0; i < Collection.Count; ++i)
            {
                if (match(Collection[i]))
                    return i;
            }
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
            return (i != -1) ? Collection[i] : default;
        }

        /// <summary>
        /// Finds all values using match.
        /// </summary>
        /// <param name="match"></param>
        /// <returns></returns>
        public List<T> FindAll(Predicate<T> match)
        {
            List<T> results = new List<T>();
            for (int i = 0; i < Collection.Count; ++i)
            {
                if (match(Collection[i]))
                    results.Add(Collection[i]);
            }
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
            if (!base.CanNetworkSetValues(true))
                return;

            Collection.Insert(index, item);
            if (asServer)
            {
                if (base.NetworkManager == null)
                    ClientHostCollection.Insert(index, item);
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
            if (!base.CanNetworkSetValues(true))
                return;

            T oldItem = Collection[index];
            Collection.RemoveAt(index);
            if (asServer)
            {
                if (base.NetworkManager == null)
                    ClientHostCollection.RemoveAt(index);
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
            for (int i = 0; i < Collection.Count; ++i)
            { 
                if (match(Collection[i]))
                    toRemove.Add(Collection[i]);
            }
            
            foreach (T entry in toRemove)
                Remove(entry);

            return toRemove.Count;
        }

        /// <summary>
        /// Gets or sets value at an index.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public T this[int i]
        {
            get => Collection[i];
            set => Set(i, value, true, true);
        }

        /// <summary>
        /// Dirties the entire collection forcing a full send.
        /// This will not invoke the callback on server.
        /// </summary>
        public void DirtyAll()
        {
            if (!base.IsInitialized)
                return;
            if (!base.CanNetworkSetValues(true))
                return;

            if (base.Dirty())
                _sendAll = true;
        }

        /// <summary>
        /// Looks up obj in Collection and if found marks it's index as dirty.
        /// While using this operation previous value will be the same as next.
        /// This operation can be very expensive, and may fail if your value cannot be compared.
        /// </summary>
        /// <param name="obj">Object to lookup.</param>
        public void Dirty(T obj)
        {
            int index = Collection.IndexOf(obj);
            if (index != -1)
                Dirty(index);
            else
                base.NetworkManager.LogError($"Could not find object within SyncList, dirty will not be set.");
        }
        /// <summary>
        /// Marks an index as dirty.
        /// While using this operation previous value will be the same as next.
        /// </summary>
        /// <param name="index"></param>
        public void Dirty(int index)
        {
            if (!base.CanNetworkSetValues(true))
                return;

            bool asServer = true;
            T value = Collection[index];
            if (asServer)
                AddOperation(SyncListOperation.Set, index, value, value);
        }
        /// <summary>
        /// Sets value at index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void Set(int index, T value, bool force = true)
        {
            Set(index, value, true, force);
        }
        private void Set(int index, T value, bool asServer, bool force)
        {
            if (!base.CanNetworkSetValues(true))
                return;

            bool sameValue = (!force && !_comparer.Equals(Collection[index], value));
            if (!sameValue)
            {
                T prev = Collection[index];
                Collection[index] = value;
                if (asServer)
                {
                    if (base.NetworkManager == null)
                        ClientHostCollection[index] = value;
                    AddOperation(SyncListOperation.Set, index, prev, value);
                }
            }
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

    }
}
