using FishNet.Managing.Object;
using FishNet.Object;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace FishNet.Managing
{
    /// <summary>
    /// This exists so that other objects don't need to manage event subscriptions if available PrefabObjects change at runtime.
    /// </summary>
    public class SpawnablePrefabsDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
        where TValue : PrefabObjects
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new();
        public event Action<TKey, PrefabId, NetworkObject, bool> OnPrefabAdded;
        public event Action<TKey, PrefabId, bool> OnPrefabDiscarded;

        public static implicit operator Dictionary<TKey, TValue>(SpawnablePrefabsDictionary<TKey, TValue> wrapper)
        {
            return new Dictionary<TKey, TValue>(wrapper._dictionary);
        }

        #region IDictionary Implementation
        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set
            {
                if (_dictionary.TryGetValue(key, out var existingValue))
                {
                    UnsubscribeEvents(key, existingValue);
                }
                _dictionary[key] = value;
                SubscribeEvents(key, value);
            }
        }
        public ICollection<TKey> Keys => _dictionary.Keys;
        public ICollection<TValue> Values => _dictionary.Values;
        public int Count => _dictionary.Count;
        public bool IsReadOnly => false;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _dictionary.Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _dictionary.Values;

        public void Add(TKey key, TValue value)
        {
            _dictionary.Add(key, value);
            SubscribeEvents(key, value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            foreach (var kvp in _dictionary)
            {
                UnsubscribeEvents(kvp.Key, kvp.Value);
            }
            _dictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _dictionary.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)_dictionary).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            if (_dictionary.TryGetValue(key, out var value))
            {
                UnsubscribeEvents(key, value);
                return _dictionary.Remove(key);
            }
            return false;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_dictionary).GetEnumerator();
        }
        #endregion

        #region Event Handling
        private void SubscribeEvents(TKey key, TValue item)
        {
            item.OnObjectAdded += (prefabId, nob, asServer) => HandleOnObjectAdded(key, prefabId, nob, asServer);
            item.OnObjectDiscarded += (prefabId, asServer) => HandleOnObjectDiscarded(key, prefabId, asServer);
        }

     

        private void UnsubscribeEvents(TKey key, TValue item)
        {
            item.OnObjectAdded -= (prefabId, nob, asServer) => HandleOnObjectAdded(key, prefabId, nob, asServer);
            item.OnObjectDiscarded -= (prefabId, asServer) => HandleOnObjectDiscarded(key, prefabId, asServer);
        }

        private void HandleOnObjectDiscarded(TKey key, PrefabId prefabId, bool asServer)
        {
            OnPrefabDiscarded(key, prefabId, asServer);
        }

        private void HandleOnObjectAdded(TKey key, PrefabId prefabId, NetworkObject nob, bool asServer)
        {
            OnPrefabAdded?.Invoke(key, prefabId, nob, asServer);
        }
        #endregion
    } 
}