using FishNet.Transporting;
using GameKit.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace FishNet.Component.Prediction
{
    internal class PredictedObjectSpectatorSmoother
    {
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
            public TransformData Transforms = new TransformData();

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

            public void Update(uint localTick, RateData rd, TransformData td)
            {
                LocalTick = localTick;
                Rates = rd;
                Transforms = td;
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
        /// <summary>
        /// Data about where a transform should move towards.
        /// </summary>
        private class TransformData
        {
            /// <summary>
            /// Position of the transform.
            /// </summary>
            public Vector3 Position;
            /// <summary>
            /// Rotation of the transform.
            /// </summary>
            public Quaternion Rotation;

            public void Reset()
            {
                Position = Vector3.zero;
                Rotation = Quaternion.identity;
            }
            /// <summary>
            /// Updates this data.
            /// </summary>
            public void Update(TransformData copy)
            {
                Update(copy.Position, copy.Rotation);
            }
            /// <summary>
            /// Updates this data.
            /// </summary>
            public void Update(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
            /// <summary>
            /// Updates this data.
            /// </summary>
            public void Update(Rigidbody rigidbody)
            {
                Position = rigidbody.transform.position;
                Rotation = rigidbody.transform.rotation;
            }
            /// <summary>
            /// Updates this data.
            /// </summary>
            public void Update(Rigidbody2D rigidbody)
            {
                Position = rigidbody.transform.position;
                Rotation = rigidbody.transform.rotation;
            }
        }
        #endregion

        #region Private.
        /// <summary>
        /// Current GoalData being used.
        /// </summary>
        private GoalData _currentGoalData = new GoalData();
        /// <summary>
        /// Object to smooth.
        /// </summary>
        private Transform _graphicalObject;
        /// <summary>
        /// Sets GraphicalObject.
        /// </summary>
        /// <param name="value"></param>
        public void SetGraphicalObject(Transform value) => _graphicalObject = value;
        /// <summary>
        /// True to move towards position goals.
        /// </summary>
        private bool _smoothPosition;
        /// <summary>
        /// True to move towards rotation goals.
        /// </summary>
        private bool _smoothRotation;
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
        /// Rigidbody to use.
        /// </summary>
        private Rigidbody _rigidbody;
        /// <summary>
        /// Rigidbody2D to use.
        /// </summary>
        private Rigidbody2D _rigidbody2d;
        /// <summary>
        /// Transform state during PreTick.
        /// </summary>
        private TransformData _preTickTransformdata = new TransformData();
        /// <summary>
        /// Type of rigidbody being used.
        /// </summary>
        private RigidbodyType _rigidbodyType;
        /// <summary>
        /// Last tick which a reconcile occured. This is reset at the end of a tick.
        /// </summary>
        private long _reconcileLocalTick = -1;
        /// <summary>
        /// Called when this frame receives OnPreTick.
        /// </summary>
        private bool _preTickReceived;
        /// <summary>
        /// Start position for graphicalObject at the beginning of the tick.
        /// </summary>
        private Vector3 _graphicalStartPosition;
        /// <summary>
        /// Start rotation for graphicalObject at the beginning of the tick.
        /// </summary>
        private Quaternion _graphicalStartRotation;
        /// <summary>
        /// How far a distance change must exceed to teleport the graphical object. -1f indicates teleport is not enabled.
        /// </summary>
        private float _teleportThreshold;
        /// <summary>
        /// PredictedObject which is using this object.
        /// </summary>
        private PredictedObject _predictedObject;
        /// <summary>
        /// Cache of GoalDatas to prevent allocations.
        /// </summary>
        private static Stack<GoalData> _goalDataCache = new Stack<GoalData>();
        /// <summary>
        /// Cached localtick for performance.
        /// </summary>
        private uint _localTick;
        /// <summary>
        /// Number of ticks to ignore when replaying.
        /// </summary>
        private uint _ignoredTicks;
        /// <summary>
        /// Start position of the graphical object in world space.
        /// </summary>
        private Vector3 _startWorldPosition;
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

        public void SetIgnoredTicks(uint value) => _ignoredTicks = value;
        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal void Initialize(PredictedObject po, RigidbodyType rbType, Rigidbody rb, Rigidbody2D rb2d, Transform graphicalObject
            , bool smoothPosition, bool smoothRotation, float teleportThreshold)
        {
            _predictedObject = po;
            _rigidbodyType = rbType;

            _rigidbody = rb;
            _rigidbody2d = rb2d;
            _graphicalObject = graphicalObject;
            _startWorldPosition = _graphicalObject.position;
            _smoothPosition = smoothPosition;
            _smoothRotation = smoothRotation;
            _teleportThreshold = teleportThreshold;
        }

        /// <summary>
        /// <summary>
        /// Called every frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ManualUpdate()
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
                _localTick = _predictedObject.TimeManager.LocalTick;
                if (!_preTickReceived)
                {
                    uint tick = _predictedObject.TimeManager.LocalTick - 1;
                    CreateGoalData(tick, false);
                }
                _preTickReceived = true;

                if (_rigidbodyType == RigidbodyType.Rigidbody)
                    _preTickTransformdata.Update(_rigidbody);
                else
                    _preTickTransformdata.Update(_rigidbody2d);

                _graphicalStartPosition = _graphicalObject.position;
                _graphicalStartRotation = _graphicalObject.rotation;
            }
        }

        /// <summary>
        /// Called when the TimeManager invokes OnPostTick.
        /// </summary>
        public void OnPostTick()
        {
            if (CanSmooth())
            {
                if (!_preTickReceived)
                {
                    /* During test the Z value for applyImmediately is 5.9.
                     * Then increased 1 unit per tick: 6.9, 7.9.
                     * 
                     * When the spectator smoother initializes 5.9 is shown.
                     * Before first starting smoothing the transform needs to be set
                     * back to that.
                     * 
                     * The second issue is the first addition to goal datas seems
                     * to occur at 7.9. This would need to be 6.9 to move from the
                     * proper 5.9 starting point. It's probably because pretick is not received
                     * when OnPostTick is called at the 6.9 position.
                     * 
                     * Have not validated the above yet but that's the most likely situation since
                     * we know this was initialized at 5.9, which means it would be assumed pretick would
                     * call at 6.9. Perhaps the following is happening....
                     * 
                     * - Pretick.
                     * - Client gets spawn+applyImmediately.
                     * - This also initializes this script at 5.9.
                     * - Simulation moves object to 6.9.
                     * - PostTick.
                     * - This script does not run because _preTickReceived is not set yet.
                     * 
                     * - Pretick. Sets _preTickReceived.
                     * - Simulation moves object to 7.9.
                     * - PostTick.
                     * - The first goalData is created for 7.9.
                     * 
                     *  In writing the theory checks out.
                     *  Perhaps the solution could be simple as creating a goal
                     *  during pretick if _preTickReceived is being set for
                     *  the first time. Might need to reduce tick by 1
                     *  when setting goalData for this; not sure yet.
                     */
                    _graphicalObject.SetPositionAndRotation(_startWorldPosition, Quaternion.identity);
                    return;
                }

                _graphicalObject.SetPositionAndRotation(_graphicalStartPosition, _graphicalStartRotation);
                CreateGoalData(_predictedObject.TimeManager.LocalTick, true);
            }
        }

        public void OnPreReplay(uint tick)
        {
            if (!_preTickReceived)
            {
                if (CanSmooth())
                {
                    //if (_localTick - tick < _ignoredTicks)
                    //    return;

                    CreateGoalData(tick, false);
                }
            }
        }

        /// <summary>
        /// Called after a reconcile runs a replay.
        /// </summary>
        public void OnPostReplay(uint tick)
        {
            if (CanSmooth())
            {
                if (_reconcileLocalTick == -1)
                    return;

                CreateGoalData(tick, false);
            }
        }

        /// <summary>
        /// Returns if the graphics can be smoothed.
        /// </summary>
        /// <returns></returns>
        private bool CanSmooth()
        {
            if (_interpolation == 0)
                return false;
            if (_predictedObject.IsPredictingOwner() || _predictedObject.IsServer)
                return false;

            return true;
        }


        /// <summary>
        /// Sets the last tick a reconcile occurred.
        /// </summary>
        /// <param name="value"></param>
        public void SetLocalReconcileTick(long value)
        {
            _reconcileLocalTick = value;
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
        private bool GraphicalObjectMatches(Vector3 localPosition, Quaternion localRotation)
        {
            bool positionMatches = (!_smoothPosition || _graphicalObject.position == localPosition);
            bool rotationMatches = (!_smoothRotation || _graphicalObject.rotation == localRotation);
            return (positionMatches && rotationMatches);
        }

        /// <summary>
        /// Returns if there is any change between two datas.
        /// </summary>
        private bool HasChanged(TransformData a, TransformData b)
        {
            return (a.Position != b.Position) ||
                (a.Rotation != b.Rotation);
        }

        /// <summary>
        /// Returns if the transform differs from td.
        /// </summary>
        private bool HasChanged(TransformData td)
        {
            Transform rigidbodyTransform;

            if (_rigidbodyType == RigidbodyType.Rigidbody)
                rigidbodyTransform = _rigidbody.transform;
            else
                rigidbodyTransform = _rigidbody2d.transform;

            bool changed = (td.Position != rigidbodyTransform.position) || (td.Rotation != rigidbodyTransform.rotation);

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
                //if (!afterMove && _goalDatas.Count < _interpolation)
                //    return;

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
            TransformData td = _currentGoalData.Transforms;
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
                float overflowMultiplier = (!_predictedObject.IsOwner) ? OVERFLOW_MULTIPLIER : (OVERFLOW_MULTIPLIER * 1f);
                multiplier = 1f + overflowMultiplier;
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
            Transform t = _graphicalObject;

            //Position.
            if (_smoothPosition)
            {
                rate = rd.Position;
                Vector3 posGoal = td.Position;
                if (rate == -1f)
                    t.position = td.Position;
                else if (rate > 0f)
                    t.position = Vector3.MoveTowards(t.position, posGoal, rate * delta * multiplier);
            }

            //Rotation.
            if (_smoothRotation)
            {
                rate = rd.Rotation;
                if (rate == -1f)
                    t.rotation = td.Rotation;
                else if (rate > 0f)
                    t.rotation = Quaternion.RotateTowards(t.rotation, td.Rotation, rate * delta);
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
                    if (!GraphicalObjectMatches(td.Position, td.Rotation))
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
            rd.Update(-1f, -1f, 1, -1f);
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
            TransformData nextTd = nextGoalData.Transforms;
            if (channel == Channel.Reliable && HasChanged(prevGoalData.Transforms, nextTd))
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
            float timePassed = (float)_predictedObject.TimeManager.TicksToTime(tickDifference);
            RateData nextRd = nextGoalData.Rates;

            //Distance between properties.
            float distance;
            //Position.
            Vector3 lastPosition = prevGoalData.Transforms.Position;
            distance = Vector3.Distance(lastPosition, nextTd.Position);
            //If distance teleports assume rest do.
            if (_teleportThreshold >= 0f && distance >= _teleportThreshold)
            {
                SetInstantRates(nextRd);
                return;
            }

            //Position distance already calculated.
            float positionRate = (distance / timePassed);
            //Rotation.
            distance = prevGoalData.Transforms.Rotation.Angle(nextTd.Rotation, true);
            float rotationRate = (distance / timePassed);

            /* If no speed then snap just in case.
             * 0f could be from floating errors. */
            if (positionRate == 0f)
                positionRate = -1f;
            if (rotationRate == 0f)
                rotationRate = -1f;

            nextRd.Update(positionRate, rotationRate, tickDifference, timePassed);
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
                        prevGoalData = MakeGoalDataFromPreTickTransform();

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
                    gd.Transforms.Update(_preTickTransformdata);

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
            TransformData nextTd = nextGoalData.Transforms;
            if (_rigidbodyType == RigidbodyType.Rigidbody)
                nextTd.Update(_rigidbody);
            else
                nextTd.Update(_rigidbody2d);

            /* Reset properties if smoothing is not enabled
             * for them. It's less checks and easier to do it
             * after the nextGoalData is populated. */
            if (!_smoothPosition)
                nextTd.Position = _graphicalStartPosition;
            if (!_smoothRotation)
                nextTd.Rotation = _graphicalStartRotation;

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
                gd.Transforms.Update(_preTickTransformdata);
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
    }
}
