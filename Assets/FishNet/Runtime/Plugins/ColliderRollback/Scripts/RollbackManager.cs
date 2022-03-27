using FishNet.Managing;
using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{
    public class RollbackManager : MonoBehaviour
    {
        

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum time in the past colliders can be rolled back to.")]
        [SerializeField]
        private float _maximumRollbackTime = 1.25f;
        /// <summary>
        /// Maximum time in the past colliders can be rolled back to.
        /// </summary>
        internal float MaximumRollbackTime => _maximumRollbackTime;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Interpolation value for the NetworkTransform or object being rolled back.")]
        [Range(0, 250)]
        [SerializeField]
        internal ushort Interpolation = 2;
        #endregion

        

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void InitializeOnceInternal(NetworkManager manager)
        {
            
        }

        
    }

}