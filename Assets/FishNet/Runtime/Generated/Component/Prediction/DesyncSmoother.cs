using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Prediction
{

    public class DesyncSmoother : NetworkBehaviour
    {
        #region Serialized.
        /// <summary>
        /// How much time to smooth desyncs over.
        /// </summary>
        [SerializeField, HideInInspector]
        private float _smoothingDuration = 0.15f;
        /// <summary>
        /// Sets SmoothingDuration to value.
        /// </summary>
        /// <param name="value"></param>
        internal void SetSmoothingDuration(float value) => _smoothingDuration = value;
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
        /// Local position of transform when instantiated.
        /// </summary>
        private Vector3 _instantiatedPosition;
        /// <summary>
        /// Local rotation of transform when instantiated.
        /// </summary>
        private Quaternion _instantiatedRotation;

        private void OnDisable()
        {
            ChangeSubscriptions(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Transform t = transform;
            _instantiatedPosition = t.localPosition;
            _instantiatedRotation = t.localRotation;
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
            if (base.TimeManager == null)
                return;
            if (subscribe == _subscribed)
                return;

            if (subscribe)
            {
                base.TimeManager.OnPreReconcile += TimeManager_OnPreReconcile;
                base.TimeManager.OnPostReconcile += TimeManager_OnPostReconcile;
            }
            else
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
            Transform t = transform;
            t.SetPositionAndRotation(_previousPosition, _previousRotation);


            float distance;

            //Calculate move rates based on time to complete vs distance required.
            distance = (t.localPosition - _instantiatedPosition).magnitude;
            _positionRate = distance / _smoothingDuration;
            distance = Quaternion.Angle(t.localRotation, _instantiatedRotation);
            _rotationRate = distance / _smoothingDuration;
        }

        private void Update()
        {
            //If position should move.
            if (_positionRate > 0f)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, _instantiatedPosition, _positionRate * Time.deltaTime);
                if (transform.localPosition == _instantiatedPosition)
                    _positionRate = -1f;
            }
            //If rotation should move.
            if (_rotationRate > 0f)
            {
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, _instantiatedRotation, _rotationRate * Time.deltaTime);
                if (transform.localRotation == _instantiatedRotation)
                    _rotationRate = -1f;
            }
        }
    }


}