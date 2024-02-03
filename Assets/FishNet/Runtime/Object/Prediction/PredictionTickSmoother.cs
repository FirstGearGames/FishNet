using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Prediction
{
    internal class PredictionTickSmoother
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
        /// When not MoveRatesCls.UNSET_VALUE the graphical object will teleport into it's next position if the move distance exceeds this value.
        /// </summary>
        private float _teleportThreshold;
        /// <summary>
        /// How quickly to move towards goal values.
        /// </summary>
        private MoveRates _moveRates;
        /// <summary>
        /// True if a pretick occurred since last postTick.
        /// </summary>
        private bool _preTicked;
        /// <summary>
        /// Local values of the graphical during pretick.
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
        /// <summary>
        /// TickDelta on the TimeManager.
        /// </summary>
        private float _tickDelta;
        /// <summary>
        /// How many ticks to interpolate over.
        /// </summary>
        private byte _interpolation;
        #endregion

        /// <summary>
        /// Initializes this smoother; should only be completed once.
        /// </summary>
        internal void InitializeOnce(Transform graphicalObject, float teleportDistance, NetworkObject nob, byte interpolation)
        {
            _gfxInitializedLocalValues = graphicalObject.GetLocalProperties();
            _nobPreSimulateWorldValues = nob.transform.GetWorldProperties();
            _tickDelta = (float)nob.TimeManager.TickDelta;
            _graphicalObject = graphicalObject;
            _teleportThreshold = (teleportDistance * (float)interpolation);
            _networkObject = nob;
            _interpolation = interpolation;
        }

        /// <summary>
        /// Keeps initialized values but unsets runtime values.
        /// </summary>
        internal void Deinitialize()
        {
            _graphicalObject.SetLocalProperties(_gfxInitializedLocalValues);
            _preTicked = false;
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

            _preTicked = true;

            _nobPreSimulateWorldValues = _networkObject.transform.GetWorldProperties();
            _gfxPreSimulateWorldValues = _graphicalObject.GetWorldProperties();
        }

        /// <summary>
        /// Called when TimeManager invokes OnPostTick.
        /// </summary>
        internal void OnPostTick()
        {
            if (!CanSmooth())
                return;

            //If preticked then previous transform values are known.
            if (_preTicked)
            {
                _graphicalObject.SetWorldProperties(_gfxPreSimulateWorldValues);
                SetMoveRates(_gfxInitializedLocalValues, _graphicalObject);
            }
            //If did not pretick then the only thing we can do is snap to instantiated values.
            else
            {
                _graphicalObject.SetLocalProperties(_gfxInitializedLocalValues);
            }
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
        /// Sets Position and Rotation move rates to reach Target datas.
        /// </summary>
        private void SetMoveRates(TransformProperties prevValues, Transform t)
        {
            float duration = (_tickDelta * (float)_interpolation);
            /* If interpolation is 1 then add on a tiny amount
             * of more time to compensate for frame time, so that
             * the smoothing does not complete before the next tick,
             * as this would result in jitter. */
            if (_interpolation == 1)
                duration += (1f / 50f);
            float teleportT = _teleportThreshold;
            _moveRates = MoveRates.GetLocalMoveRates(prevValues, t, duration, teleportT);
        }


        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveToTarget()
        {
            _moveRates.MoveLocalToTarget(_graphicalObject, _gfxInitializedLocalValues, Time.deltaTime);
        }

#endif
    }


}