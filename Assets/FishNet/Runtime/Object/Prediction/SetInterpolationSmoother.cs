using FishNet.Utility.Extension;
using GameKit.Utilities;
using System.Collections.Generic;
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
        /// Local values of the graphical object when this was initialized.
        /// </summary>
        private TransformProperties _graphicalInitializedLocalValues;
        /// <summary>
        /// Values of the graphical object during PreTick or PreReplay.
        /// </summary>
        private TransformProperties _graphicalPreSimulateWorldValues;
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
            _graphicalPreSimulateWorldValues = _smoothingData.GraphicalObject.GetWorldProperties();
        }

        /// <summary>
        /// Sets GraphicalObject; can be changed at runtime.
        /// </summary>
        /// <param name="value"></param>
        internal void SetGraphicalObject(Transform value)
        {
            _smoothingData.GraphicalObject = value;
            _graphicalInitializedLocalValues.Update(value.localPosition, value.localRotation, value.localScale);
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
                    ResetGraphicalToInitializedLocalOffsets(true, true);

                _graphicalPreSimulateWorldValues = _smoothingData.GraphicalObject.GetWorldProperties();
            }
        }

        /// <summary>
        /// Called when TimeManager invokes OnPostTick.
        /// </summary>
        internal void OnPostTick()
        {
            if (CanSmooth())
            {
                _smoothingData.GraphicalObject.SetPositionAndRotation(_graphicalPreSimulateWorldValues.Position, _graphicalPreSimulateWorldValues.Rotation);
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
            if (!nob.IsHost && nob.SpectatorAdaptiveInterpolation && !nob.IsOwner)
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
                    ResetGraphicalToInitializedLocalOffsets(true, false);
                else if (_moveRates.PositionSet)
                    t.localPosition = Vector3.MoveTowards(t.localPosition, posGoal, _moveRates.Position * delta);
            }

            //Rotation.
            if (_smoothingData.SmoothRotation)
            {
                if (_moveRates.InstantRotation)
                    ResetGraphicalToInitializedLocalOffsets(false, true);
                else if (_moveRates.RotationSet)
                    t.localRotation = Quaternion.RotateTowards(t.localRotation, rotGoal, _moveRates.Rotation * delta);
            }

            if (GraphicalObjectMatchesLocalValues(posGoal, rotGoal))
                _moveRates.Update(MoveRates.UNSET_VALUE);
        }

        /// <summary>
        /// Returns if this transform matches arguments.
        /// </summary>
        /// <returns></returns>
        private bool GraphicalObjectMatchesLocalValues(Vector3 position, Quaternion rotation)
        {
            bool positionMatches = (!_smoothingData.SmoothPosition || (_smoothingData.GraphicalObject.localPosition == position));
            bool rotationMatches = (!_smoothingData.SmoothRotation || (_smoothingData.GraphicalObject.localRotation == rotation));
            return (positionMatches && rotationMatches);
        }

        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        private void SetGraphicalMoveRates()
        {
            uint interval = _smoothingData.Interpolation;
            float delta = (float)_smoothingData.NetworkObject?.TimeManager.TickDelta;

            float rate;
            float distance;
            Transform t = _smoothingData.GraphicalObject;
            /* Position. */
            rate = t.localPosition.GetRate(_graphicalInitializedLocalValues.Position, delta, out distance, interval);
            //If qualifies for teleporting.
            if (_smoothingData.TeleportThreshold != MoveRates.UNSET_VALUE && distance >= _smoothingData.TeleportThreshold)
            {
                _moveRates.Update(MoveRates.INSTANT_VALUE);
            }
            //Smoothing.
            else
            {
                float positionRate = rate.SetIfUnderTolerance(0.0001f, MoveRates.INSTANT_VALUE);
                rate = t.localRotation.GetRate(_graphicalInitializedLocalValues.Rotation, delta, out _, interval);
                float rotationRate = rate.SetIfUnderTolerance(0.2f, MoveRates.INSTANT_VALUE);
                _moveRates.Update(positionRate, rotationRate, MoveRates.UNSET_VALUE);
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
                return _graphicalInitializedLocalValues.Position;
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
                return _graphicalInitializedLocalValues.Rotation;
            else
                return _smoothingData.GraphicalObject.rotation;
        }

        /// <summary>
        /// Resets the graphical object to it's transform offsets during instantiation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetGraphicalToInitializedLocalOffsets(bool position, bool rotation)
        {
            Transform graphical = _smoothingData.GraphicalObject;
            if (position)
                graphical.localPosition = GetGraphicalGoalPosition();
            if (rotation)
                graphical.localRotation = GetGraphicalGoalRotation();
        }

#endif
    }


}