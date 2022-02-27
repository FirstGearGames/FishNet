using FishNet.Connection;
using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Utility.Performance
{
    /// <summary>
    /// Various ListCache instances that may be used on the Unity thread.
    /// </summary>
    public static class ListCaches
    {
        /// <summary>
        /// Cache for NetworkObjects.
        /// </summary>
        public static ListCache<NetworkObject> NetworkObjectCache = new ListCache<NetworkObject>();
        /// <summary>
        /// Cache for NetworkBehaviours.
        /// </summary>
        public static ListCache<NetworkBehaviour> NetworkBehaviourCache = new ListCache<NetworkBehaviour>();
        /// <summary>
        /// Cache for Transforms.
        /// </summary>
        public static ListCache<Transform> TransformCache = new ListCache<Transform>();
        /// <summary>
        /// Cache for NetworkConnectios.
        /// </summary>
        public static ListCache<NetworkConnection> NetworkConnectionCache = new ListCache<NetworkConnection>();
        /// <summary>
        /// Cache for ints.
        /// </summary>
        public static ListCache<int> IntCache = new ListCache<int>();
    }

    /// <summary>
    /// Creates a reusable cache of T which auto expands.
    /// </summary>
    public class ListCache<T>
    {
        #region Public.
        /// <summary>
        /// Collection cache is for.
        /// </summary>
        public List<T> Collection;
        /// <summary>
        /// Entries currently written.
        /// </summary>
        public int Written { get; private set; }
        #endregion

        public ListCache()
        {
            Collection = new List<T>();
        }
        public ListCache(int capacity)
        {
            Collection = new List<T>(capacity);
        }

        /// <summary>
        /// Adds a new value to Collection and returns it.
        /// </summary>
        /// <param name="value"></param>
        public T AddReference()
        {
            if (Collection.Count <= Written)
            {
                T next = Activator.CreateInstance<T>();
                Collection.Add(next);
                Written++;
                return next;
            }
            else
            {
                T next = Collection[Written];
                Written++;
                return next;
            }
        }


        /// <summary>
        /// Adds value to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValue(T value)
        {
            if (Collection.Count <= Written)
                Collection.Add(value);
            else
                Collection[Written] = value;

            Written++;
        }

        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(T[] values)
        {
            for (int i = 0; i < values.Length; i++)
                AddValue(values[i]);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(List<T> values)
        {
            for (int i = 0; i < values.Count; i++)
                AddValue(values[i]);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(HashSet<T> values)
        {
            foreach (T item in values)
                AddValue(item);
        }
        /// <summary>
        /// Adds values to Collection.
        /// </summary>
        /// <param name="value"></param>
        public void AddValues(ISet<T> values)
        {
            foreach (T item in values)
                AddValue(item);
        }

        /// <summary>
        /// Resets cache.
        /// </summary>
        public void Reset()
        {
            Written = 0;
        }
    }


}
