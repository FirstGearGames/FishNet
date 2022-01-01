using FishNet.Utility;
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
        #region Debug //debug
        //[HideInInspector]
        //public string GivenName;
        //public void SetGivenName(string s) => GivenName = s;
        #endregion

        /// <summary>
        /// True if this NetworkBehaviour is initialized for the network.
        /// </summary>
        public bool IsSpawned => (NetworkObject != null && NetworkObject.IsSpawned);
        /// <summary>
        /// ComponentIndex for this NetworkBehaviour.
        /// </summary>
        public byte ComponentIndex { get; private set; }
#if UNITY_EDITOR
        /// <summary>
        /// NetworkObject automatically added or discovered during edit time.
        /// </summary>
        [SerializeField, HideInInspector]
        private NetworkObject _addedNetworkObject;
#endif
        /// <summary>
        /// NetworkObject this behaviour is for.
        /// </summary>        
        public NetworkObject NetworkObject { get; private set; }

        /// <summary>
        /// Prepares this script for initialization.
        /// </summary>
        /// <param name="networkObject"></param>
        /// <param name="componentIndex"></param>
        internal void PreInitialize(NetworkObject networkObject, byte componentIndex)
        {
            NetworkObject = networkObject;
            ComponentIndex = componentIndex;
            PreInitializeSyncTypes(networkObject);
            PreInitializeRpcLinks();
        }


        #region Editor.
        protected virtual void Reset()
        {
#if UNITY_EDITOR
            if (!ApplicationState.IsPlaying())
                TryAddNetworkObject();
#endif
        }

        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            if (!ApplicationState.IsPlaying())
                TryAddNetworkObject();
#endif
        }
        /// <summary>
        /// Tries to add the NetworkObject component.
        /// </summary>
        private void TryAddNetworkObject()
        {
#if UNITY_EDITOR
            if (_addedNetworkObject != null)
                return;
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
#endif
        }
        #endregion
    }


}