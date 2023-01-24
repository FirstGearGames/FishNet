using FishNet.Documenting;
using FishNet.Serializing.Helping;
using FishNet.Utility.Constant;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo(UtilityConstants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object
{
    /// <summary>
    /// Scripts which inherit from NetworkBehaviour can be used to gain insight of, and perform actions on the network.
    /// </summary>
    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        /// <summary>
        /// True if this NetworkBehaviour is initialized for the network.
        /// </summary>
        public bool IsSpawned => _networkObjectCache.IsSpawned;
        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        private byte _componentIndexCache = byte.MaxValue;
        /// <summary>
        /// ComponentIndex for this NetworkBehaviour.
        /// </summary>
        public byte ComponentIndex
        {
            get => _componentIndexCache;
            private set => _componentIndexCache = value;
        }
#if UNITY_EDITOR
        /// <summary>
        /// NetworkObject automatically added or discovered during edit time.
        /// </summary>
        [SerializeField, HideInInspector]
        private NetworkObject _addedNetworkObject;
#endif 
        /// <summary>
        /// 
        /// </summary>
        [SerializeField, HideInInspector]
        private NetworkObject _networkObjectCache;
        /// <summary>
        /// NetworkObject this behaviour is for.
        /// </summary>
        public NetworkObject NetworkObject => _networkObjectCache;

        /// <summary>
        /// Initializes this script. This will only run once even as host.
        /// </summary>
        /// <param name="networkObject"></param>
        /// <param name="componentIndex"></param>
        internal void InitializeOnce_Internal()
        {
            InitializeOnceSyncTypes();
            InitializeOnceRpcLinks();
        }


        /// <summary>
        /// Serializes information for network components.
        /// </summary>
        internal void SerializeComponents(NetworkObject nob, byte componentIndex)
        {
            _networkObjectCache = nob;
            ComponentIndex = componentIndex;
        }

        /// <summary>
        /// Manually initializes network content for the NetworkBehaviour if the object it's on is disabled.
        /// </summary>
        internal void InitializeIfDisabled()
        {
            if (gameObject.activeInHierarchy)
                return;

            NetworkInitializeIfDisabled();
        }
        /// <summary>
        /// Long name is to prevent users from potentially creating their own method named the same.
        /// </summary>
        [CodegenMakePublic]
        [APIExclude]
        internal virtual void NetworkInitializeIfDisabled() { }

        #region Editor.
        protected virtual void Reset()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            TryAddNetworkObject();
#endif
        }

        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return; 

            TryAddNetworkObject();
#endif
        }

        /// <summary>
        /// Resets this NetworkBehaviour so that it may be added to an object pool.
        /// </summary>
        internal void ResetForObjectPool()
        {
            ResetSyncTypes();
            ClearReplicateCache();
            ClearBuffedRpcs();
        }


        /// <summary>
        /// Tries to add the NetworkObject component.
        /// </summary>
        private NetworkObject TryAddNetworkObject()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return _addedNetworkObject;

            if (_addedNetworkObject != null)
            {
                AlertToDuplicateNetworkObjects(_addedNetworkObject.transform);
                return _addedNetworkObject;
            }

            /* Manually iterate up the chain because GetComponentInParent doesn't
             * work when modifying prefabs in the inspector. Unity, you're starting
             * to suck a lot right now. */
            NetworkObject result = null;
            Transform climb = transform;

            while (climb != null)
            {
                if (climb.TryGetComponent<NetworkObject>(out result))
                    break;
                else
                    climb = climb.parent;
            }

            if (result != null)
            {
                _addedNetworkObject = result;
            }
            //Not found, add a new nob.
            else
            {
                _addedNetworkObject = transform.root.gameObject.AddComponent<NetworkObject>();
                Debug.Log($"Script {GetType().Name} on object {gameObject.name} added a NetworkObject component to {transform.root.name}.");
            }

            AlertToDuplicateNetworkObjects(_addedNetworkObject.transform);
            return _addedNetworkObject;

            //Removes duplicate network objects from t.
            void AlertToDuplicateNetworkObjects(Transform t)
            {
                NetworkObject[] nobs = t.GetComponents<NetworkObject>();
                //This shouldn't be possible but does occur sometimes; maybe a unity bug?
                if (nobs.Length > 1)
                { 
                    //Update added to first entryt.
                    _addedNetworkObject = nobs[0];
 
                    string useMenu = " You may also use the Fish-Networking menu to automatically remove duplicate NetworkObjects.";
                    string sceneName = t.gameObject.scene.name;
                    if (string.IsNullOrEmpty(sceneName))
                        Debug.LogError($"Prefab {t.name} has multiple NetworkObject components. Please remove the extra component(s) to prevent errors.{useMenu}");
                    else
                        Debug.LogError($"Object {t.name} in scene {sceneName} has multiple NetworkObject components. Please remove the extra component(s) to prevent errors.{useMenu}");
                }

            } 
#else
            return null;
#endif
        }

        #endregion
    }


}