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
        internal void InitializeOnceInternal()
        {
            InitializeOnceSyncTypes();
            InitializeOnceRpcLinks();
        }


        /// <summary>
        /// Serializes information about components.
        /// </summary>
        internal void SerializeComponents(NetworkObject nob, byte componentIndex)
        {
            _networkObjectCache = nob;
            ComponentIndex = componentIndex;
        }

        #region Editor.
        protected virtual void Reset()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            //NetworkObject nob = TryAddNetworkObject();
            TryAddNetworkObject();
            //nob.UpdateNetworkBehaviours();
#endif
        }

        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            TryAddNetworkObject();
            //NetworkObject nob = TryAddNetworkObject();
            ////If componentIndex has not been set.
            //if (ComponentIndex == byte.MaxValue)
            //    nob.UpdateNetworkBehaviours();
#endif
        }

        /// <summary>
        /// Tries to add the NetworkObject component.
        /// </summary>
        private NetworkObject TryAddNetworkObject()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || _addedNetworkObject != null)
                return _addedNetworkObject;

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

            _addedNetworkObject = (result != null) ? result : transform.root.gameObject.AddComponent<NetworkObject>();
            return _addedNetworkObject;
#else
            return null;
#endif
        }
        #endregion
    }


}