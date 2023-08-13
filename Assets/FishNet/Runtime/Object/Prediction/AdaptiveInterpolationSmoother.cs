using GameKit.Utilities;
using FishNet.Utility.Extension;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object.Prediction
{
    internal class AdaptiveInterpolationSmoother
    {
#if PREDICTION_V2

        #region Private.
        private TransformProperties _graphicalInitializedLocalOffsets;
        /// <summary>
        /// Offsets of the graphical object during PreTick. This could also be the offset PreReplay.
        /// </summary>
        private TransformProperties _graphicalPretickWorldValues;
        /// <summary>
        /// Offsets of the root transform before simulating. This could be PreTick, or PreReplay.
        /// </summary>
        private TransformProperties _rootPreSimulateValues;
        /// <summary>
        /// SmoothingData to use.
        /// </summary>
        private AdaptiveInterpolationSmoothingData _smoothingData;
        /// <summary>
        /// Last ping value when it was checked.
        /// </summary>
        private long _lastPing = long.MinValue;
        /// <summary>
        /// Cached NetworkObject reference in SmoothingData for performance.
        /// </summary>
        private NetworkObject _networkObject;

        private float _positionRate;
        private float _rotationRate;
        private float _scaleRate;
        #endregion

        public AdaptiveInterpolationSmoother()
        {
            /* Initialize for up to 50
             * goal datas. Anything beyond that
             * is unreasonable. */
            //_goalDatas.Initialize(50);
        }

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal void Initialize(AdaptiveInterpolationSmoothingData data)
        {
            _smoothingData = data;
            _networkObject = data.NetworkObject;
            SetGraphicalObject(data.GraphicalObject);
        }

        /// <summary>
        /// <summary>
        /// Called every frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            if (CanSmooth())
                MoveToTarget();
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPreTick.
        /// </summary>
        public void OnPreTick()
        {
            if (CanSmooth())
            {
                UpdatePingInterpolation();
                _graphicalPretickWorldValues.Update(_smoothingData.GraphicalObject);
                _rootPreSimulateValues.Update(_networkObject.transform);
            }
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPostTick.
        /// </summary>
        public void OnPostTick()
        {
            if (CanSmooth())
            {
                Transform graphicalTransform = _smoothingData.GraphicalObject;
                //Reset to pretick position.
                graphicalTransform.position = _graphicalPretickWorldValues.Position;
                graphicalTransform.rotation = _graphicalPretickWorldValues.Rotation;
                graphicalTransform.localScale = _graphicalPretickWorldValues.LocalScale;

                float tickDelta = (float)_networkObject.TimeManager.TickDelta;
                float lastPingWithoutTickDelta = Mathf.Max(0f, (_lastPing - (tickDelta * 1000f)));
                //Default interpolation.
                byte interpolation;
                if (_networkObject.IsServer)
                {
                    interpolation = 1;
                }
                else
                {
                    interpolation = 2;
                    //Add on 1 interpolation for every Xms ping. Cap at Y extra.
                    byte extraInterpolation = (byte)Mathf.Min(2, Mathf.FloorToInt(lastPingWithoutTickDelta / 200));
                    interpolation += extraInterpolation;
                }
                float timePassed = (float)_networkObject.TimeManager.TickDelta * (float)interpolation;

                float distance;
                float safetyRate = 99999f;

                /* Position. */
                distance = Vector3.Distance(graphicalTransform.localPosition, _graphicalInitializedLocalOffsets.Position);
                _positionRate = (distance / timePassed);
                if (_positionRate <= 0.0001f)
                    _positionRate = safetyRate;

                /* Rotation. */
                distance = graphicalTransform.localRotation.Angle(_graphicalInitializedLocalOffsets.Rotation, true);
                _rotationRate = (distance / timePassed);
                if (_rotationRate <= 0.25f)
                    _rotationRate = safetyRate;

                /* Scale. */
                distance = Vector3.Distance(graphicalTransform.localScale, _graphicalInitializedLocalOffsets.LocalScale);
                _scaleRate = (distance / timePassed);
                if (_scaleRate <= 0.0001f)
                    _scaleRate = safetyRate;
            }
        }

        /// <summary>
        /// Called before a reconcile runs a replay.
        /// </summary>
        public void OnPreReplicateReplay(uint clientTick, uint serverTick) { }
        /// <summary>
        /// Called after a reconcile runs a replay.
        /// </summary>
        public void OnPostReplicateReplay(uint clientTick, uint serverTick) { }

        /// <summary>
        /// Updates interpolation values based on ping.
        /// </summary>
        private void UpdatePingInterpolation()
        {
            /* Only update if ping has changed considerably.
             * This will prevent random lag spikes from throwing
             * off the interpolation. */
            long ping = _networkObject.TimeManager.RoundTripTime;
            ulong difference = (ulong)Mathf.Abs(ping - _lastPing);
            if (difference > 25)
                _lastPing = ping;
        }


        /// <summary>
        /// Sets GraphicalObject.
        /// </summary>
        /// <param name="value"></param>
        public void SetGraphicalObject(Transform value)
        {
            _smoothingData.GraphicalObject = value;
            _graphicalInitializedLocalOffsets.Update(value.localPosition, value.localRotation, value.localScale);
        }

        /// <summary>
        /// Returns if the graphics can be smoothed.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            if (_networkObject.IsOwner)
                return false;
            if (_networkObject.IsServerOnly)
                return false;

            return true;
        }

        /// <summary>
        /// Moves to a GoalData. Automatically determins if to use data from server or client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveToTarget()
        {
            float delta = Time.deltaTime;

            Transform graphicalTransform = _smoothingData.GraphicalObject;

            graphicalTransform.localPosition = Vector3.MoveTowards(graphicalTransform.localPosition, _graphicalInitializedLocalOffsets.Position, _positionRate * delta);
            graphicalTransform.localRotation = Quaternion.RotateTowards(graphicalTransform.localRotation, _graphicalInitializedLocalOffsets.Rotation, _rotationRate * delta);
            graphicalTransform.localScale = Vector3.MoveTowards(graphicalTransform.localScale, _graphicalInitializedLocalOffsets.LocalScale, _scaleRate * delta);
        }



#endif
    }
}
