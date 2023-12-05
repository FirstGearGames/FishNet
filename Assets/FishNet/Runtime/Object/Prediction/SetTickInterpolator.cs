//using FishNet.Utility.Extension;
//using GameKit.Dependencies.Utilities;
//using System.Collections.Generic;
//using System.Runtime.CompilerServices;
//using UnityEngine;

//namespace FishNet.Object.Prediction
//{
//    internal class SetTickInterpolator : IResettable
//    {
//#if PREDICTION_V2
//        /// <summary>
//        /// Goal to move towards.
//        /// </summary>
//        private struct MoveGoal
//        {
//            /// <summary>
//            /// Amount of delta passed on the move.
//            /// </summary>
//            public float Delta;
//            /// <summary>
//            /// Goal to move towards.
//            /// </summary>
//            public TransformProperties Goal;
//        }
//        /// <summary>
//        /// Data to be used to configure smoothing for an owned predicted object.
//        /// </summary>
//        internal struct SetTickInterpolatorData
//        {
//            /// <summary>
//            /// Object to smooth.
//            /// </summary>
//            public Transform GraphicalObject;
//            /// <summary>
//            /// Number of tick goals to cache.
//            /// </summary>
//            public byte Interpolation;
//            /// <summary>
//            /// NetworkObject which the GraphicalObject belongs.
//            /// </summary>
//            public NetworkObject NetworkObject;
//            /// <summary>
//            /// True to smooth position. When false position will not be updated.
//            /// </summary>
//            public bool SmoothPosition;
//            /// <summary>
//            /// True to smooth rotation. When false rotation will not be updated.
//            /// </summary>
//            public bool SmoothRotation;
//            /// <summary>
//            /// True to smooth scale. When false scale will not be updated.
//            /// </summary>
//            public bool SmoothScale;
//            /// <summary>
//            /// Distance between each tick which must be passed for the GraphicalObject to teleport for that tick.
//            /// </summary>
//            public float TeleportThreshold;
//        }


//        #region Private.
//        /// <summary>
//        /// Local values of the graphical object when this was initialized.
//        /// </summary>
//        private TransformProperties _graphicalInitializedLocalValues;
//        /// <summary>
//        /// Values of the graphical object during PreTick or PreReplay.
//        /// </summary>
//        private TransformProperties _graphicalPreSimulateWorldValues;
//        /// <summary>
//        /// SmoothingData to use.
//        /// </summary>
//        private SetTickInterpolatorData _smoothingData;
//        /// <summary>
//        /// GraphicalObject to smooth.
//        /// </summary>
//        private Transform _graphicalObject => _smoothingData.GraphicalObject;
//        /// <summary>
//        /// Goals to move towards.
//        /// </summary>
//        private Queue<MoveGoal> _goals;
//        /// <summary>
//        /// Delta per tick.
//        /// </summary>
//        private float _tickDelta;
//        /// <summary>
//        /// Properties of the graphicalObject after the previous PostTick.
//        /// </summary>
//        private TransformProperties _lastTransformProperties;
//        #endregion

//        /// <summary>
//        /// Initializes this smoother; should only be completed once.
//        /// </summary>
//        /// <param name="data"></param>
//        internal void Initialize(SetTickInterpolatorData data)
//        {
//            _smoothingData = data;
//            _tickDelta = (float)data.NetworkObject.TimeManager.TickDelta;
//            SetGraphicalObject(_graphicalObject);
//            _graphicalPreSimulateWorldValues = _smoothingData.GraphicalObject.GetWorldProperties();
//        }

//        /// <summary>
//        /// Sets GraphicalObject; can be changed at runtime.
//        /// </summary>
//        /// <param name="value"></param>
//        internal void SetGraphicalObject(Transform value)
//        {
//            //Unchanged.
//            if (value == _graphicalObject)
//                return;

//            _smoothingData.GraphicalObject = value;
//            _graphicalInitializedLocalValues.Update(value.localPosition, value.localRotation, value.localScale);
//            //Clear goals to start anew.
//            _goals.Clear();
//        }
//        /// <summary>
//        /// Sets the interpolation value to use when the owner of this object.
//        /// </summary>
//        /// <param name="value">New interpolation value.</param>
//        internal void SetInterpolation(byte value)
//        {
//            if (value <= 0)
//                value = 1;

//            _smoothingData.Interpolation = value;
//        }

//        /// <summary>
//        /// Called every frame.
//        /// </summary>
//        internal void Update()
//        {
//            if (CanSmooth())
//                MoveToTarget();
//        }


//        /// <summary>
//        /// Called when the TimeManager invokes OnPreTick.
//        /// </summary>
//        internal void OnPreTick()
//        {
//            if (CanSmooth())
//            {
//                /* If goal delta has not been completed yet
//                 * then snap to the goal. This can happen very very
//                 * rarely but the jump will be so insignificant it
//                 * will go unseen. */
//                if (_goals.TryPeek(out MoveGoal result))
//                {
//                    if (result.Delta < _tickDelta)
//                    {
//                        Transform t = _graphicalObject;
//                        if (_smoothingData.SmoothPosition)
//                            t.localPosition = _graphicalInitializedLocalValues.Position;
//                        if (_smoothingData.SmoothRotation)
//                            t.localRotation = _graphicalInitializedLocalValues.Rotation;
//                        if (_smoothingData.SmoothScale)
//                            t.localScale = _graphicalInitializedLocalValues.LocalScale;
//                    }
//                }

//                _graphicalPreSimulateWorldValues = _graphicalObject.GetWorldProperties();
//            }
//        }

//        /// <summary>
//        /// Called when TimeManager invokes OnPostTick.
//        /// </summary>
//        internal void OnPostTick()
//        {
//            if (CanSmooth())
//            {
//                //Move back to position during pretick (presimulate).
//                _graphicalObject.SetPositionAndRotation(_graphicalPreSimulateWorldValues.Position, _graphicalPreSimulateWorldValues.Rotation);
//                //Creates a goal using current transform properties.
//                CreateGoal();
//            }
//        }

//        /// <summary>
//        /// Sets Position and Rotation move rates to reach Target datas.
//        /// </summary>
//        private void CreateGoal()
//        {
//            //First check if the goal is the same as the last.
//            TransformProperties current = _graphicalObject.GetWorldProperties();
//            //If values did not change then no need to add to the queue.
//            if (current.ValuesEquals(_lastTransformProperties))
//                return;
//            _lastTransformProperties = current;

//            //uint interval = _smoothingData.Interpolation;
//            //float delta = (float)_smoothingData.NetworkObject?.TimeManager.TickDelta;

//            //float rate;
//            //float distance;
//            //Transform t = _graphicalObject;
//            ///* Position. */
//            //rate = t.localPosition.GetRate(_graphicalInitializedLocalValues.Position, delta, out distance, interval);
//            ////If qualifies for teleporting.
//            //if (_smoothingData.TeleportThreshold != MoveRates.UNSET_VALUE && distance >= _smoothingData.TeleportThreshold)
//            //{
//            //    _moveRates.Update(MoveRates.INSTANT_VALUE);
//            //}
//            ////Smoothing.
//            //else
//            //{
//            //    float positionRate = rate.SetIfUnderTolerance(0.0001f, MoveRates.INSTANT_VALUE);
//            //    rate = t.localRotation.GetRate(_graphicalInitializedLocalValues.Rotation, delta, out _, interval);
//            //    float rotationRate = rate.SetIfUnderTolerance(0.2f, MoveRates.INSTANT_VALUE);
//            //    _moveRates.Update(positionRate, rotationRate, MoveRates.UNSET_VALUE);
//            //}
//        }

//        /// <summary>
//        /// Returns if prediction can be used on this rigidbody.
//        /// </summary>
//        /// <returns></returns>
//        private bool CanSmooth()
//        {
//            NetworkObject nob = _smoothingData.NetworkObject;
//            if (nob == null)
//                return false;
//            if (nob.IsServerOnlyStarted)
//                return false;

//            return true;
//        }

//        /// <summary>
//        /// Moves transform to target values.
//        /// </summary>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private void MoveToTarget()
//        {
//            //No goals.
//            if (_goals.Count == 0)
//                return;

//            Vector3 posGoal = GetGraphicalGoalPosition();
//            Quaternion rotGoal = GetGraphicalGoalRotation();
//            Vector3 scaleGoal = GetGraphicalGoalScale();
//            /* Only try to update properties if they have a valid move rate.
//             * Properties may have 0f move rate if they did not change. */
//            Transform t = _smoothingData.GraphicalObject;
//            float delta = Time.deltaTime;

//            //Position.
//            if (_smoothingData.SmoothPosition)
//            {
//                if (_moveRates.InstantPosition)
//                    ResetGraphicalToInitializedLocalOffsets(true, false);
//                else if (_moveRates.PositionSet)
//                    t.localPosition = Vector3.MoveTowards(t.localPosition, posGoal, _moveRates.Position * delta);
//            }

//            //Rotation.
//            if (_smoothingData.SmoothRotation)
//            {
//                if (_moveRates.InstantRotation)
//                    ResetGraphicalToInitializedLocalOffsets(false, true);
//                else if (_moveRates.RotationSet)
//                    t.localRotation = Quaternion.RotateTowards(t.localRotation, rotGoal, _moveRates.Rotation * delta);
//            }

//            if (GraphicalObjectMatchesLocalValues(posGoal, rotGoal))
//                _moveRates.Update(MoveRates.UNSET_VALUE);
//        }

//        /// <summary>
//        /// Returns if this transform matches arguments.
//        /// </summary>
//        /// <returns></returns>
//        private bool GraphicalObjectMatchesLocalValues(Vector3 position, Quaternion rotation)
//        {
//            bool positionMatches = (!_smoothingData.SmoothPosition || (_graphicalObject.localPosition == position));
//            bool rotationMatches = (!_smoothingData.SmoothRotation || (_graphicalObject.localRotation == rotation));
//            return (positionMatches && rotationMatches);
//        }

//        /// <summary>
//        /// Sets Position and Rotation move rates to reach Target datas.
//        /// </summary>
//        private void SetGraphicalMoveRates()
//        {
//            uint interval = _smoothingData.Interpolation;
//            float delta = (float)_smoothingData.NetworkObject?.TimeManager.TickDelta;

//            float rate;
//            float distance;
//            Transform t = _graphicalObject;
//            /* Position. */
//            rate = t.localPosition.GetRate(_graphicalInitializedLocalValues.Position, delta, out distance, interval);
//            //If qualifies for teleporting.
//            if (_smoothingData.TeleportThreshold != MoveRates.UNSET_VALUE && distance >= _smoothingData.TeleportThreshold)
//            {
//                _moveRates.Update(MoveRates.INSTANT_VALUE);
//            }
//            //Smoothing.
//            else
//            {
//                float positionRate = rate.SetIfUnderTolerance(0.0001f, MoveRates.INSTANT_VALUE);
//                rate = t.localRotation.GetRate(_graphicalInitializedLocalValues.Rotation, delta, out _, interval);
//                float rotationRate = rate.SetIfUnderTolerance(0.2f, MoveRates.INSTANT_VALUE);
//                _moveRates.Update(positionRate, rotationRate, MoveRates.UNSET_VALUE);
//            }
//        }
  
//        /// <summary>
//        /// Resets the graphical object to it's starting position during initialization.
//        /// </summary>
//        private void ResetGraphicalObjectPositionToDefault()
//        {
//            if (_smoothingData.SmoothPosition)
//                _graphicalObject.localPosition = _graphicalInitializedLocalValues.Position;
//        }
//        /// <summary>
//        /// Resets the graphical object to it's starting position during initialization.
//        /// </summary>
//        private void ResetGraphicalObjectRotationToDefault()
//        {
//            if (_smoothingData.SmoothRotation)
//                _graphicalObject.localRotation = _graphicalInitializedLocalValues.Rotation;
//        }

//        public void ResetState()
//        {
//            //Even though this is a struct set it default so it's references are lost.
//            _smoothingData = default;
//            CollectionCaches<MoveGoal>.Store(_goals);

//        }

//        public void InitializeState()
//        {
//            _goals = CollectionCaches<MoveGoal>.RetrieveQueue();
//        }

//#endif
//    }


//}