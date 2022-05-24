using FishNet.Managing;
using FishNet.Transporting;
using System;
using UnityEngine;

namespace FishNet.Component.ColliderRollback
{
    public class RollbackManager : MonoBehaviour
    {
        //PROSTART
        #region Types.
        public enum PhysicsType
        {
            TwoDimensional = 1,
            ThreeDimensional = 2,
            Both = 4
        }
        #endregion

        #region Public.
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
        /// <summary>
        /// Returns the current PreciseTick.
        /// </summary>
        public PreciseTick PreciseTick
        {
            get
            {
                if (_networkManager == null)
                    return new PreciseTick(0, 0);

                return new PreciseTick(
                    _networkManager.TimeManager.LastPacketTick,
                    _networkManager.TimeManager.TickPercent
                    );
            }
        }

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
        #region Private.
        /// <summary>
        /// Physics used when rolling back.
        /// </summary>
        private PhysicsType _rollbackPhysics;
        /// <summary>
        /// NetworkManager on the same object as this script.
        /// </summary>
        private NetworkManager _networkManager;
        #endregion

        #region Const.
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
        internal void InitializeOnceInternal(NetworkManager manager)
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
            if (obj.ConnectionState == LocalConnectionStates.Started)
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
        /// <param name="fixedFrame"></param>
        /// <param name="physicsType"></param>
        public void Rollback(PreciseTick pt, PhysicsType physicsType, bool asHost = false)
        {
            if (_networkManager == null)
                return;

            //How much time to rollback.
            float time;
            float tickDelta = (float)_networkManager.TimeManager.TickDelta;
            //Rolling back not as host.
            if (!asHost)
            {
                pt.Tick -= Interpolation;
                uint pastTicks = (_networkManager.TimeManager.Tick - pt.Tick);
                //No ticks to rollback to.
                if (pastTicks <= 0)
                    return;
                //They should never get this high, ever. This is to prevent overflows.
                if (pastTicks > ushort.MaxValue)
                    pastTicks = ushort.MaxValue;

                //Weight percent by -40%
                float percent = (float)(pt.Percent / 100f) * -0.4f;
                time = (float)(pastTicks * tickDelta);
                time += (percent * tickDelta);
            }
            //Rolling back as host.
            else
            {
                //Roll back 1 tick + percent.
                float percent = (_networkManager.TimeManager.TickPercent / 100f);
                time = tickDelta + (tickDelta * percent);
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