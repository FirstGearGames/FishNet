using FishNet.Managing.Object;
using FishNet.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityComponent = UnityEngine.Component;


namespace FishNet.Managing
{
    public partial class NetworkManager : MonoBehaviour
    {
        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Collection to use for spawnable objects.")]
        [SerializeField]
        private PrefabObjects _spawnablePrefabs;
        /// <summary>
        /// Collection to use for spawnable objects.
        /// </summary>
        public PrefabObjects SpawnablePrefabs { get => _spawnablePrefabs; set => _spawnablePrefabs = value; }
        #endregion

        #region Private.
        /// <summary>
        /// Delegates waiting to be invoked when a component is registered.
        /// </summary>
        private Dictionary<string, List<Action<UnityComponent>>> _pendingInvokes = new Dictionary<string, List<Action<UnityComponent>>>();
        /// <summary>
        /// Currently registered components.
        /// </summary>
        private Dictionary<string, UnityComponent> _registeredComponents = new Dictionary<string, UnityComponent>();
        #endregion

        /// <summary>
        /// Gets the index a prefab uses. Can be used in conjuction with GetPrefab.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="asServer">True if to get from the server collection.</param>
        /// <returns>Returns index if found, and -1 if not found.</returns>
        public int GetPrefabIndex(GameObject prefab, bool asServer)
        {
            int count = SpawnablePrefabs.GetObjectCount();
            for (int i = 0; i < count; i++)
            {
                GameObject go = SpawnablePrefabs.GetObject(asServer, i).gameObject;
                if (go == prefab)
                    return i;
            }

            //Fall through, not found.
            return -1;
        }

        /// <summary>
        /// Returns a prefab with prefabId.
        /// This method will bypass object pooling.
        /// </summary>
        /// <param name="prefabId">PrefabId to get.</param>
        /// <param name="asServer">True if getting the prefab asServer.</param>
        public NetworkObject GetPrefab(int prefabId, bool asServer)
        {
            return SpawnablePrefabs.GetObject(asServer, prefabId);
        }


        #region Registered components
        /// <summary>
        /// Invokes an action when a specified component becomes registered. Action will invoke immediately if already registered.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <param name="handler">Action to invoke.</param>
        public void RegisterInvokeOnInstance<T>(Action<UnityComponent> handler) where T : UnityComponent
        {
            T result = GetInstance<T>(false);
            //If not found yet make a pending invoke.
            if (result == default(T))
            {
                string tName = GetInstanceName<T>();
                List<Action<UnityComponent>> handlers;
                if (!_pendingInvokes.TryGetValue(tName, out handlers))
                {
                    handlers = new List<Action<UnityComponent>>();
                    _pendingInvokes[tName] = handlers;
                }

                handlers.Add(handler);
            }
            //Already exist, invoke right away.
            else
            {
                handler.Invoke(result);
            }
        }
        /// <summary>
        /// Removes an action to be invokes when a specified component becomes registered.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <param name="handler">Action to invoke.</param>
        public void UnregisterInvokeOnInstance<T>(Action<UnityComponent> handler) where T : UnityComponent
        {
            string tName = GetInstanceName<T>();
            List<Action<UnityComponent>> handlers;
            if (!_pendingInvokes.TryGetValue(tName, out handlers))
                return;

            handlers.Remove(handler);
            //Do not remove pending to prevent garbage collection later on list recreation.
        }
        /// <summary>
        /// Returns class of type if found within CodegenBase classes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="warn">True to warn if component is not registered.</param>
        /// <returns></returns>
        public T GetInstance<T>(bool warn = true) where T : UnityComponent
        {
            string tName = GetInstanceName<T>();
            if (_registeredComponents.TryGetValue(tName, out UnityComponent result))
                return (T)result;
            else if (warn)
                LogWarning($"Component {tName} is not registered.");

            return default(T);
        }
        /// <summary>
        /// Registers a new component to this NetworkManager.
        /// </summary>
        /// <typeparam name="T">Type to register.</typeparam>
        /// <param name="component">Reference of the component being registered.</param>
        /// <param name="replace">True to replace existing references.</param>
        public void RegisterInstance<T>(T component, bool replace = true) where T : UnityComponent
        {
            string tName = GetInstanceName<T>();
            if (_registeredComponents.ContainsKey(tName) && !replace)
            {
                LogWarning($"Component {tName} is already registered.");
            }
            else
            {
                _registeredComponents[tName] = component;
                RemoveNullPendingDelegates();
                //If in pending invokes also send these out.
                if (_pendingInvokes.TryGetValue(tName, out List<Action<UnityComponent>> dels))
                {
                    for (int i = 0; i < dels.Count; i++)
                        dels[i].Invoke(component);
                    /* Clear delegates but do not remove dictionary entry
                     * to prevent list from being re-initialized. */
                    dels.Clear();
                }
            }
        }
        /// <summary>
        /// Unregisters a component from this NetworkManager.
        /// </summary>
        /// <typeparam name="T">Type to unregister.</typeparam>
        public void UnregisterInstance<T>() where T : UnityComponent
        {
            string tName = GetInstanceName<T>();
            _registeredComponents.Remove(tName);
        }
        /// <summary>
        /// Removes delegates from pending invokes when may have gone missing.
        /// </summary>
        private void RemoveNullPendingDelegates()
        {
            foreach (List<Action<UnityComponent>> handlers in _pendingInvokes.Values)
            {
                for (int i = 0; i < handlers.Count; i++)
                {
                    if (handlers[i] == null)
                    {
                        handlers.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
        /// <summary>
        /// Returns the name to use for T.
        /// </summary>
        private string GetInstanceName<T>()
        {
            return typeof(T).FullName;
        }
        #endregion


    }

}