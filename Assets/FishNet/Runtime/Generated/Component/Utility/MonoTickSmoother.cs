using FishNet.Managing.Logging;
using FishNet.Managing.Predicting;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    /// <summary>
    /// Smoothes an object between ticks.
    /// </summary>
    public class MonoTickSmoother : MonoBehaviour
    {
        #region Serialized.
        /// <summary>
        /// True to use InstanceFinder to locate the TimeManager. When false specify which TimeManager to use by calling SetTimeManager.
        /// </summary>
        [Tooltip("True to use InstanceFinder to locate the TimeManager. When false specify which TimeManager to use by calling SetTimeManager.")]
        [SerializeField]
        private bool _useInstanceFinder = true;
        #endregion

        #region Private.
        /// <summary>
        /// TimeManager subscribed to.
        /// </summary>
        private TimeManager _timeManager;
        /// <summary>
        /// BasicTickSmoother for this script.
        /// </summary>
        private TransformTickSmoother _tickSmoother;
        #endregion

        private void Awake()
        {
            InitializeOnce();
        }

        private void OnDestroy()
        {
            ChangeSubscription(false);
            ObjectCaches<TransformTickSmoother>.StoreAndDefault(ref _tickSmoother);
        }

        [Client(Logging = LoggingType.Off)]
        private void Update()
        {
            _tickSmoother?.Update();
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        private void InitializeOnce()
        {
            _tickSmoother = ObjectCaches<TransformTickSmoother>.Retrieve();
            if (_useInstanceFinder)
            {
                _timeManager = InstanceFinder.TimeManager;
                ChangeSubscription(true);
            }
        }

        /// <summary>
        /// Sets a new PredictionManager to use.
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
        /// Changes the subscription to the TimeManager.
        /// </summary>
        private void ChangeSubscription(bool subscribe)
        {
            if (_timeManager == null)
                return;

            if (subscribe)
            {
                _timeManager.OnPreTick += _timeManager_OnPreTick;
                _timeManager.OnPostTick += _timeManager_OnPostTick;
            }
            else
            {
                _timeManager.OnPreTick -= _timeManager_OnPreTick;
                _timeManager.OnPostTick -= _timeManager_OnPostTick;
            }
        }


        /// <summary>
        /// Called before a tick starts.
        /// </summary>
        private void _timeManager_OnPreTick()
        {
            _tickSmoother.OnPreTick();
        }

        /// <summary>
        /// Called after a tick completes.
        /// </summary>
        private void _timeManager_OnPostTick()
        {
            _tickSmoother.OnPostTick();
        }


    }


}

