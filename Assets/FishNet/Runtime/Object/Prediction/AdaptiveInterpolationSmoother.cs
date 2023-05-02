
using FishNet.Component.Prediction;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace FishNet.Object.Prediction
{
    internal class AdaptiveInterpolationSmoother
    {
#if PREDICTION_V2
        #region Types.
        /// <summary>
        /// Data on a goal to move towards.
        /// </summary>
        private class GoalData
        {
            public bool IsActive;
            /// <summary>
            /// LocalTick this data is for.
            /// </summary>
            public uint LocalTick;
            /// <summary>
            /// Data on how fast to move to transform values.
            /// </summary>
            public RateData Rates = new RateData();
            /// <summary>
            /// Transform values to move towards.
            /// </summary> 
            public TransformProperties Transforms;

            public GoalData() { }
            /// <summary>
            /// Resets values for re-use.
            /// </summary>
            public void Reset()
            {
                LocalTick = 0;
                Transforms.Reset();
                Rates.Reset();
                IsActive = false;
            }

            /// <summary>
            /// Updates values using a GoalData.
            /// </summary>
            public void Update(GoalData gd)
            {
                LocalTick = gd.LocalTick;
                Rates.Update(gd.Rates);
                Transforms.Update(gd.Transforms);
                IsActive = true;
            }

            public void Update(uint localTick, RateData rd, TransformProperties tp)
            {
                LocalTick = localTick;
                Rates = rd;
                Transforms = tp;
                IsActive = true;
            }
        }
        /// <summary>
        /// How fast to move to values.
        /// </summary>
        private class RateData
        {
            /// <summary>
            /// Rate for position after smart calculations.
            /// </summary>
            public float Position;
            /// <summary>
            /// Rate for rotation after smart calculations.
            /// </summary>
            public float Rotation;
            /// <summary>
            /// Number of ticks the rates are calculated for.
            /// If TickSpan is 2 then the rates are calculated under the assumption the transform changed over 2 ticks.
            /// </summary>
            public uint TickSpan;
            /// <summary>
            /// Time remaining until transform is expected to reach it's goal.
            /// </summary>
            internal float TimeRemaining;

            public RateData() { }

            /// <summary>
            /// Resets values for re-use.
            /// </summary>
            public void Reset()
            {
                Position = 0f;
                Rotation = 0f;
                TickSpan = 0;
                TimeRemaining = 0f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Update(RateData rd)
            {
                Update(rd.Position, rd.Rotation, rd.TickSpan, rd.TimeRemaining);
            }

            /// <summary>
            /// Updates rates.
            /// </summary>
            public void Update(float position, float rotation, uint tickSpan, float timeRemaining)
            {
                Position = position;
                Rotation = rotation;
                TickSpan = tickSpan;
                TimeRemaining = timeRemaining;
            }
        }
        #endregion

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
        /// SmoothingData to use.
        /// </summary>
        private AdaptiveInterpolationSmoothingData _smoothingData;
        /// <summary>
        /// Current GoalData being used.
        /// </summary>
        private GoalData _currentGoalData = new GoalData();
        /// <summary>
        /// How far in the past to keep the graphical object.
        /// </summary>
        private uint _interpolation = 4;
        /// <summary>
        /// Sets the interpolation value to use when the owner of this object.
        /// </summary>
        /// <param name="value"></param>
        public void SetInterpolation(uint value) => _interpolation = value;
        /// <summary>
        /// GoalDatas to move towards.
        /// </summary>
        private List<GoalData> _goalDatas = new List<GoalData>();
        /// <summary>
        /// Offsets of the graphical object prior to the NetworkObject transform moving.
        /// </summary>
        private TransformProperties _interpolationOffsets;
        /// <summary>
        /// Cache of GoalDatas to prevent allocations.
        /// </summary>
        private static Stack<GoalData> _goalDataCache = new Stack<GoalData>();
        #endregion

        #region Const.
        /// <summary>
        /// Multiplier to apply to movement speed when buffer is over interpolation.
        /// </summary>
        private const float OVERFLOW_MULTIPLIER = 0.1f;
        /// <summary>
        /// Multiplier to apply to movement speed when buffer is under interpolation.
        /// </summary>
        private const float UNDERFLOW_MULTIPLIER = 0.02f;
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal void Initialize(AdaptiveInterpolationSmoothingData data)
        {
            _smoothingData = data;
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
                Transform t = _smoothingData.NetworkObject.transform;
                _interpolationOffsets.Update(t.position, t.rotation);
            }
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPostTick.
        /// </summary>
        public void OnPostTick()
        {
            if (CanSmooth())
            {
                _smoothingData.GraphicalObject.SetPositionAndRotation(_interpolationOffsets.Position, _interpolationOffsets.Rotation);
                CreateGoalData(_smoothingData.NetworkObject.TimeManager.LocalTick, true);
            }
        }

        public void OnPreReplay(uint tick)
        {
            if (CanSmooth())
            {
                CreateGoalData(tick, false);
            }
        }

        /// <summary>
        /// Called after a reconcile runs a replay.
        /// </summary>
        public void OnPostReplay(uint tick)
        {
            if (CanSmooth())
            {
                CreateGoalData(tick, false);
            }
        }

        /// <summary>
        /// Sets GraphicalObject.
        /// </summary>
        /// <param name="value"></param>
        public void SetGraphicalObject(Transform value)
        {
            _smoothingData.GraphicalObject = value;
            _initializedOffsets = _smoothingData.NetworkObject.transform.GetTransformOffsets(_smoothingData.GraphicalObject);
        }


        /// <summary>
        /// Returns if the graphics can be smoothed.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            if (_interpolation == 0)
                return false;
            NetworkObject nob = _smoothingData.NetworkObject;
            if (nob.IsOwner)
                return false;
            if (nob.IsServerOnly)
                return false;

            return true;
        }

        /// <summary>
        /// Caches a GoalData.
        /// </summary>
        /// <param name="gd"></param>
        private void StoreGoalData(GoalData gd)
        {
            gd.Reset();
            _goalDataCache.Push(gd);
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
        /// Returns if there is any change between two datas.
        /// </summary>
        private bool HasChanged(TransformProperties a, TransformProperties b)
        {
            return (a.Position != b.Position) ||
                (a.Rotation != b.Rotation);
        }

        /// <summary>
        /// Returns if the transform differs from td.
        /// </summary>
        private bool HasChanged(TransformProperties tp)
        {
            Transform t = _smoothingData.NetworkObject.transform;
            bool changed = (tp.Position != t.position) || (tp.Rotation != t.rotation);

            return changed;
        }

        /// <summary>
        /// Sets CurrentGoalData to the next in queue.
        /// </summary>
        private void SetCurrentGoalData(bool afterMove)
        {
            if (_goalDatas.Count == 0)
            {
                _currentGoalData.IsActive = false;
            }
            else
            {
                //Update current to next.
                _currentGoalData.Update(_goalDatas[0]);
                //Store old and remove it.
                StoreGoalData(_goalDatas[0]);
                _goalDatas.RemoveAt(0);
            }
        }

        /// <summary>
        /// Moves to a GoalData. Automatically determins if to use data from server or client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveToTarget(float deltaOverride = -1f)
        {
            /* If the current goal data is not active then
             * try to set a new one. If none are available
             * it will remain inactive. */
            if (!_currentGoalData.IsActive)
            {
                SetCurrentGoalData(false);
                //If still inactive then it could not be updated.
                if (!_currentGoalData.IsActive)
                    return;
            }

            float delta = (deltaOverride != -1f) ? deltaOverride : Time.deltaTime;
            /* Once here it's safe to assume the object will be moving.
             * Any checks which would stop it from moving be it client
             * auth and owner, or server controlled and server, ect,
             * would have already been run. */
            TransformProperties tp = _currentGoalData.Transforms;
            RateData rd = _currentGoalData.Rates;

            int queueCount = _goalDatas.Count;
            /* Begin moving even if interpolation buffer isn't
             * met to provide more real-time interactions but
             * speed up when buffer is too large. This should
             * provide a good balance of accuracy. */

            float multiplier;
            int countOverInterpolation = (queueCount - (int)_interpolation);
            if (countOverInterpolation > 0)
            {
                multiplier = 1f + OVERFLOW_MULTIPLIER;
            }
            else if (countOverInterpolation < 0)
            {
                float value = (UNDERFLOW_MULTIPLIER * Mathf.Abs(countOverInterpolation));
                const float maximum = 0.9f;
                if (value > maximum)
                    value = maximum;
                multiplier = 1f - value;
            }
            else
            {
                multiplier = 1f;
            }

            //Rate to update. Changes per property.
            float rate;
            Transform t = _smoothingData.GraphicalObject;

            //Position.
            if (_smoothingData.SmoothPosition)
            {
                rate = rd.Position;
                rate *= 0.25f;
                multiplier = 1f;
                Vector3 posGoal = tp.Position;
                if (rate == MoveRates.INSTANT_VALUE)
                    t.position = tp.Position;
                else if (rate > 0f)
                    t.position = Vector3.MoveTowards(t.position, posGoal, rate * delta * multiplier);
            }

            //Rotation.
            if (_smoothingData.SmoothRotation)
            {
                rate = rd.Rotation;
                if (rate == MoveRates.INSTANT_VALUE)
                    t.rotation = tp.Rotation;
                else if (rate > 0f)
                    t.rotation = Quaternion.RotateTowards(t.rotation, tp.Rotation, rate * delta);
            }

            //Subtract time remaining for movement to complete.
            if (rd.TimeRemaining > 0f)
            {
                float subtractionAmount = (delta * multiplier);
                float timeRemaining = rd.TimeRemaining - subtractionAmount;
                rd.TimeRemaining = timeRemaining;
            }

            //If movement shoudl be complete.
            if (rd.TimeRemaining <= 0f)
            {
                float leftOver = Mathf.Abs(rd.TimeRemaining);
                //Set to next goal data if available.
                SetCurrentGoalData(true);

                //New data was set.
                if (_currentGoalData.IsActive)
                {
                    if (leftOver > 0f)
                        MoveToTarget(leftOver);
                }
                //No more in buffer, see if can extrapolate.
                else
                {
                    /* Everything should line up when
                     * time remaining is <= 0f but incase it's not,
                     * such as if the user manipulated the grapihc object
                     * somehow, then set goaldata active again to continue
                     * moving it until it lines up with the goal. */
                    if (!GraphicalObjectMatches(tp.Position, tp.Rotation))
                        _currentGoalData.IsActive = true;
                }
            }
        }

        #region Rates.
        /// <summary>
        /// Sets move rates which will occur instantly.
        /// </summary>
        private void SetInstantRates(RateData rd)
        {
            rd.Update(MoveRates.INSTANT_VALUE, MoveRates.INSTANT_VALUE, 1, MoveRates.INSTANT_VALUE);
        }

        /// <summary>
        /// Sets move rates which will occur over time.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCalculatedRates(GoalData prevGoalData, GoalData nextGoalData, Channel channel)
        {
            /* Only update rates if data has changed.
             * When data comes in reliably for eventual consistency
             * it's possible that it will be the same as the last
             * unreliable packet. When this happens no change has occurred
             * and the distance of change woudl also be 0; this prevents
             * the NT from moving. Only need to compare data if channel is reliable. */
            if (channel == Channel.Reliable && HasChanged(prevGoalData.Transforms, nextGoalData.Transforms))
            {
                nextGoalData.Rates.Update(prevGoalData.Rates);
                return;
            }

            uint lastTick = prevGoalData.LocalTick;
            /* How much time has passed between last update and current.
             * If set to 0 then that means the transform has
             * settled. */
            if (lastTick == 0)
                lastTick = (nextGoalData.LocalTick - 1);

            uint tickDifference = (nextGoalData.LocalTick - lastTick);
            float timePassed = (float)_smoothingData.NetworkObject.TimeManager.TicksToTime(tickDifference);
            RateData nextRd = nextGoalData.Rates;

            //Distance between properties.
            float distance;
            //Position.
            Vector3 lastPosition = prevGoalData.Transforms.Position;
            distance = Vector3.Distance(lastPosition, nextGoalData.Transforms.Position);
            //If distance teleports assume rest do.
            if (_smoothingData.TeleportThreshold != MoveRates.UNSET_VALUE && distance >= _smoothingData.TeleportThreshold)
            {
                SetInstantRates(nextRd);
                return;
            }

            //Position distance already calculated.
            float positionRate = (distance / timePassed);
            //Rotation.
            distance = prevGoalData.Transforms.Rotation.Angle(nextGoalData.Transforms.Rotation, true);
            float rotationRate = (distance / timePassed);

            /* If no speed then snap just in case.
             * 0f could be from floating errors. */
            if (positionRate == 0f)
            {
                Debug.Log("Setting position instant.");
                positionRate = MoveRates.INSTANT_VALUE;
            }
            if (rotationRate == 0f)
            {
                Debug.Log("Setting rotation instant.");
                rotationRate = MoveRates.INSTANT_VALUE;
            }

            nextRd.Update(positionRate, rotationRate, tickDifference, timePassed);
            nextRd.Position = 1f;
            Debug.Log("TickDifference " + tickDifference + " > TimePassed " + timePassed);
        }
        #endregion       

        /// <summary>
        /// Creates a new goal data for tick. The result will be placed into the goalDatas queue at it's proper position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateGoalData(uint tick, bool postTick)
        {
            /* It's possible a removed entry would void further
             * logic so remove excess entires first. */

            /* Remove entries which are excessive to the buffer.
             * This could create a starting jitter but it will ensure
             * the buffer does not fill too much. The buffer next should
             * actually get unreasonably high but rather safe than sorry. */
            int maximumBufferAllowance = ((int)_interpolation * 8);
            int removedBufferCount = (_goalDatas.Count - maximumBufferAllowance);
            //If there are some to remove.
            if (removedBufferCount > 0)
            {
                for (int i = 0; i < removedBufferCount; i++)
                    StoreGoalData(_goalDatas[0 + i]);
                _goalDatas.RemoveRange(0, removedBufferCount);
            }

            uint currentGoalDataTick = _currentGoalData.LocalTick;
            //Tick has already been interpolated past, no reason to process it.
            if (tick <= currentGoalDataTick)
                return;

            //GoalData from previous calculation.
            GoalData prevGoalData;
            int datasCount = _goalDatas.Count;
            /* Where to insert next data. This could have value
             * somewhere in the middle of goalDatas if the tick
             * is a replay rather than post tick. */
            int injectionIndex = datasCount + 1;
            //If being added at the end of a tick rather than from replay.
            if (postTick)
            {
                //Becomes true if transform differs from previous data.
                bool changed;

                //If there is no goal data then create one using pretick data.
                if (datasCount == 0)
                {
                    prevGoalData = MakeGoalDataFromPreTickTransform();
                    changed = HasChanged(prevGoalData.Transforms);
                }
                //If there's goal datas grab the last, it will always be the tick before.
                else
                {
                    prevGoalData = _goalDatas[datasCount - 1];
                    /* If the tick is not exactly 1 past the last
                     * then there's gaps in the saved values. This can
                     * occur if the transform went idle and the buffer
                     * hasn't emptied out yet. When this occurs use the
                     * preTick data to calculate differences. */
                    if (tick - prevGoalData.LocalTick != 1)
                    {
                        prevGoalData = MakeGoalDataFromPreTickTransform();
                    }

                    changed = HasChanged(prevGoalData.Transforms);
                }

                //Nothing has changed so no further action is required.
                if (!changed)
                {
                    if (datasCount > 0 && prevGoalData != _goalDatas[datasCount - 1])
                        StoreGoalData(prevGoalData);
                    return;
                }
            }
            //Not post tick so it's from a replay.
            else
            {
                int prevIndex = -1;
                /* If the tick is 1 past current goalData
                 * then it's the next in line for smoothing
                 * from the current.
                 * When this occurs use currentGoalData as
                 * the previous. */
                if (tick == (currentGoalDataTick + 1))
                {
                    prevGoalData = _currentGoalData;
                    injectionIndex = 0;
                }
                //When not the next in line find out where to place data.
                else
                {
                    if (tick > 0)
                        prevGoalData = GetGoalData(tick - 1, out prevIndex);
                    //Cannot find prevGoalData if tick is 0.
                    else
                        prevGoalData = null;
                }

                //If previous goalData was found then inject just past the previous value.
                if (prevIndex != -1)
                    injectionIndex = prevIndex + 1;

                /* Should previous goalData be null then it could not be found.
                 * Create a new previous goal data based on rigidbody state
                 * during pretick. */
                if (prevGoalData == null)
                {
                    //Create a goaldata based on information. If it differs from pretick then throw.
                    GoalData gd = RetrieveGoalData();
                    gd.Transforms.Update(_interpolationOffsets);

                    if (HasChanged(gd.Transforms))
                    {
                        prevGoalData = gd;
                    }
                    else
                    {
                        StoreGoalData(gd);
                        return;
                    }
                }
                /* Previous goal data is not active.
                 * This should not be possible but this
                 * is here as a sanity check anyway. */
                else if (!prevGoalData.IsActive)
                {
                    return;
                }
            }

            //Begin building next goal data.
            GoalData nextGoalData = RetrieveGoalData();
            nextGoalData.LocalTick = tick;
            //Set next transform data.
            TransformProperties nextTp = default;
            nextTp.Update(_smoothingData.NetworkObject.transform);

            /* Reset properties if smoothing is not enabled
             * for them. It's less checks and easier to do it
             * after the nextGoalData is populated. */
            if (!_smoothingData.SmoothPosition)
                nextTp.Position = _initializedOffsets.Position;
            if (!_smoothingData.SmoothRotation)
                nextTp.Rotation = _initializedOffsets.Rotation;

            nextGoalData.Transforms = nextTp;

            //Calculate rates for prev vs next data.
            SetCalculatedRates(prevGoalData, nextGoalData, Channel.Unreliable);
            /* If injectionIndex would place at the end
             * then add. to goalDatas. */
            if (injectionIndex >= _goalDatas.Count)
                _goalDatas.Add(nextGoalData);
            //Otherwise insert into the proper location.
            else
                _goalDatas[injectionIndex].Update(nextGoalData);

            //Makes previous goal data from transforms pretick values.
            GoalData MakeGoalDataFromPreTickTransform()
            {
                GoalData gd = RetrieveGoalData();
                //RigidbodyData contains the data from preTick.
                gd.Transforms.Update(_initializedOffsets);
                //No need to update rates because this is just a starting point reference for interpolation.
                return gd;
            }
        }

        /// <summary>
        /// Returns the GoalData at tick.
        /// </summary>
        /// <returns></returns>
        private GoalData GetGoalData(uint tick, out int index)
        {
            index = -1;
            if (tick == 0)
                return null;

            for (int i = 0; i < _goalDatas.Count; i++)
            {
                if (_goalDatas[i].LocalTick == tick)
                {
                    index = i;
                    return _goalDatas[i];
                }
            }

            //Not found.
            return null;
        }


        /// <summary>
        /// Returns a GoalData from the cache.
        /// </summary>
        /// <returns></returns>
        private GoalData RetrieveGoalData()
        {
            GoalData result = (_goalDataCache.Count > 0) ? _goalDataCache.Pop() : new GoalData();
            result.IsActive = true;
            return result;
        }
#endif
    }
}
