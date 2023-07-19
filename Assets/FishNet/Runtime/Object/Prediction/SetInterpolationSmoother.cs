using FishNet.Utility.Extension;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Prediction
{
    internal class SetInterpolationSmoother
    {
#if PREDICTION_V2

        #region Private.
        /// <summary>
        /// How quickly to move towards goal values.
        /// </summary>
        private MoveRates _moveRates;
        /// <summary>
        /// Offsets of the graphical object when this was initialized.
        /// </summary>
        private TransformProperties _initializedOffsets;
        /// <summary>
        /// Offsets of the graphical object prior to the NetworkObject transform moving.
        /// </summary>
        private TransformProperties _interpolationOffsets;
        /// <summary>
        /// SmoothingData to use.
        /// </summary>
        private SetInterpolationSmootherData _smoothingData;
        #endregion

        /// <summary>
        /// Initializes this smoother; should only be completed once.
        /// </summary>
        /// <param name="data"></param>
        internal void InitializeOnce(SetInterpolationSmootherData data)
        {
            _smoothingData = data;
            SetGraphicalObject(data.GraphicalObject);
            _moveRates = new MoveRates(MoveRates.UNSET_VALUE);
            SetGraphicalPreviousProperties();
        }

        /// <summary>
        /// Sets GraphicalObject; can be changed at runtime.
        /// </summary>
        /// <param name="value"></param>
        internal void SetGraphicalObject(Transform value)
        {
            _smoothingData.GraphicalObject = value;
            _initializedOffsets = _smoothingData.GraphicalObject.transform.GetTransformOffsets(_smoothingData.NetworkObject.transform);
        }
        /// <summary>
        /// Sets the interpolation value to use when the owner of this object.
        /// </summary>
        /// <param name="value">New interpolation value.</param>
        internal void SetInterpolation(byte value)
        {
            if (value < 1)
                value = 1;

            _smoothingData.Interpolation = value;
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        internal void Update()
        {
            if (CanSmooth())
                MoveToTarget();
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPreTick.
        /// </summary>
        internal void OnPreTick()
        {
            if (CanSmooth())
            {
                /* Only snap to destination if interpolation is 1.
                 * This ensures the graphics will be at the proper location
                 * before the next movement rates are calculated. */
                if (_smoothingData.Interpolation == 1)
                    ResetGraphicalToInstantiatedProperties(true, true);

                SetGraphicalPreviousProperties();
            }
        }

        /// <summary>
        /// Called when TimeManager invokes OnPostTick.
        /// </summary>
        internal void OnPostTick()
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
            NetworkObject nob = _smoothingData.NetworkObject;
            if (nob == null)
                return false;
            if (nob.IsServerOnly)
                return false;
            if (nob.SpectatorAdaptiveInterpolation && !nob.IsOwner)
                return false;

            return true;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveToTarget()
        {
            //No rates are set.
            if (!_moveRates.AnySet)
                return;

            Vector3 posGoal = GetGraphicalGoalPosition();
            Quaternion rotGoal = GetGraphicalGoalRotation();

            /* Only try to update properties if they have a valid move rate.
             * Properties may have 0f move rate if they did not change. */
            Transform t = _smoothingData.GraphicalObject;
            float delta = Time.deltaTime;

            //Position.
            if (_smoothingData.SmoothPosition)
            {
                if (_moveRates.InstantPosition)
                    ResetGraphicalToInstantiatedProperties(true, false);
                else if (_moveRates.PositionSet)
                    t.position = Vector3.MoveTowards(t.position, posGoal, _moveRates.Position * delta);
            }

            //Rotation.
            if (_smoothingData.SmoothRotation)
            {
                if (_moveRates.InstantRotation)
                    ResetGraphicalToInstantiatedProperties(false, true);
                else if (_moveRates.RotationSet)
                    t.rotation = Quaternion.RotateTowards(t.rotation, rotGoal, _moveRates.Rotation * delta);
            }

            if (GraphicalObjectMatches(posGoal, rotGoal))
                _moveRates.SetValues(MoveRates.UNSET_VALUE);
        }

        /// <summary>
        /// Returns if this transform matches arguments.
        /// </summary>
        /// <returns></returns>
        private bool GraphicalObjectMatches(Vector3 position, Quaternion rotation)
        {
            bool positionMatches = (!_smoothingData.SmoothPosition || (_smoothingData.GraphicalObject.position == position));
            bool rotationMatches = (!_smoothingData.SmoothRotation || (_smoothingData.GraphicalObject.rotation == rotation));
            return (positionMatches && rotationMatches);
        }

        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        private void SetGraphicalMoveRates()
        {
            float delta = ((float)_smoothingData.NetworkObject?.TimeManager.TickDelta * _smoothingData.Interpolation);

            float distance;
            distance = Vector3.Distance(_smoothingData.GraphicalObject.position, GetGraphicalGoalPosition());
            //If qualifies for teleporting.
            if (_smoothingData.TeleportThreshold != MoveRates.UNSET_VALUE && distance >= _smoothingData.TeleportThreshold)
            {
                _moveRates.SetValues(MoveRates.INSTANT_VALUE);
            }
            //Smoothing.
            else
            {
                float positionRate =  (distance / delta);
                distance = Quaternion.Angle(_smoothingData.GraphicalObject.rotation, GetGraphicalGoalRotation());
                float rotationRate = (distance > 0f) ? (distance / delta) : MoveRates.INSTANT_VALUE;
                _moveRates.SetValues(positionRate, rotationRate, MoveRates.UNSET_VALUE);
            }
        }

        /// <summary>
        /// Gets a goal position for the graphical object.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 GetGraphicalGoalPosition()
        {
            if (_smoothingData.SmoothPosition)
                return (_smoothingData.NetworkObject.transform.position + _initializedOffsets.Position);
            else
                return _smoothingData.GraphicalObject.position;
        }

        /// <summary>
        /// Gets a goal rotation for the graphical object.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Quaternion GetGraphicalGoalRotation()
        {
            if (_smoothingData.SmoothRotation)
                return (_initializedOffsets.Rotation * _smoothingData.NetworkObject.transform.rotation);
            else
                return _smoothingData.GraphicalObject.rotation;
        }
        /// <summary>
        /// Caches the graphical object' current position and rotation.
        /// </summary>
        private void SetGraphicalPreviousProperties()
        {
            Transform graphical = _smoothingData.GraphicalObject;
            _interpolationOffsets.Position = graphical.position;
            _interpolationOffsets.Rotation = graphical.rotation;
        }

        /// <summary>
        /// Resets the graphical object to cached position and rotation of the transform.
        /// </summary>
        private void ResetGraphicalToPreviousProperties()
        {
            _smoothingData.GraphicalObject.SetPositionAndRotation(_interpolationOffsets.Position, _interpolationOffsets.Rotation);
        }

        /// <summary>
        /// Resets the graphical object to it's transform offsets during instantiation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetGraphicalToInstantiatedProperties(bool position, bool rotation)
        {
            Transform graphical = _smoothingData.GraphicalObject;
            if (position)
                graphical.position = GetGraphicalGoalPosition();
            if (rotation)
                graphical.rotation = GetGraphicalGoalRotation();
        }

#endif
    }


}