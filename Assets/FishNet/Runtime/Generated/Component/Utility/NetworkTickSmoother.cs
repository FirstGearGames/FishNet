using FishNet.Managing.Logging;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Transforming
{
    /// <summary>
    /// Smoothes an object between ticks.
    /// </summary>
    public class NetworkTickSmoother : NetworkBehaviour
    {
        #region Serialized.
        /// <summary>
        /// GraphicalObject you wish to smooth.
        /// </summary>
        [Tooltip("GraphicalObject you wish to smooth.")]
        [SerializeField]
        private Transform _graphicalObject;
        /// <summary>
        /// True to enable teleport threshhold.
        /// </summary>
        [Tooltip("True to enable teleport threshold.")]
        [SerializeField]
        private bool _enableTeleport;
        /// <summary>
        /// How far the object must move between ticks to teleport rather than smooth.
        /// </summary>
        [Tooltip("How far the object must move between ticks to teleport rather than smooth.")]
        [Range(0f, ushort.MaxValue)]
        [SerializeField]
        private float _teleportThreshold;
        #endregion

        #region Private.
        /// <summary>
        /// TimeManager subscribed to.
        /// </summary>
        private TimeManager _timeManager;
        /// <summary>
        /// BasicTickSmoother for this script.
        /// </summary>
        private LocalTransformTickSmoother _tickSmoother;
        /// <summary>
        /// True once destroyed. This is to resolve a race condition when the object is destroyed in awake but initialized after.
        /// </summary>
        private bool _destroyed;
        #endregion

        private void OnDestroy()
        {
            ChangeSubscription(false);
            ObjectCaches<LocalTransformTickSmoother>.StoreAndDefault(ref _tickSmoother);
            _destroyed = true;
        }

        public override void OnStartClient()
        {
            Initialize();
        }

        [Client(Logging = LoggingType.Off)]
        private void Update()
        {
            _tickSmoother?.Update();
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        private void Initialize()
        {
            if (_tickSmoother == null)
                _tickSmoother = ObjectCaches<LocalTransformTickSmoother>.Retrieve();

            SetTimeManager(base.TimeManager);
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
            if (_destroyed)
                return;
            if (_timeManager == null)
                return;

            if (subscribe)
            {
                if (_tickSmoother != null)
                {
                    float tDistance = (_enableTeleport) ? _teleportThreshold : MoveRatesCls.UNSET_VALUE;
                    _tickSmoother.InitializeOnce(_graphicalObject, tDistance, (float)_timeManager.TickDelta, 1);
                }
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

