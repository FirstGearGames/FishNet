//Remove on 2024/06/01
//using FishNet.Utility.Extension;
//using System.Runtime.CompilerServices;
//using UnityEngine;

//namespace FishNet.Object.Prediction
//{
//    internal class LocalTransformTickSmoother
//    {
//        #region Private.
//        /// <summary>
//        /// Object to smooth.
//        /// </summary>
//        private Transform _transform;
//        /// <summary>
//        /// When not 0f the graphical object will teleport into it's next position if the move distance exceeds this value.
//        /// </summary>
//        private float _teleportThreshold;
//        /// <summary>
//        /// How quickly to move towards goal values.
//        /// </summary>
//        private MoveRates _moveRates;
//        /// <summary>
//        /// WOrld values of the graphical during PreTick.
//        /// </summary>
//        private TransformProperties _preTickValues;
//        /// <summary>
//        /// Local values of the graphical after PostTick.
//        /// </summary>
//        private TransformProperties? _postTickValues;
//        /// <summary>
//        /// Duration to move over.
//        /// </summary>
//        private float _tickDelta;
//        /// <summary>
//        /// True if a pretick occurred since last postTick.
//        /// </summary>
//        private bool _preTicked;
//        #endregion

//        /// <summary>
//        /// Initializes this smoother; should only be completed once.
//        /// </summary>
//        internal void InitializeOnce(Transform t, float teleportDistance, float tickDelta)
//        {
//            //If current graphicalObject is set then snap it to postTick values.
//            if (_transform != null && _postTickValues.HasValue)
//                _transform.SetLocalProperties(_postTickValues.Value);

//            _tickDelta = tickDelta;
//            _postTickValues = t.GetWorldProperties();
//            _transform = t;
//            _teleportThreshold = teleportDistance;
//        }

//        /// <summary>
//        /// Called every frame.
//        /// </summary>
//        internal void Update()
//        {
//            if (!CanSmooth())
//                return;

//            if (_postTickValues.HasValue)
//                MoveToTarget(_postTickValues.Value);
//        }


//        /// <summary>
//        /// Called when the TimeManager invokes OnPreTick.
//        /// </summary>
//        internal void OnPreTick()
//        {
//            if (!CanSmooth())
//                return;

//            _preTicked = true;
//            _preTickValues = _transform.GetWorldProperties();
//        }

//        /// <summary>
//        /// Called when TimeManager invokes OnPostTick.
//        /// </summary>
//        internal void OnPostTick()
//        {
//            if (!CanSmooth())
//                return;

//            //If preticked then previous transform values are known.
//            if (_preTicked)
//            {
//                SetMoveRates(_preTickValues, _transform);
//                _postTickValues = _transform.GetWorldProperties();
//                _transform.SetWorldProperties(_preTickValues);
//            }
//            _preTicked = false;
//        }

//        /// <summary>
//        /// Returns if prediction can be used on this rigidbody.
//        /// </summary>
//        /// <returns></returns>
//        private bool CanSmooth()
//        {
//            if (_transform == null)
//                return false;

//            return true;
//        }

//        /// <summary>
//        /// Moves transform to target values.
//        /// </summary>
//        
//        private void MoveToTarget(TransformProperties tp)
//        {
//            _moveRates.MoveLocalToTarget(_transform, tp, Time.deltaTime);
//        }

//        /// <summary>
//        /// Sets Position and Rotation move rates to reach Target datas.
//        /// </summary>
//        private void SetMoveRates(TransformProperties prevValues, Transform t)
//        {
//            float duration = _tickDelta;
//            float teleportT = _teleportThreshold;
//            _moveRates = MoveRates.GetWorldMoveRates(prevValues, t, duration, teleportT);
//        }

//    }


//}