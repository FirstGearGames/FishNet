using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Prediction
{
    internal class BasicTickSmoother
    {
#if PREDICTION_V2

        #region Private.
        /// <summary>
        /// Object to smooth.
        /// </summary>
        private Transform _graphicalObject;
        /// <summary>
        /// NetworkObject graphical belongs to.
        /// </summary>
        private NetworkObject _networkObject;
        /// <summary>
        /// When not 0f the graphical object will teleport into it's next position if the move distance exceeds this value.
        /// </summary>
        private float _teleportDistance;
        /// <summary>
        /// How quickly to move towards goal values.
        /// </summary>
        private MoveRates _moveRates;
        /// <summary>
        /// World values of the graphical during pretick.
        /// </summary>
        private TransformProperties _gfxInitializedLocalValues;
        /// <summary>
        /// World values of the NetworkObject during pretick.
        /// </summary>
        private TransformProperties _nobPreSimulateWorldValues;
        /// <summary>
        /// World values of the graphical after it's been aligned to initialized values in PreTick.
        /// </summary>
        private TransformProperties _gfxPreSimulateWorldValues;
        #endregion

        /// <summary>
        /// Initializes this smoother; should only be completed once.
        /// </summary>
        internal void InitializeOnce(Transform graphicalObject, float teleportDistance, NetworkObject nob)
        {
            _gfxInitializedLocalValues = graphicalObject.GetLocalProperties();
            _nobPreSimulateWorldValues = nob.transform.GetWorldProperties();
            _graphicalObject = graphicalObject;
            _teleportDistance = teleportDistance;
            _networkObject = nob;
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        internal void Update()
        {
            if (!CanSmooth())
                return;

            MoveToTarget();
        }


        /// <summary>
        /// Called when the TimeManager invokes OnPreTick.
        /// </summary>
        internal void OnPreTick()
        {
            if (!CanSmooth())
                return;

            _nobPreSimulateWorldValues = _networkObject.transform.GetWorldProperties();
            /* Snap graphical to nob since its iterated always
             * over 1 tick. This will line it up to it's initialized
             * position incase the movement didn't complete by 1 frame. 
             * 
             * Also set world cordinates of the graphics as it must be set
             * back to these values in post tick. Adding the initialized values
             * onto nobPreSimulateWorldValues would work too but it's easier
             * to just store the gfx world values separate. */
            _graphicalObject.SetLocalProperties(_gfxInitializedLocalValues);
            _gfxPreSimulateWorldValues = _graphicalObject.GetWorldProperties();
        }

        /// <summary>
        /// Called when TimeManager invokes OnPostTick.
        /// </summary>
        internal void OnPostTick()
        {
            if (!CanSmooth())
                return;

            SetMoveRates(_nobPreSimulateWorldValues, _networkObject.transform);
            _graphicalObject.SetWorldProperties(_gfxPreSimulateWorldValues);            
        }

        /// <summary>
        /// Returns if prediction can be used on this rigidbody.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            if (_graphicalObject == null)
                return false;
            if (_networkObject.IsServerOnlyStarted)
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

            Transform t = _graphicalObject;
            float delta = Time.deltaTime;
            float rate;

            rate = _moveRates.Position;
            if (rate == MoveRates.INSTANT_VALUE)
                t.localPosition = _gfxInitializedLocalValues.Position;
            else
                t.localPosition = Vector3.MoveTowards(t.localPosition, _gfxInitializedLocalValues.Position, rate * delta);

            rate = _moveRates.Rotation;
            if (rate == MoveRates.INSTANT_VALUE)
                t.localRotation = _gfxInitializedLocalValues.Rotation;
            else
                t.localRotation = Quaternion.RotateTowards(t.localRotation, _gfxInitializedLocalValues.Rotation, rate * delta);

            rate = _moveRates.Scale;
            if (rate == MoveRates.INSTANT_VALUE)
                t.localScale = _gfxInitializedLocalValues.LocalScale;
            else
                t.localScale = Vector3.MoveTowards(t.localScale, _gfxInitializedLocalValues.LocalScale, rate * delta);
        }



        /// <summary>
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        private void SetMoveRates(TransformProperties prevValues, Transform t)
        {
            float duration = (float)_networkObject.TimeManager.TickDelta;
            float rate;
            float distance;

            /* Position. */
            rate = t.position.GetRate(prevValues.Position, duration, out distance);
            //Basic teleport check.
            if (_teleportDistance != NetworkObject.TELEPORT_DISABLED_DISTANCE_THRESHOLD && distance > _teleportDistance)
            {
                _moveRates.Update(MoveRates.INSTANT_VALUE);
            }
            //Smoothing.
            else
            {
                float positionRate = rate.SetIfUnderTolerance(0.0001f, MoveRates.INSTANT_VALUE);
                rate = t.rotation.GetRate(prevValues.Rotation, duration, out _);
                float rotationRate = rate.SetIfUnderTolerance(0.2f, MoveRates.INSTANT_VALUE);
                rate = t.localScale.GetRate(prevValues.LocalScale, duration, out _);
                float scaleRate = rate.SetIfUnderTolerance(0.0001f, MoveRates.INSTANT_VALUE);
                _moveRates.Update(positionRate, rotationRate, scaleRate);
            }
            
        }



#endif
    }


}