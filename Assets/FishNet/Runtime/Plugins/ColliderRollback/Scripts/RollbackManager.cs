using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{
    public class RollbackManager : MonoBehaviour
    {
        //PROSTART
        #region Types Pro.
        public enum PhysicsType
        {
            TwoDimensional = 1,
            ThreeDimensional = 2,
            Both = 4
        }
        #endregion
        //PROEND

        //PROSTART
        #region Public Pro.
        /// <summary>
        /// Called when a snapshot should be created.
        /// </summary>
        public event Action OnCreateSnapshot;
        /// <summary>
        /// Called when colliders should return.
        /// </summary>
        public event Action OnReturn;
        /// <summary>
        /// Called when a rollback should occur.
        /// </summary>
        public event Action<float> OnRollback;

        #endregion
        //PROEND

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

        //PROSTART
        #region Private Pro.
        /// <summary>
        /// Physics used when rolling back.
        /// </summary>
        private PhysicsType _rollbackPhysics;
        /// <summary>
        /// NetworkManager on the same object as this script.
        /// </summary>
        private NetworkManager _networkManager;
        #endregion
        //PROEND

        //PROSTART
        #region Const Pro.
        /// <summary>
        /// Maximum amount of time colliders can roll back.
        /// </summary>
        public const float MAX_ROLLBACK_TIME = 1f;
        #endregion
        //PROEND

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void InitializeOnce_Internal(NetworkManager manager)
        {
            //PROSTART
            _networkManager = manager;
            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            //PROEND
        }

        //PROSTART
        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            //Listen just before ticks.
            if (obj.ConnectionState == LocalConnectionState.Started)
            {
                //If the server invoking this event is the only one started subscribe.
                if (_networkManager.ServerManager.OneServerStarted())
                    _networkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
            }
            else
            {
                //If no servers are started then unsubscribe.
                if (!_networkManager.ServerManager.AnyServerStarted())
                    _networkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        private void TimeManager_OnPostTick()
        {
            OnCreateSnapshot?.Invoke();
        }

        /// <summary>
        /// Returns all ColliderRollback objects back to their original position.
        /// </summary>
        public void Return()
        {
            OnReturn?.Invoke();
            SyncTransforms(_rollbackPhysics);
        }

        /// <summary>
        /// Rolls back colliders based on a fixed frame.
        /// </summary>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name="asOwner">True if IsOwner of the object. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(PreciseTick pt, PhysicsType physicsType, bool asOwner = false)
        {
            if (_networkManager == null)
                return;

            TimeManager timeManager = _networkManager.TimeManager;
            //How much time to rollback.
            float time = 0f;
            float tickDelta = (float)timeManager.TickDelta;
            //Rolling back not as host.
            if (!asOwner)
            {
                ulong pastTicks = (timeManager.Tick - pt.Tick) + Interpolation;
                if (pastTicks >= 0)
                {
                    //They should never get this high, ever. This is to prevent overflows.
                    if (pastTicks > ushort.MaxValue)
                        pastTicks = ushort.MaxValue;

                    //Add past ticks time.
                    time = (pastTicks * tickDelta);

                    //More protection.
                    if (pt.Percent > 100)
                        pt.Percent = 100;
                    else if (pt.Percent < 0)
                        pt.Percent = 0;

                    int percentWhole;
                    float percent;
                    //Add client percent time.
                    percentWhole = (sbyte)(100 - pt.Percent);
                    percent = Mathf.Max(0f, percentWhole / 100f);
                    time += (percent * tickDelta);
                    time -= tickDelta;
                }
            }
            //Rolling back as owner (client host firing).
            else
            {
                ulong pastTicks = (timeManager.Tick - pt.Tick);
                if (pastTicks >= 0)
                {
                    time = (pastTicks * tickDelta * 0.5f);
                    float percent = (float)(1f - (timeManager.GetTickPercent() / 100f));
                    time -= (percent * tickDelta);
                }
            }

            OnRollback?.Invoke(time);
            _rollbackPhysics = physicsType;
            SyncTransforms(physicsType);
        }



        /// <summary>
        /// Applies transforms for the specified physics type.
        /// </summary>
        /// <param name="physicsType"></param>
        private void SyncTransforms(PhysicsType physicsType)
        {
            if (physicsType == PhysicsType.ThreeDimensional)
            {
                Physics.SyncTransforms();
            }
            else if (physicsType == PhysicsType.TwoDimensional)
            {
                Physics2D.SyncTransforms();
            }
            else
            {
                Physics.SyncTransforms();
                Physics2D.SyncTransforms();
            }
        }
        //PROEND
    }

}