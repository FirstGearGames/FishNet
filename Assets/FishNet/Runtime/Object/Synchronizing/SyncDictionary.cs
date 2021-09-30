using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Object.Synchronizing
{

    public class SyncIDictionary<TKey, TValue> : SyncBase, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {

        #region Types.
        private struct ChangeData
        {
            internal SyncDictionaryOperation Operation;
            internal TKey Key;
            internal TValue Value;

            public ChangeData(SyncDictionaryOperation operation, TKey key, TValue value)
            {
                this.Operation = operation;
                this.Key = key;
                this.Value = value;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Implementation from Dictionary<TKey,TValue>. Not used.
        /// </summary>
        public bool IsReadOnly => false;
        /// <summary>
        /// Delegate signature for when SyncDictionary changes.
        /// </summary>
        /// <param name="op"></param>
        /// <param name="key"></param>
        public delegate void SyncDictionaryChanged(SyncDictionaryOperation op, TKey key, TValue item, bool asServer);
        /// <summary>
        /// Called when the SyncDictionary changes.
        /// </summary>
        public event SyncDictionaryChanged OnChange;
        /// <summary>
        /// Number of objects in the collection.
        /// </summary>
        public int Count => Collection.Count;
        /// <summary>
        /// Keys within the collection.
        /// </summary>
        public ICollection<TKey> Keys => Collection.Keys;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Collection.Keys;
        /// <summary>
        /// Values within the collection.
        /// </summary>
        public ICollection<TValue> Values => Collection.Values;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Collection.Values;
        #endregion

        #region Protected.
        /// <summary>
        /// Collection of objects.
        /// </summary>
        protected readonly IDictionary<TKey, TValue> Collection;
        /// <summary>
        /// Copy of objects on client portion when acting as a host.
        /// </summary>
        protected readonly IDictionary<TKey, TValue> ClientHostCollection;
        #endregion

        #region Private.
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

        public SyncIDictionary(IDictionary<TKey, TValue> objects)
        {
            this.Collection = objects;
            this.ClientHostCollection = objects;
        }

        /// <summary>
        /// Gets the collection being used within this SyncList.
        /// </summary>
        /// <param name="asServer">True if returning the server value, false if client value. The values will only differ when running as host. While asServer is true the most current values on server will be returned, and while false the latest values received by client will be returned.</param>
        /// <returns></returns>
        public Dictionary<TKey, TValue> GetCollection(bool asServer)
        {
            bool asClientAndHost = (!asServer && base.NetworkManager.IsServer);
            IDictionary<TKey, TValue> collection = (asClientAndHost) ? ClientHostCollection : Collection;
            return (collection as Dictionary<TKey, TValue>);
        }


        /// <summary>
        /// Adds an operation and invokes locally.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="index"></param>
        /// <param name="prev"></param>
        /// <param name="next"></param>
        private void AddOperation(SyncDictionaryOperation operation, TKey key, TValue value)
        {
            if (base.Settings.WritePermission == WritePermission.ServerOnly && !base.NetworkBehaviour.IsServer)
            {
                Debug.LogWarning($"Cannot complete operation {operation} as server when server is not active.");
                return;
            }

            ChangeData change = new ChangeData(operation, key, value);
            _changed.Add(change);

            _valuesChanged = true;
            bool asServer = true;
            OnChange?.Invoke(operation, key, value, asServer);

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
                if (change.Operation == SyncDictionaryOperation.Add ||
                    change.Operation == SyncDictionaryOperation.Set)
                {
                    writer.Write(change.Key);
                    writer.Write(change.Value);
                }
                else if (change.Operation == SyncDictionaryOperation.Remove)
                {
                    writer.Write(change.Key);
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
            writer.WriteUInt32((uint)Collection.Count);
            foreach (KeyValuePair<TKey, TValue> item in Collection)
            {
                writer.WriteByte((byte)SyncDictionaryOperation.Add);
                writer.Write(item.Key);
                writer.Write(item.Value);
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
            bool asClientAndHost = (!asServer && base.NetworkBehaviour.IsServer);
            IDictionary<TKey, TValue> objects = (asClientAndHost) ? ClientHostCollection : Collection;

            int changes = (int)reader.ReadUInt32();
            for (int i = 0; i < changes; i++)
            {
                SyncDictionaryOperation operation = (SyncDictionaryOperation)reader.ReadByte();
                TKey key = default;
                TValue value = default;

                /* Add, Set.
                 * Use the Set code for add and set,
                 * especially so collection doesn't throw
                 * if entry has already been added. */
                if (operation == SyncDictionaryOperation.Add || operation == SyncDictionaryOperation.Set)
                {
                    key = reader.Read<TKey>();
                    value = reader.Read<TValue>();
                    objects[key] = value;
                }
                //Clear.
                else if (operation == SyncDictionaryOperation.Clear)
                {
                    objects.Clear();
                }
                //Remove.
                else if (operation == SyncDictionaryOperation.Remove)
                {
                    key = reader.Read<TKey>();
                    objects.Remove(key);
                }

                OnChange?.Invoke(operation, key, value, false);
            }

            //If changes were made invoke complete after all have been read.
            if (changes > 0)
                OnChange?.Invoke(SyncDictionaryOperation.Complete, default, default, false);
        }

        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _changed.Clear();
            ClientHostCollection.Clear();
        }


        /// <summary>
        /// Adds item.
        /// </summary>
        /// <param name="item"></param>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }
        /// <summary>
        /// Adds value.
        /// </summary>
        /// <param name="key"></param>
        public void Add(TKey key, TValue value)
        {
            Add(key, value, true);
        }
        private void Add(TKey key, TValue value, bool asServer)
        {
            Collection.Add(key, value);
            if (asServer)
                AddOperation(SyncDictionaryOperation.Add, key, value);
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
                AddOperation(SyncDictionaryOperation.Clear, default, default);
        }


        /// <summary>
        /// Returns if key exist.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(TKey key)
        {
            return Collection.ContainsKey(key);
        }
        /// <summary>
        /// Returns if item exist.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return TryGetValue(item.Key, out TValue value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        /// <summary>
        /// Copies collection to an array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="offset"></param>
        public void CopyTo([NotNull] KeyValuePair<TKey, TValue>[] array, int offset)
        {
            if (offset <= -1 || offset >= array.Length)
            {
                Debug.LogError($"Index is out of range.");
                return;
            }

            int remaining = array.Length - offset;
            if (remaining < Count)
            {
                Debug.LogError($"Array is not large enough to copy data. Array is of length {array.Length}, index is {offset}, and number of values to be copied is {Count.ToString()}.");
                return;
            }

            int i = offset;
            foreach (KeyValuePair<TKey, TValue> item in Collection)
            {
                array[i] = item;
                i++;
            }
        }


        /// <summary>
        /// Removes a key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(TKey key)
        {
            if (Collection.Remove(key))
            {
                AddOperation(SyncDictionaryOperation.Remove, key, default);
                return true;
            }

            return false;
        }


        /// <summary>
        /// Removes an item.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        /// <summary>
        /// Tries to get value from key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return Collection.TryGetValue(key, out value);
        }

        /// <summary>
        /// Gets or sets value for a key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue this[TKey key]
        {
            get => Collection[key];
            set
            {
                Collection[key] = value;
                AddOperation(SyncDictionaryOperation.Set, key, value);
            }
        }




        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Collection.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Collection.GetEnumerator();

    }

    public class SyncDictionary<TKey, TValue> : SyncIDictionary<TKey, TValue>
    {
        public SyncDictionary() : base(new Dictionary<TKey, TValue>()) { }
        public SyncDictionary(IEqualityComparer<TKey> eq) : base(new Dictionary<TKey, TValue>(eq)) { }
        public new Dictionary<TKey, TValue>.ValueCollection Values => ((Dictionary<TKey, TValue>)Collection).Values;
        public new Dictionary<TKey, TValue>.KeyCollection Keys => ((Dictionary<TKey, TValue>)Collection).Keys;
        public new Dictionary<TKey, TValue>.Enumerator GetEnumerator() => ((Dictionary<TKey, TValue>)Collection).GetEnumerator();

    }
}
