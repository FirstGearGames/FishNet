using FishNet.Utility.Extension;
using FishNet.Object;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Component.Prediction
{
    internal class PredictedObjectOwnerSmoother
    {
#if !PREDICTION_V2

        #region Serialized.
        /// <summary>
        /// Transform which holds the graphical features of this object. This transform will be smoothed when desynchronizations occur.
        /// </summary>
        private Transform _graphicalObject;
        /// <summary>
        /// Sets GraphicalObject.
        /// </summary>
        /// <param name="value"></param>
        public void SetGraphicalObject(Transform value)
        {
            _graphicalObject = value;
            _networkBehaviour.transform.SetTransformOffsets(value, ref _graphicalInstantiatedOffsetPosition, ref _graphicalInstantiatedOffsetRotation);
        }
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
        /// GraphicalObject position difference from the PredictedObject when this is initialized.
        /// </summary>
        private Vector3 _graphicalInstantiatedOffsetPosition;
        /// <summary>
        /// How quickly to move towards TargetPosition.
        /// </summary>
        private float _positionMoveRate = -2;
        /// <summary>
        /// GraphicalObject rotation difference from the PredictedObject when this is initialized.
        /// </summary>
        private Quaternion _graphicalInstantiatedOffsetRotation;
        /// <summary>
        /// How quickly to move towards TargetRotation.
        /// </summary>
        private float _rotationMoveRate = -2;
        /// <summary>
        /// True if OnPreTick was received this frame.
        /// </summary>
        private bool _preTickReceived;
        /// <summary>
        /// True to move towards position goals.
        /// </summary>
        private bool _smoothPosition;
        /// <summary>
        /// True to move towards rotation goals.
        /// </summary>
        private bool _smoothRotation;
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        public void Initialize(NetworkBehaviour nb, Vector3 instantiatedOffsetPosition, Quaternion instantiatedOffsetRotation, Transform graphicalObject
              , bool smoothPosition, bool smoothRotation, byte interpolation, float teleportThreshold)
        {
            _networkBehaviour = nb;
            _graphicalInstantiatedOffsetPosition = instantiatedOffsetPosition;
            _graphicalInstantiatedOffsetRotation = instantiatedOffsetRotation;
            _graphicalObject = graphicalObject;

            _smoothPosition = smoothPosition;
            _smoothRotation = smoothRotation;

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
                _preTickReceived = true;
                /* Only snap to destination if interpolation is 1.
                 * This ensures the graphics will be at the proper location
                 * before the next movement rates are calculated. */
                if (_interpolation == 1)
                    ResetGraphicalToInstantiatedProperties(true, true);

                SetGraphicalPreviousProperties();
            }
        }

        public void OnPostTick()
        {
            if (CanSmooth() && _preTickReceived)
            {
                _preTickReceived = false;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveToTarget()
        {
            //Not set, meaning movement doesnt need to happen or completed.
            if (_positionMoveRate == -2f && _rotationMoveRate == -2f)
                return;

            Vector3 posGoal = GetGraphicalGoalPosition();
            Quaternion rotGoal = GetGraphicalGoalRotation();

            /* Only try to update properties if they have a valid move rate.
             * Properties may have 0f move rate if they did not change. */
            Transform t = _graphicalObject;
            float delta = Time.deltaTime;

            //Position.
            if (SmoothPosition())
            {
                if (_positionMoveRate == -1f)
                    ResetGraphicalToInstantiatedProperties(true, false);
                else if (_positionMoveRate > 0f)
                    t.position = Vector3.MoveTowards(t.position, posGoal, _positionMoveRate * delta);
            }

            //Rotation.
            if (SmoothRotation())
            {
                if (_rotationMoveRate == -1f)
                    ResetGraphicalToInstantiatedProperties(false, true);
                else if (_rotationMoveRate > 0f)
                    t.rotation = Quaternion.RotateTowards(t.rotation, rotGoal, _rotationMoveRate * delta);
            }

            if (GraphicalObjectMatches(posGoal, rotGoal))
            {
                _positionMoveRate = -2f;
                _rotationMoveRate = -2f;
            } 
        }

        /// <summary>
        /// Returns if this transform matches arguments.
        /// </summary>
        /// <returns></returns>
        private bool GraphicalObjectMatches(Vector3 position, Quaternion rotation)
        {
            bool positionMatches = (!_smoothPosition || (_graphicalObject.position == position));
            bool rotationMatches = (!_smoothRotation || (_graphicalObject.rotation == rotation));
            return (positionMatches && rotationMatches);
        }

        /// <summary>
        /// True to smooth position. When false the graphicalObjects property will not be updated.
        /// </summary>
        /// <returns></returns>
        private bool SmoothPosition() => (_smoothPosition && (_networkBehaviour.IsOwner || _networkBehaviour.IsHost));
        /// <summary>
        /// True to smooth rotation. When false the graphicalObjects property will not be updated.
        /// </summary>
        /// <returns></returns>
        private bool SmoothRotation() => (_smoothRotation && (_networkBehaviour.IsOwner || _networkBehaviour.IsHost));

        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        private void SetGraphicalMoveRates()
        {
            float delta = ((float)_networkBehaviour.TimeManager.TickDelta * _interpolation);

            float distance;
            distance = Vector3.Distance(_graphicalObject.position, GetGraphicalGoalPosition());
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
                distance = Quaternion.Angle(_graphicalObject.rotation, GetGraphicalGoalRotation());
                if (distance > 0f)
                    _rotationMoveRate = (distance / delta);
            }
        }

        /// <summary>
        /// Gets a goal position for the graphical object.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 GetGraphicalGoalPosition()
        {
            if (SmoothPosition())
                return (_networkBehaviour.transform.position + _graphicalInstantiatedOffsetPosition);
            else
                return _graphicalObject.position;
        }

        /// <summary>
        /// Gets a goal rotation for the graphical object.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Quaternion GetGraphicalGoalRotation()
        {
            if (SmoothRotation())
                return (_graphicalInstantiatedOffsetRotation * _networkBehaviour.transform.rotation);
            else
                return _graphicalObject.rotation;
        }
        /// <summary>
        /// Caches the graphical object' current position and rotation.
        /// </summary>
        private void SetGraphicalPreviousProperties()
        {
            _graphicalStartPosition = _graphicalObject.position;
            _graphicalStartRotation = _graphicalObject.rotation;
        }

        /// <summary>
        /// Resets the graphical object to cached position and rotation of the transform.
        /// </summary>
        private void ResetGraphicalToPreviousProperties()
        {
            _graphicalObject.SetPositionAndRotation(_graphicalStartPosition, _graphicalStartRotation);
        }

        /// <summary>
        /// Resets the graphical object to it's transform offsets during instantiation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetGraphicalToInstantiatedProperties(bool position, bool rotation)
        {
            if (position)
                _graphicalObject.position = GetGraphicalGoalPosition();
            if (rotation)
                _graphicalObject.rotation = GetGraphicalGoalRotation();
        }

#endif
    }


}