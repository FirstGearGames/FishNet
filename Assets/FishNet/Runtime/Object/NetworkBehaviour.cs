using FishNet.Constants;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo(Constants.CODEGEN_ASSEMBLY_NAME)]
namespace FishNet.Object
{

    public abstract partial class NetworkBehaviour : MonoBehaviour
    {
        #region Debug //debug
        //[HideInInspector]
        //public string GivenName;
        //public void SetGivenName(string s) => GivenName = s;
        #endregion

        /// <summary>
        /// Returns if this NetworkBehaviour is spawned.
        /// </summary>
        public bool IsSpawned => (NetworkObject != null && NetworkObject.IsSpawned);

        /// <summary>
        /// ComponentIndex for this NetworkBehaviour.
        /// </summary>
        public byte ComponentIndex { get; private set; } = 0;
        /// <summary>
        /// NetworkObject this behaviour is for.
        /// </summary>        
        public NetworkObject NetworkObject { get; private set; } = null;


        /// <summary>
        /// Prepares this script for initialization.
        /// </summary>
        /// <param name="networkObject"></param>
        /// <param name="componentIndex"></param>
        public void PreInitialize(NetworkObject networkObject, byte componentIndex)
        {
            NetworkObject = networkObject;
            ComponentIndex = componentIndex;
            PreInitializeSyncTypes(networkObject);
            PreInitializeCallbacks(networkObject);
            PreInitializeRpcs(networkObject);
        }


        #region Editor.
#if UNITY_EDITOR
        protected virtual void Reset()
        {
            TryAddNetworkObject();
        }

        protected virtual void OnValidate()
        {
            TryAddNetworkObject();
        }
        /// <summary>
        /// Tries to add the NetworkObject component.
        /// </summary>
        private void TryAddNetworkObject()
        {
            if (NetworkObject != null)
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

            NetworkObject = (result != null) ? result : transform.root.gameObject.AddComponent<NetworkObject>();
        }
#endif
        #endregion
    }


}