using FishNet.Managing.Timing;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    public partial class OfflineRigidbody : MonoBehaviour
    {

        #region Serialized.
        /// <summary>
        /// Type of prediction movement which is being used.
        /// </summary>
        [Tooltip("Type of prediction movement which is being used.")]
        [SerializeField]
        private RigidbodyType _rigidbodyType;
        /// <summary>
        /// True to also get rigidbody components within children.
        /// </summary>
        [Tooltip("True to also get rigidbody components within children.")]
        [SerializeField]
        private bool _getInChildren;
        #endregion

        #region Private.
        /// <summary>
        /// Pauser for rigidbodies.
        /// </summary>
        private RigidbodyPauser _rigidbodyPauser = new RigidbodyPauser();
        /// <summary>
        /// TimeManager subscribed to.
        /// </summary>
        private TimeManager _timeManager;
        #endregion


        private void Awake()
        {
            InitializeOnce();
        }


        private void OnDestroy()
        {
            ChangeSubscription(false);
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        private void InitializeOnce()
        {
            _timeManager = InstanceFinder.TimeManager;
            UpdateRigidbodies();
            ChangeSubscription(true);
        }

        /// <summary>
        /// Sets a new TimeManager to use.
        /// </summary>
        /// <param name="tm"></param>
        public void SetTimeManager(TimeManager tm)
        {
            if (tm == _timeManager)
                return;

            //Unsub from current.
            ChangeSubscription(false);
            //Sub to newest.
            _timeManager = tm;
            ChangeSubscription(true);
        }

        /// <summary>
        /// Finds and assigns rigidbodie using configured settings.
        /// </summary>
        public void UpdateRigidbodies()
        {
            _rigidbodyPauser.UpdateRigidbodies(transform, _rigidbodyType, _getInChildren);
        }

        /// <summary>
        /// Changes the subscription to the TimeManager.
        /// </summary>
        private void ChangeSubscription(bool subscribe)
        {
            if (_timeManager == null)
                return;

            if (subscribe)
            {
                _timeManager.OnPreReconcile += _timeManager_OnPreReconcile;
                _timeManager.OnPostTick += _timeManager_OnPostTick;
            }
            else
            {
                _timeManager.OnPreReconcile -= _timeManager_OnPreReconcile;
                _timeManager.OnPostTick -= _timeManager_OnPostTick;
            }
        }

        private void _timeManager_OnPreReconcile(Object.NetworkBehaviour obj)
        {
            //Make rbs all kinematic/!simulated before reconciling, which would also result in replays.
            _rigidbodyPauser.Pause();
        }

        private void _timeManager_OnPostTick()
        {
            _rigidbodyPauser.Unpause();
        }

    }


}