using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    internal class PredictedObjectOwnerSmoother
    {
        #region Serialized.
        /// <summary>
        /// Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.
        /// </summary>
        private Transform _graphicalObject;
        /// <summary>
        /// Sets GraphicalObject.
        /// </summary>
        /// <param name="value"></param>
        public void SetGraphicalObject(Transform value) => _graphicalObject = value;
        /// <summary>
        /// NetworkBehaviour which is using this object.
        /// </summary>
        private NetworkBehaviour _networkBehaviour;
        /// <summary>
        /// How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.
        /// </summary>
        private float _teleportThreshold = 1f;
        /// <summary>
        /// How far in the past to keep the graphical object when owner.
        /// </summary>
        private byte _interpolation = 1;
        /// <summary>
        /// Sets the interpolation value to use when the owner of this object.
        /// </summary>
        /// <param name="value"></param>
        public void SetInterpolation(byte value) => _interpolation = value;
        #endregion

        #region Private.
        /// <summary>
        /// World position before transform was predicted or reset.
        /// </summary>
        private Vector3 _graphicalStartPosition;
        /// <summary>
        /// World rotation before transform was predicted or reset.
        /// </summary>
        private Quaternion _graphicalStartRotation;
        /// <summary>
        /// Local position of transform when instantiated.
        /// </summary>
        private Vector3 _graphicalInstantiatedLocalPosition;
        /// <summary>
        /// How quickly to move towards TargetPosition.
        /// </summary>
        private float _positionMoveRate = -2;
        /// <summary>
        /// Local rotation of transform when instantiated.
        /// </summary>
        private Quaternion _graphicalInstantiatedLocalRotation;
        /// <summary>
        /// How quickly to move towards TargetRotation.
        /// </summary>
        private float _rotationMoveRate = -2;
        #endregion
    
        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        public void Initialize(NetworkBehaviour nb, Vector3 instantiatedLocalPosition, Quaternion instantiatedLocalRotation, Transform graphicalObject, byte interpolation, float teleportThreshold)
        {
            _networkBehaviour = nb;
            _graphicalInstantiatedLocalPosition = instantiatedLocalPosition;
            _graphicalInstantiatedLocalRotation = instantiatedLocalRotation;

            _graphicalObject = graphicalObject;
            _interpolation = interpolation;
            _teleportThreshold = teleportThreshold;
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        public void ManualUpdate()
        {
            MoveToTarget();
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPreTick.
        /// </summary>
        public void OnPreTick()
        {
            if (CanSmooth())
            {
                /* Only snap to destination if interpolation is 1.
                 * This ensures the graphics will be at the proper location
                 * before the next movement rates are calculated. */
                if (_interpolation == 1)
                {
                    _graphicalObject.localPosition = _graphicalInstantiatedLocalPosition;
                    _graphicalObject.localRotation = _graphicalInstantiatedLocalRotation;
                }
                SetGraphicalPreviousProperties();
            }
        }

        public void OnPostTick()
        {
            if (CanSmooth())
            {
                ResetGraphicalToPreviousProperties();
                SetGraphicalMoveRates();
            }
        }

        /// <summary>
        /// Returns if prediction can be used on this rigidbody.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            if (_interpolation == 0)
                return false;
            //Only owner needs smoothing.
            if (!_networkBehaviour.IsOwner && !_networkBehaviour.IsHost)
                return false;

            return true;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        private void MoveToTarget()
        {
            //Not set, meaning movement doesnt need to happen or completed.
            if (_positionMoveRate == -2f && _rotationMoveRate == -2f)
                return;

            /* Only try to update properties if they have a valid move rate.
             * Properties may have 0f move rate if they did not change. */
            Transform t = _graphicalObject;
            float delta = Time.deltaTime;
            //Position.
            if (_positionMoveRate == -1f)
                t.localPosition = _graphicalInstantiatedLocalPosition;
            else if (_positionMoveRate > 0f)
                t.localPosition = Vector3.MoveTowards(t.localPosition, _graphicalInstantiatedLocalPosition, _positionMoveRate * delta);
            //Rotation.
            if (_rotationMoveRate == -1f)
                t.localRotation = _graphicalInstantiatedLocalRotation;
            else if (_rotationMoveRate > 0f)
                t.localRotation = Quaternion.RotateTowards(t.localRotation, _graphicalInstantiatedLocalRotation, _rotationMoveRate * delta);

            if (GraphicalObjectMatches(_graphicalInstantiatedLocalPosition, _graphicalInstantiatedLocalRotation))
            {
                _positionMoveRate = -2f;
                _rotationMoveRate = -2f;
            }
        }

        /// <summary>
        /// Returns if this transform matches arguments.
        /// </summary>
        /// <returns></returns>
        private bool GraphicalObjectMatches(Vector3 localPosition, Quaternion localRotation)
        {
            return (_graphicalObject.localPosition == localPosition && _graphicalObject.localRotation == localRotation);
        }

        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        private void SetGraphicalMoveRates()
        {
            float delta = ((float)_networkBehaviour.TimeManager.TickDelta * _interpolation);

            float distance;
            distance = Vector3.Distance(_graphicalInstantiatedLocalPosition, _graphicalObject.localPosition);
            //If qualifies for teleporting.
            if (_teleportThreshold != -1f && distance >= _teleportThreshold)
            {
                _positionMoveRate = -1f;
                _rotationMoveRate = -1f;
            }
            //Smoothing.
            else
            {
                _positionMoveRate = (distance / delta);
                distance = Quaternion.Angle(_graphicalInstantiatedLocalRotation, _graphicalObject.localRotation);
                if (distance > 0f)
                    _rotationMoveRate = (distance / delta);
            }
        }

        /// <summary>
        /// Caches the transforms current position and rotation.
        /// </summary>
        private void SetGraphicalPreviousProperties()
        {
            _graphicalStartPosition = _graphicalObject.position;
            _graphicalStartRotation = _graphicalObject.rotation;
        }

        /// <summary>
        /// Resets the transform to cached position and rotation of the transform.
        /// </summary>
        private void ResetGraphicalToPreviousProperties()
        {
            _graphicalObject.SetPositionAndRotation(_graphicalStartPosition, _graphicalStartRotation);
        }

    }


}