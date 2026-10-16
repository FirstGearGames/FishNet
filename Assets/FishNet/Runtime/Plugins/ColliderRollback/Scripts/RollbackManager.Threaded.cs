#if FISHNET_THREADED_COLLIDER_ROLLBACK
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using System;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.SceneManagement;
#if UNITY_6000_5_OR_NEWER
using SceneHandle = System.UInt64;
#else
using SceneHandle = System.Int32;
#endif

namespace FishNet.Component.ColliderRollback
{
    public partial class RollbackManager : MonoBehaviour
    {
        #region Serialized.
        /// <summary>
        /// </summary>
        [Tooltip("Maximum time in the past colliders can be rolled back to.")]
        [SerializeField]
        private float _maximumRollbackTime = 1.25f;
        /// <summary>
        /// </summary>
        [Tooltip("When to invoke OnRollbackDeferred.")]
        [SerializeField]
        private DeferredRollbackOrder _deferredRollbackOrder = DeferredRollbackOrder.PreTick;
        /// <summary>
        /// Maximum time in the past colliders can be rolled back to.
        /// </summary>
        internal float MaximumRollbackTime => _maximumRollbackTime;
        /// <summary>
        /// </summary>
        [Tooltip("Interpolation value for the NetworkTransforms or objects being rolled back.")]
        [Range(0, 250)]
        [SerializeField]
        internal ushort Interpolation = 2;
        #endregion

        #region Private
        
        #region Private Profiler Markers
        
        private static readonly ProfilerMarker _pm_OnPreTick = new("RollbackManager.TimeManager_OnPreTick()");
        private static readonly ProfilerMarker _pm_OnTick = new("RollbackManager.TimeManager_OnTick()");
        private static readonly ProfilerMarker _pm_OnPostTick = new("RollbackManager.TimeManager_OnPostTick()");
        private static readonly ProfilerMarker _pm_CreateSnapshots = new("RollbackManager.CreateSnapshots()");
        private static readonly ProfilerMarker _pm_Rollback0 = new("RollbackManager.Rollback(int, PreciseTick, RollbackPhysicsType, bool)");
        private static readonly ProfilerMarker _pm_Rollback1 = new("RollbackManager.Rollback(int, Vector3, Vector3, float, PreciseTick, RollbackPhysicsType, bool)");
        private static readonly ProfilerMarker _pm_RequestRollbackDeferred = new("RollbackManager.RequestRollbackDeferred(int, Vector3, Vector3, float, PreciseTick, RollbackPhysicsType, bool)");
        private static readonly ProfilerMarker _pm_RollbackDeferred = new("RollbackManager.RollbackDeferred()");
        private static readonly ProfilerMarker _pm_Return = new("RollbackManager.Return()");
        
        private static readonly ProfilerMarker _pm_RegisterColliderRollback = new("RollbackManager.RegisterColliderRollback(ColliderRollback)");
        private static readonly ProfilerMarker _pm_UnregisterColliderRollback = new("RollbackManager.UnregisterColliderRollback(ColliderRollback)");
        
        #endregion
        
        #region Public.
        /// <summary>
        /// Called when deferred rollback occured for all requests.
        /// </summary>
        public event Action OnRollbackDeferred;
        /// <summary>
        /// Called when deferred rollback in past occured for all requests.
        /// </summary>
        public event Action OnPostRollbackDeferred;
        #endregion
        
        #endregion
        
        // PROSTART

        #region Private Pro.
        /// <summary>
        /// NetworkManager on the same object as this script.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// All active ColliderRollback scripts.
        /// </summary>
        private readonly RollbackCollection _rollbackCollection = new();
        #endregion

        // PROEND
        
        // PROSTART
        private void TimeManager_OnPreTick()
        {
            using (_pm_OnPreTick.Auto())
            {
                if (_deferredRollbackOrder == DeferredRollbackOrder.PreTick)
                {
                    // Make snapshots for RollbackDeferred.
                    CreateSnapshots();
                    RollbackDeferred();
                }
            }
        }
        
        private void TimeManager_OnTick()
        {
            using (_pm_OnTick.Auto())
            {
                if (_deferredRollbackOrder == DeferredRollbackOrder.Tick)
                {
                    // Make snapshots for RollbackDeferred.
                    CreateSnapshots();
                    RollbackDeferred();
                }
            }
        }

        private void TimeManager_OnPostTick()
        {
            using (_pm_OnPostTick.Auto())
            {
                // Make snapshots in every PostTick.
                CreateSnapshots();
                if (_deferredRollbackOrder == DeferredRollbackOrder.PostTick)
                {
                    RollbackDeferred();
                }
            }
        }
        // PROEND

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name = "manager"></param>
        internal void InitializeOnce_Internal(NetworkManager manager)
        {
            // PROSTART
            _networkManager = manager;
            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            // PROEND
        }

        // PROSTART
        /// <summary>
        /// Called when server connection state changes.
        /// </summary>
        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            //Listen just before ticks.
            if (obj.ConnectionState == LocalConnectionState.Started)
            {
                TimeManager tm = _networkManager.TimeManager;
                _rollbackCollection.Initialize(_networkManager, tm.TickDelta, MaximumRollbackTime);
                //If the server invoking this event is the only one started subscribe.
                if (_networkManager.ServerManager.IsOnlyOneServerStarted())
                {
                    _networkManager.TimeManager.OnPreTick += TimeManager_OnPreTick;
                    _networkManager.TimeManager.OnTick += TimeManager_OnTick;
                    _networkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
                }
            }
            else
            {
                _rollbackCollection.Deinitialize();
                //If no servers are started then unsubscribe.
                if (!_networkManager.ServerManager.IsAnyServerStarted())
                {
                    _networkManager.TimeManager.OnPreTick -= TimeManager_OnPreTick;
                    _networkManager.TimeManager.OnTick -= TimeManager_OnTick;
                    _networkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
                }
            }
        }

        private void OnDestroy()
        {
            _rollbackCollection.Deinitialize();
        }

        private void OnApplicationQuit()
        {
            _rollbackCollection.Deinitialize();
        }

        /// <summary>
        /// Registers a ColliderRollback.
        /// </summary>
        /// <param name = "cr"></param>
        internal void RegisterColliderRollback(ColliderRollback cr)
        {
            using (_pm_RegisterColliderRollback.Auto())
            {
                _rollbackCollection.RegisterColliderRollback(cr);
            }
        }

        /// <summary>
        /// Unregisters a ColliderRollback.
        /// </summary>
        /// <param name = "cr"></param>
        internal void UnregisterColliderRollback(ColliderRollback cr)
        {
            using (_pm_UnregisterColliderRollback.Auto())
            {
                _rollbackCollection.UnregisterColliderRollback(cr);
            }
        }

        /// <summary>
        /// Creates snapshots for colliders.
        /// </summary>
        private void CreateSnapshots()
        {
            using (_pm_CreateSnapshots.Auto())
            {
                _rollbackCollection.CreateSnapshots();
            }
        }
        //PROEND

        [Obsolete("Use Rollback(Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics, bool) instead.")] //Remove on V5
        public void Rollback(Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
        {
            
        }

        [Obsolete("Use Rollback(Scene, Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics, bool) instead.")] //Remove on V5
        public void Rollback(Scene scene, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
        {
            
        }

        [Obsolete("Use Rollback(int, Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics, bool) instead.")] //Remove on V5
        public void Rollback(SceneHandle sceneHandle, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
        {
            
        }

        [Obsolete("Use Rollback(Scene, Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics2D, bool) instead.")] //Remove on V5
        public void Rollback(Scene scene, Vector2 origin, Vector2 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
        {
            
        }

        [Obsolete("Use Rollback(Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics2D, bool) instead.")] //Remove on V5
        public void Rollback(Vector2 origin, Vector2 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
        {
            
        }

        /// <summary>
        /// Rolls back all colliders.
        /// </summary>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            
        }

        /// <summary>
        /// Rolls back all colliders in a scene.
        /// </summary>
        /// <param name = "scene">Scene containing colliders.</param>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(Scene scene, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            
        }

        /// <summary>
        /// Rolls back all colliders in a scene.
        /// </summary>
        /// <param name = "sceneHandle">Scene handle containing colliders.</param>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(SceneHandle sceneHandle, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            using (_pm_Rollback0.Auto())
            {
                
            }
        }

        /// <summary>
        /// Rolls back colliders hit by a test cast against bounding boxes.
        /// </summary>
        /// <param name = "origin">Ray origin.</param>
        /// <param name = "normalizedDirection">Direction to cast.</param>
        /// <param name = "distance">Distance of cast.</param>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            
        }

        /// <summary>
        /// Rolls back colliders hit by a test cast against bounding boxes, in a specific scene.
        /// </summary>
        /// <param name = "scene">Scene containing colliders.</param>
        /// <param name = "origin">Ray origin.</param>
        /// <param name = "normalizedDirection">Direction to cast.</param>
        /// <param name = "distance">Distance of cast.</param>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(Scene scene, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            
        }

        /// <summary>
        /// Rolls back colliders hit by a test cast against bounding boxes, in a specific scene.
        /// </summary>
        /// <param name = "sceneHandle">Scene handle containing colliders.</param>
        /// <param name = "origin">Ray origin.</param>
        /// <param name = "normalizedDirection">Direction to cast.</param>
        /// <param name = "distance">Distance of cast.</param>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(SceneHandle sceneHandle, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            using (_pm_Rollback1.Auto())
            {
                
            }
        }
        
        /// <summary>
        /// Requests deferred rollback for colliders hit by a test cast against bounding boxes.
        /// </summary>
        /// <param name = "origin">Ray origin.</param>
        /// <param name = "normalizedDirection">Direction to cast.</param>
        /// <param name = "distance">Distance of cast.</param>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void RequestRollbackDeferred(Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            
        }
        
        /// <summary>
        /// Requests deferred rollback for colliders hit by a test cast against bounding boxes, in a specific scene.
        /// </summary>
        /// <param name = "scene">Scene containing colliders.</param>
        /// <param name = "origin">Ray origin.</param>
        /// <param name = "normalizedDirection">Direction to cast.</param>
        /// <param name = "distance">Distance of cast.</param>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void RequestRollbackDeferred(Scene scene, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            
        }
        
        /// <summary>
        /// Requests deferred rollback for colliders hit by a test cast against bounding boxes, in a specific scene.
        /// </summary>
        /// <param name = "sceneHandle">Scene handle containing colliders.</param>
        /// <param name = "origin">Ray origin.</param>
        /// <param name = "normalizedDirection">Direction to cast.</param>
        /// <param name = "distance">Distance of cast.</param>
        /// <param name = "pt">Precise tick received from the client.</param>
        /// <param name = "physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name = "asOwnerAndClientHost">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void RequestRollbackDeferred(SceneHandle sceneHandle, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
        {
            using (_pm_RequestRollbackDeferred.Auto())
            {
                
            }
        }
        
        /// <summary>
        /// Rolls back for all RollbackRequests.
        /// </summary>
        public void RollbackDeferred()
        {
            
        }

        

        /// <summary>
        /// Returns all ColliderRollback objects back to their original position.
        /// </summary>
        public void Return()
        {
            using (_pm_Return.Auto())
            {
                
            }
        }

        
    }
}
#endif