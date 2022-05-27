using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{

    public class ColliderRollback : NetworkBehaviour
    {
        

        #region Serialized.
        /// <summary>
        /// Objects holding colliders which can rollback.
        /// </summary>
        [Tooltip("Objects holding colliders which can rollback.")]
        [SerializeField]
        private GameObject[] _colliderParents = new GameObject[0];
        #endregion

        
    }

}
