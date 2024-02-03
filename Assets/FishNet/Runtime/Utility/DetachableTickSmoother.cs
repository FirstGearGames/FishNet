using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Utility.Extension;
using UnityEngine;

namespace FishNet.Utility
{
    /// <summary>
    /// Smooths movemoments between ticks towards a followed target.
    /// </summary>
    public class DetachableTickSmoother : NetworkBehaviour
    {
        /// <summary>
        /// Target to follow; this is usually a graphical object but can be anything you want to use as your camera target.
        /// </summary>
        [Tooltip("Target to follow; this is usually a graphical object but can be anything you want to use as your camera target.")]
        [SerializeField]
        private Transform _target;
        /// <summary>
        /// True to attach this object to it's previous parent when OnStopClient is called.
        /// </summary>
        [Tooltip("True to attach this object to it's previous parent when OnStopClient is called.")]
        [SerializeField]
        private bool _attachOnStop = true;
        /// <summary>
        /// True to enable teleport threshhold.
        /// </summary>
        [Tooltip("True to enable teleport threshhold.")]
        [SerializeField]
        private bool _enableTeleport;
        /// <summary>
        /// Distance the graphical object must move between ticks to teleport the transform properties.
        /// </summary>
        [Tooltip("Distance the graphical object must move between ticks to teleport the transform properties.")]
        [Range(0.001f, ushort.MaxValue)]
        [SerializeField]
        private float _teleportThreshold = 1f;
        /// <summary>
        /// MoveRates to use.
        /// </summary>
        private MoveRates _moveRates;
        /// <summary>
        /// True if pretick ran.
        /// </summary>
        private bool _preTicked;
        /// <summary>
        /// TickDelta when started.
        /// </summary>
        private float _tickDelta;
        /// <summary>
        /// Last TransformProperties for Target.
        /// </summary>
        private TransformProperties? _targetProperties;
        /// <summary>
        /// Parent prior to detaching.
        /// </summary>
        private Transform _previousParent;

        public override void OnStartClient()
        {
            if (_target == null)
            { 
                base.NetworkManager.LogError($"{GetType().Name} on {transform.name} does not have a target specified.");
                return;
            }

            _previousParent = transform.parent;
            transform.SetParent(null);
            _tickDelta = (float)base.TimeManager.TickDelta;
            ChangeSubscriptions(true);
        }

        public override void OnStopClient()
        {
            ChangeSubscriptions(false);
        }

        private void Update()
        {
            MoveToTarget();
        }

        private void TimeManager_OnPreTick()
        {
            _preTicked = true;
        }

        private void TimeManager_OnPostTick()
        {
            if (_preTicked)
            { 
                SetMoveRates();
                _targetProperties = _target.GetWorldProperties();
            }
            _preTicked = false;
        }

        /// <summary>
        /// Changes subscriptions which are needed to function.
        /// </summary>
        private void ChangeSubscriptions(bool subscribe)
        {
            TimeManager tm = base.TimeManager;
            if (tm == null)
                return;

            if (subscribe)
            {
                base.TimeManager.OnPreTick += TimeManager_OnPreTick;
                base.TimeManager.OnPostTick += TimeManager_OnPostTick;
            }
        }

        /// <summary>
        /// Sets move rates from current position to goal using tickDelta.
        /// </summary>
        private void SetMoveRates()
        {
            float teleportT = (_enableTeleport) ? _teleportThreshold : MoveRatesCls.UNSET_VALUE;

            TransformProperties thisProperties = transform.GetWorldProperties();
            _moveRates = MoveRates.GetWorldMoveRates(thisProperties, _target, _tickDelta, teleportT);
        }

        /// <summary>
        /// Moves to target.
        /// </summary>
        private void MoveToTarget()
        {
            if (_target == null)
                return;
            if (!_targetProperties.HasValue)
                return;
            if (!_moveRates.AnySet)
                return;

            _moveRates.MoveLocalToTarget(transform, _targetProperties.Value, Time.deltaTime);
            /* If no longer spawned and to rechild this
             * then do so once at goal. */
            if (!base.IsSpawned && _attachOnStop)
            {
                if (transform.position == _target.position
                    && transform.rotation == _target.rotation
                    && transform.localScale == _target.localScale)
                {
                    _targetProperties = null;
                    transform.SetParent(_previousParent, true);
                    //Unset move data to prevent excess work.
                    _moveRates.Update(MoveRatesCls.UNSET_VALUE);
                }
            }
        }


    }

}