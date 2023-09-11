using GameKit.Utilities;
using FishNet.Utility.Extension;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Collections.Generic;
using FishNet.Managing.Timing;

namespace FishNet.Object.Prediction
{
    internal class AdaptiveInterpolationSmoother
    {
#if PREDICTION_V2

        #region Types.
        /// <summary>
        /// Data on a goal to move towards.
        /// </summary>
        private class GoalData : IResettable
        {
            /// <summary>
            /// True if this GoalData is valid.
            /// </summary>
            public bool IsValid;
            /// <summary>
            /// Tick of the data this GoalData is for.
            /// </summary>
            public uint DataTick;
            /// <summary>
            /// Transform values to move towards.
            /// </summary> 
            public TransformPropertiesCls TransformProperties = new TransformPropertiesCls();
            /// <summary>
            /// Time remaining to move towards goal.
            /// </summary>
            public float TimeRemaining;

            public GoalData() { }

            public void InitializeState() { }

            public void ResetState()
            {
                DataTick = 0;
                TimeRemaining = 0f;
                TransformProperties.ResetState();
                IsValid = false;
            }

            /// <summary>
            /// Updates values using a GoalData.
            /// </summary>
            public void Update(GoalData gd)
            {
                DataTick = gd.DataTick;
                TransformProperties.Update(gd.TransformProperties);
                TimeRemaining = gd.TimeRemaining;
                IsValid = true;
            }

            public void Update(uint dataTick, TransformPropertiesCls tp, float timeRemaining)
            {
                DataTick = dataTick;
                TransformProperties = tp;
                TimeRemaining = timeRemaining;
                IsValid = true;
            }
        }
        #endregion

        #region Private.
        /// <summary>
        /// Offsets of the root object during PreTick or PreReplicateReplay.
        /// </summary>
        private TransformProperties _rootPreSimulateWorldValues;
        /// <summary>
        /// Offsets of the graphical object during PreTick or PreReplicateReplay.
        /// </summary>
        private TransformProperties _graphicalPreSimulateWorldValues;
        /// <summary>
        /// SmoothingData to use.
        /// </summary>
        private AdaptiveInterpolationSmoothingData _smoothingData;
        /// <summary>
        /// Current interpolation value. This changes based on ping and settings.
        /// </summary>
        private float _currentInterpolation = 4;
        /// <summary>
        /// Current GoalData being used.
        /// </summary>
        private GoalData _currentGoalData = new GoalData();
        /// <summary>
        /// MoveRates for currentGoalData.
        /// </summary>
        private MoveRates _currentMoveRates;
        /// <summary>
        /// GoalDatas to move towards.
        /// </summary>
        //private RingBuffer<GoalData> _goalDatas = new RingBuffer<GoalData>();
        private List<GoalData> _goalDatas = new List<GoalData>();
        /// <summary>
        /// Cached NetworkObject reference in SmoothingData for performance.
        /// </summary>
        private NetworkObject _networkObject;
        /// <summary>
        /// Cached tickDelta on the TimeManager.
        /// </summary>
        private float _tickDelta;
        /// <summary>
        /// Multiplier to apply towards movements. This is used to speed up and slow down buffer as needed.
        /// </summary>
        private float _rateMultiplier = 1f;
        /// <summary>
        /// Target interpolation when collision is exited. This changes based on ping and settings.
        /// </summary>
        private uint _targetNormalInterpolation;
        /// <summary>
        /// Target interpolation when collision is entered. This changes based on ping and settings.
        /// </summary>
        private uint _targetCollisionInterpolation;
        /// <summary>
        /// Last ping value when it was checked.
        /// </summary>
        private long _lastPing = long.MinValue;
        #endregion

        #region Const.
        /// <summary>
        /// Multiplier to apply to movement speed when buffer is over interpolation.
        /// </summary>
        private const float OVERFLOW_MULTIPLIER = 10f;
        /// <summary>
        /// Multiplier to apply to movement speed when buffer is under interpolation.
        /// </summary>
        private const float UNDERFLOW_MULTIPLIER = 1f;
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
            _tickDelta = (float)_networkObject.TimeManager.TickDelta;
            SetGraphicalObject(data.GraphicalObject);
            UpdatePingInterpolation(true);
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
                UpdatePingInterpolation(false);
                UpdateCurrentInterpolation();
                _graphicalPreSimulateWorldValues = _smoothingData.GraphicalObject.GetWorldProperties();
                _rootPreSimulateWorldValues.Update(_networkObject.transform);
            }
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPostTick.
        /// </summary>
        public void OnPostTick()
        {
            if (CanSmooth())
            {
                //Reset graphics to start graphicals transforms properties.
                _smoothingData.GraphicalObject.SetPositionAndRotation(_graphicalPreSimulateWorldValues.Position, _graphicalPreSimulateWorldValues.Rotation);
                //Create a goal data for new transform position.
                uint tick = _networkObject.LastUnorderedReplicateTick;
                CreatePostSimulateGoalData(tick, true);
            }
        }

        /// <summary>
        /// Called before a reconcile runs a replay.
        /// </summary>
        public void OnPreReplicateReplay(uint clientTick, uint serverTick)
        {
            //Update the last post simulate data.
            if (CanSmooth())
                _rootPreSimulateWorldValues.Update(_networkObject.transform);
        }

        /// <summary>
        /// Called after a reconcile runs a replay.
        /// </summary>
        public void OnPostReplicateReplay(uint clientTick, uint serverTick)
        {
            if (CanSmooth())
            {
                /* Create new goal data from the replay.
				 * This must be done every replay. If a desync
				 * did occur then the goaldatas would be different
				 * from what they were previously. */
                uint tick = _networkObject.LastUnorderedReplicateTick;
                CreatePostSimulateGoalData(tick, false);
            }
        }

        public void OnPostReconcile(uint clientReconcileTick, uint serverReconcileTick)
        {
            if (CanSmooth())
            {
                _rootPreSimulateWorldValues.Update(_networkObject.transform);
            }
        }

        /// <summary>
        /// Sets GraphicalObject.
        /// </summary>
        /// <param name="value"></param>
        public void SetGraphicalObject(Transform value)
        {
            _smoothingData.GraphicalObject = value;
            _graphicalPreSimulateWorldValues.Update(value);
        }

        /// <summary>
        /// Returns if the graphics can be smoothed.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            if (_networkObject.IsOwner)
                return false;
            if (_networkObject.IsServerOnly || _networkObject.IsHost)
                return false;

            return true;
        }

        /// <summary>
        /// Moves current interpolation to target interpolation.
        /// </summary>
        private void UpdateCurrentInterpolation()
        {
            AdaptiveInterpolationSmoothingData data = _smoothingData;
            bool colliding = _networkObject.CollidingWithLocalClient();
            //Decrease interpolation if colliding.
            if (colliding)
                _currentInterpolation -= data.CollisionStep;
            else
                _currentInterpolation += data.NormalStep;

            //Clamp current interpolation to potential values.
            _currentInterpolation = Mathf.Clamp(_currentInterpolation, _targetCollisionInterpolation, _targetNormalInterpolation);
        }

        /// <summary>
        /// Updates interpolation values based on ping.
        /// </summary>
        private void UpdatePingInterpolation(bool setImmediately)
        {
            /* Only update if ping has changed considerably.
             * This will prevent random lag spikes from throwing
             * off the interpolation. */
            long ping = _networkObject.TimeManager.RoundTripTime;
            ulong difference = (ulong)Mathf.Abs(ping - _lastPing);
            _lastPing = ping;
            //Allow update if ping jump is large enough.
            if (setImmediately || difference > 25)
                SetTargetSmoothing(ping, setImmediately);
        }

        /// <summary>
        /// Sets target smoothing values.
        /// </summary>
        /// <param name="setImmediately">True to set current values to targets immediately.</param>
        private void SetTargetSmoothing(long ping, bool setImmediately)
        {
            AdaptiveInterpolationSmoothingData data = _smoothingData;
            TimeManager tm = _networkObject.TimeManager;
            double interpolationTime = (ping / 1000d) * data.NormalPercent;
            _targetNormalInterpolation = AdaptiveInterpolationSmoothingData.BASE_NORMAL_INTERPOLATION + tm.TimeToTicks(interpolationTime, TickRounding.RoundUp);
            double collisionInterpolationTime = (ping / 1000d) * data.CollisionPercent;
            _targetCollisionInterpolation = AdaptiveInterpolationSmoothingData.BASE_COLLISION_INTERPOLATION + tm.TimeToTicks(collisionInterpolationTime, TickRounding.RoundUp);

            //If to apply values to targets immediately.
            if (setImmediately)
                _currentInterpolation = (_networkObject.CollidingWithLocalClient()) ? _targetCollisionInterpolation : _targetNormalInterpolation;
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
        /// Sets CurrentGoalData to the next in queue. Returns if was set successfully.
        /// </summary>
        private bool SetCurrentGoalData()
        {
            if (_goalDatas.Count == 0)
            {
                _currentGoalData.IsValid = false;
                return false;
            }
            else
            {
                /* Previous will always be current since
                 * we are getting next in queue. We
                 * later check if current is valid to determine
                 * if instant rates should be set or normal rates.
                 * If current is not valie then instant rates are set
                 * to teleport graphics to their starting position, and
                 * future sets will have a valid current. */
                GoalData prev = _currentGoalData;
                //Set next and make valid.
                GoalData next = _goalDatas[0];
                //Remove from goalDatas.
                _goalDatas.RemoveAt(0);

                if (prev != null && prev.IsValid)
                    SetCurrentMoveRates(prev.DataTick, next.DataTick, prev.TransformProperties, next.TransformProperties);
                else
                    _currentMoveRates.SetInstantRates();

                //Store previous.
                if (prev != null)
                    ResettableObjectCaches<GoalData>.Store(prev);
                //Assign new current.
                _currentGoalData = next;
                return true;
            }
        }

        /// <summary>
        /// Moves to a GoalData. Automatically determins if to use data from server or client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveToTarget(float deltaOverride = -1f)
        {
            /* If the current goal data is not valid then
			 * try to set a new one. If none are available
			 * it will remain inactive. */
            if (!_currentGoalData.IsValid)
            {
                if (!SetCurrentGoalData())
                    return;
            }

            GoalData currentGd = _currentGoalData;

            float delta = (deltaOverride != -1f) ? deltaOverride : Time.deltaTime;
            /* Once here it's safe to assume the object will be moving.
			 * Any checks which would stop it from moving be it client
			 * auth and owner, or server controlled and server, ect,
			 * would have already been run. */
            TransformPropertiesCls td = currentGd.TransformProperties;
            MoveRates mr = _currentMoveRates;

            //How much multiplier should change in either direction over a second.
            float multiplierChangeRate = 0.05f;

            int queueCount = _goalDatas.Count;
            /* Begin moving even if interpolation buffer isn't
			 * met to provide more real-time interactions but
			 * speed up when buffer is too large. This should
			 * provide a good balance of accuracy. */
            int countOverInterpolation = (queueCount - (int)_currentInterpolation);
            //Really high over interpolation, snap to datas.
            if (countOverInterpolation > (_currentInterpolation * 30))
            {
                //debugPrint = $"TELEPORT {countOverInterpolation}. Teleporting.";
                mr.SetInstantRates();
                //Setting to -1 will force it to go negative, which will clear next goal data for teleport as well.
                currentGd.TimeRemaining = -1f;
            }
            else if (countOverInterpolation > 0)
            {
                //debugPrint = $"OverInterpolation {countOverInterpolation}. Increasing.";
                _rateMultiplier += (multiplierChangeRate * delta);
            }
            else if (countOverInterpolation < 0)
            {
                //debugPrint = $"UnderInterpolation {countOverInterpolation}. Slowing.";
                _rateMultiplier -= (multiplierChangeRate * delta);
            }
            else
            {
                _rateMultiplier = Mathf.MoveTowards(_rateMultiplier, 1f, (multiplierChangeRate * delta));
            }

            //Clamp multiplier.
            const float maximumMultiplier = 1.1f;
            const float minimumMultiplier = 0.95f;
            _rateMultiplier = Mathf.Clamp(_rateMultiplier, minimumMultiplier, maximumMultiplier);
            //Apply multiplier to delta.
            delta *= _rateMultiplier;

            //Rate to update. Changes per property.
            float rate;
            Transform t = _smoothingData.GraphicalObject;

            //Position.
            if (_smoothingData.SmoothPosition)
            {
                rate = mr.Position;
                Vector3 posGoal = td.Position;
                if (rate == MoveRatesCls.INSTANT_VALUE)
                    t.position = td.Position;
                else if (rate > 0f)
                    t.position = Vector3.MoveTowards(t.position, posGoal, rate * delta);
            }

            //Rotation.
            if (_smoothingData.SmoothRotation)
            {
                rate = mr.Rotation;
                if (rate == MoveRatesCls.INSTANT_VALUE)
                    t.rotation = td.Rotation;
                else if (rate > 0f)
                    t.rotation = Quaternion.RotateTowards(t.rotation, td.Rotation, rate * delta);
            }

            if (currentGd.TimeRemaining > 0f)
                currentGd.TimeRemaining -= delta;

            if (currentGd.TimeRemaining <= 0f)
            {
                bool graphicsMatch = GraphicalObjectMatches(td.Position, td.Rotation);
                if (graphicsMatch)
                {
                    float leftOver = Mathf.Abs(currentGd.TimeRemaining);
                    if (SetCurrentGoalData())
                        MoveToTarget(leftOver);
                }
            }
        }

        #region Rates.
        /* If reconciles do not occur frequently there is a very good chance the graphical object will
         * teleport around. This is not due to any calculation error but rather because inputs are
         * being wrongly predicted for so long the correction is harsh. Shorter reconciles will result
         * in quicker corrections. */
        
        /// <summary>
        /// Sets move rates which will occur over time.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCurrentMoveRates(uint prevTick, uint tick, TransformPropertiesCls prevTp, TransformPropertiesCls nextTp)
        {
            long lngTicksPassed = (tick - prevTick);
            //Should not happen.
            if (lngTicksPassed <= 0)
            {
                _networkObject.NetworkManager.LogError($"Ticks passed returned negative as {lngTicksPassed}. Instant rates are being set.");
                _currentMoveRates.SetInstantRates();
                return;
            }
            //More than 1 tick, also unusual.
            else if (lngTicksPassed > 1)
            {
                //_networkObject.NetworkManager.LogError($"Ticks passed are not equal to 1, passed value is {lngTicksPassed}");
                lngTicksPassed = 1;
            }


            uint ticksPassed = (uint)lngTicksPassed;
            float delta = _tickDelta;

            float distance;
            float rate;
            const float v3Tolerance = 0.0001f;
            const float qTolerance = 0.2f;

            //Position.
            rate = prevTp.Position.GetRate(nextTp.Position, delta, out distance, ticksPassed);
            //If distance teleports assume rest do.
            if (_smoothingData.TeleportThreshold != MoveRates.UNSET_VALUE && distance >= _smoothingData.TeleportThreshold)
            {
                _currentMoveRates.SetInstantRates();
                return;
            }
            float positionRate = rate.SetIfUnderTolerance(v3Tolerance, MoveRates.INSTANT_VALUE);

            //Rotation.
            rate = prevTp.Rotation.GetRate(nextTp.Rotation, delta, out _, ticksPassed);
            float rotationRate = rate.SetIfUnderTolerance(qTolerance, MoveRates.INSTANT_VALUE);

            _currentMoveRates.Update(positionRate, rotationRate, MoveRates.INSTANT_VALUE);
        }

        #endregion

        /// <summary>
        /// Removes GoalDatas which make the queue excessive.
        /// This could cause teleportation but would rarely occur, only potentially during sever network issues.
        /// </summary>
        private void RemoveExcessiveGoalDatas()
        {
            /* Remove entries which are excessive to the buffer.
            * This could create a starting jitter but it will ensure
            * the buffer does not fill too much. The buffer next sho0..uld
            * actually get unreasonably high but rather safe than sorry. */
            int maximumBufferAllowance = ((int)_currentInterpolation * 10);
            int removedBufferCount = (_goalDatas.Count - maximumBufferAllowance);
            //If there are some to remove.
            if (removedBufferCount > 0)
            {
                for (int i = 0; i < removedBufferCount; i++)
                    ResettableObjectCaches<GoalData>.Store(_goalDatas[0 + i]);
                //_goalDatas.RemoveRange(true, removedBufferCount);
                _goalDatas.RemoveRange(0, removedBufferCount);
            }
        }

        /// <summary>
        /// Creates a GoalData after a simulate.
        /// </summary>
        /// <param name="postTick">True if being created for OnPostTick.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreatePostSimulateGoalData(uint tick, bool postTick)
        {
            RemoveExcessiveGoalDatas();

            int dataIndex = -1;
            bool useUpdate = false;

            /* Post ticks always go on the end.
             * The tick will be wrong for the post tick, so set it
             * to the last entry tick + 1. */

            int datasCount = _goalDatas.Count;


            if (postTick)
            {
                if (datasCount > 0)
                    tick = _goalDatas[datasCount - 1].DataTick + 1;
                else
                    tick = _currentGoalData.DataTick + 1;

                dataIndex = datasCount;
            }
            else
            {
                /* There is no need to create a goaldata
                 * if the tick is previous to currentGoalData.
                 * This would indicate the graphics have already
                 * moved past tick. */
                if (tick < _currentGoalData.DataTick)
                    return;
                //If current tick then let current play out and do nothing.
                else if (tick == _currentGoalData.DataTick)
                    return;

                uint prevArrTick = 0;
                for (int i = 0; i < datasCount; i++)
                {
                    uint arrTick = _goalDatas[i].DataTick;
                    if (tick == arrTick)
                    {
                        dataIndex = i;
                        useUpdate = true;
                        break;
                    }
                    else if (i > 0 && tick > prevArrTick && tick < arrTick)
                    {
                        dataIndex = i;
                        break;
                    }

                    prevArrTick = arrTick;
                }

                //DataIndex was not found which means it's before the first goaldata or after the last.
                if (dataIndex == -1)
                {
                    //Insert at beginning.
                    if (datasCount > 0 && tick < _goalDatas[0].DataTick)
                        dataIndex = 0;
                    //Insert at end.
                    else
                        dataIndex = datasCount;
                }
            }

            Transform rootT = _networkObject.transform;
            //Begin building next goal data.
            GoalData nextGd = ResettableObjectCaches<GoalData>.Retrieve();
            nextGd.DataTick = tick;
            nextGd.TimeRemaining = _tickDelta;
            nextGd.IsValid = true;
            //Set next transform data.
            TransformPropertiesCls nextTp = nextGd.TransformProperties;
            //Position.
            if (!_smoothingData.SmoothPosition)
                nextTp.Position = _graphicalPreSimulateWorldValues.Position;
            else
                nextTp.Position = rootT.position;
            //ROtation.
            if (!_smoothingData.SmoothRotation)
                nextTp.Rotation = _graphicalPreSimulateWorldValues.Rotation;
            else
                nextTp.Rotation = rootT.rotation;

            //Vector3 lineDist = new Vector3(0f, 3f, 0f);
            //if (!postTick)
            //    Debug.DrawLine(rootT.position + lineDist, rootT.position, Color.red, 2f);
            //else
            //    Debug.DrawLine(rootT.position + lineDist + new Vector3(1f, 0f, 0f), rootT.position, Color.blue, 2f);

            if (useUpdate)
                _goalDatas[dataIndex].Update(nextGd);
            else
                _goalDatas.Insert(dataIndex, nextGd);


        }
#endif
    }
}
