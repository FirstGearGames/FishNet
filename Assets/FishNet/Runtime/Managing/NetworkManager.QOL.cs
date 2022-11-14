using FishNet.Managing.Object;
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Managing
{
    /// <summary>
    /// Delegate for InvokeOnInstance.
    /// </summary>
    /// <param name="component">Component which must be registered to invoke.</param>
    public delegate void ComponentRegisteredDelegate(UnityEngine.Component component);

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
        private Dictionary<string, List<ComponentRegisteredDelegate>> _pendingInvokes = new Dictionary<string, List<ComponentRegisteredDelegate>>();
        /// <summary>
        /// Currently registered components.
        /// </summary>
        private Dictionary<string, UnityEngine.Component> _registeredComponents = new Dictionary<string, UnityEngine.Component>();
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
        /// Invokes a delegate when a specified component becomes registered. Delegate will invoke immediately if already registered.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <param name="del">Delegate to invoke.</param>
        public void InvokeOnInstance<T>(ComponentRegisteredDelegate del) where T : UnityEngine.Component
        {
            T result = GetInstance<T>();
            //If not found yet make a pending invoke.
            if (result == default(T))
            {
                string tName = GetInstanceName<T>();
                List<ComponentRegisteredDelegate> dels;
                if (!_pendingInvokes.TryGetValue(tName, out dels))
                {
                    dels = new List<ComponentRegisteredDelegate>();
                    _pendingInvokes[tName] = dels;
                }

                dels.Add(del);
            }
            //Already exist, invoke right away.
            else
            {
                del.Invoke(result);
            }
        }
        /// <summary>
        /// Returns class of type if found within CodegenBase classes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetInstance<T>() where T : UnityEngine.Component
        {
            string tName = GetInstanceName<T>();
            if (_registeredComponents.TryGetValue(tName, out UnityEngine.Component result))
                return (T)result;
            else
                LogWarning($"Component {tName} is not registered.");

            return default(T);
        }
        /// <summary>
        /// Registers a new component to this NetworkManager.
        /// </summary>
        /// <typeparam name="T">Type to register.</typeparam>
        /// <param name="component">Reference of the component being registered.</param>
        /// <param name="replace">True to replace existing references.</param>
        public void RegisterInstance<T>(T component, bool replace = true) where T : UnityEngine.Component
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
                if (_pendingInvokes.TryGetValue(tName, out List<ComponentRegisteredDelegate> dels))
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
        public void UnregisterInstance<T>() where T : UnityEngine.Component
        {
            string tName = GetInstanceName<T>();
            _registeredComponents.Remove(tName);
        }
        /// <summary>
        /// Removes delegates from pending invokes when may have gone missing.
        /// </summary>
        private void RemoveNullPendingDelegates()
        {
            foreach (List<ComponentRegisteredDelegate> dels in _pendingInvokes.Values)
            {
                for (int i = 0; i < dels.Count; i++)
                {
                    if (dels[i] == null)
                    {
                        dels.RemoveAt(i);
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