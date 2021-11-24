#if PREDICTION
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Prediction
{

    public class DesyncSmoother : NetworkBehaviour
    {
        #region Serialized.
        /// <summary>
        /// How many ticks to smooth differences over.
        /// </summary>
        [Tooltip("How many ticks to smooth differences over.")]
        [SerializeField]
        private byte _ticks = 3;
        #endregion

        #region Private.
        /// <summary>
        /// True if subscribed to events.
        /// </summary>
        private bool _subscribed = false;
        #endregion

        /// <summary>
        /// Position prior to reconcile.
        /// </summary>
        private Vector3 _previousPosition;
        /// <summary>
        /// Rotation prior to reconcile.
        /// </summary>
        private Quaternion _previousRotation;
        /// <summary>
        /// How quickly to move position to starting point.
        /// </summary>
        private float _positionRate = -1f;
        /// <summary>
        /// How quickly to move rotation to starting point.
        /// </summary>
        private float _rotationRate = -1f;
        /// <summary>
        /// Local position of this transform during OnStartClient.
        /// </summary>
        private Vector3 _startPosition = Vector3.zero;
        /// <summary>
        /// Local rotation of this transform during OnStartClient.
        /// </summary>
        private Quaternion _startRotation = Quaternion.identity;

        private void OnDisable()
        {
            ChangeSubscriptions(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Transform t = transform;
            _startPosition = t.localPosition;
            _startRotation = t.localRotation;
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ChangeSubscriptions(base.IsOwner);
        }

        /// <summary>
        /// Subscribes to events needed to function.
        /// </summary>
        /// <param name="subscribe"></param>
        private void ChangeSubscriptions(bool subscribe)
        {
            if (subscribe && !_subscribed)
            {
                base.TimeManager.OnPreReconcile += TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReconcile += TimeManager_OnPostReconcile;
            }
            else if (!subscribe && _subscribed)
            {
                base.TimeManager.OnPreReconcile -= TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReconcile -= TimeManager_OnPostReconcile;
            }

            _subscribed = subscribe;
        }


        /// <summary>
        /// Called before performing a reconcile on NetworkBehaviour.
        /// </summary>
        private void TimeManager_OnPreReconcile(NetworkBehaviour obj)
        {
            Transform t = transform;
            _previousPosition = t.position;
            _previousRotation = t.rotation;
        }

        /// <summary>
        /// Called after performing a reconcile on a NetworkBehaviour.
        /// </summary>
        private void TimeManager_OnPostReconcile(NetworkBehaviour obj)
        {
            //Set transform back to where it was before reconcile so there's no visual disturbances.
            Transform t;
            (t = transform).SetPositionAndRotation(_previousPosition, _previousRotation);

            float tickDelta = (float)base.TimeManager.TickDelta;
            float goalTime = tickDelta * (float)_ticks;
            float distance;

            //Calculate move rates based on time to complete vs distance required.
            distance = (t.localPosition - _startPosition).magnitude;
            _positionRate = distance / goalTime;
            distance = Quaternion.Angle(t.localRotation, _startRotation);
            _rotationRate = distance / goalTime;
        }

        private void Update()
        {
            //If position should move.
            if (_positionRate > 0f)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, _startPosition, _positionRate * Time.deltaTime);
                if (transform.localPosition == _startPosition)
                    _positionRate = -1f;
            }
            //If rotation should move.
            if (_rotationRate > 0f)
            {
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, _startRotation, _rotationRate * Time.deltaTime);
                if (transform.localRotation == _startRotation)
                    _rotationRate = -1f;
            }
        }
    }


}
#endif