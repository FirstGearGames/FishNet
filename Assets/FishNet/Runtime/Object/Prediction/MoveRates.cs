using System.Runtime.CompilerServices;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Jobs;
using UnityEngine.Scripting;

namespace FishNet.Object.Prediction
{
    /// <summary>
    /// Used to make calculations and perform actions in moving transforms over time.
    /// </summary>
    [Preserve]
    public struct MoveRates
    {
        #region Private Profiler Markers
        
        private static readonly ProfilerMarker _pm_GetMoveRatesFull = new ProfilerMarker("MoveRates.GetMoveRates(float3, float3, quaternion, quaternion, float3, float3, float, float)");
        private static readonly ProfilerMarker _pm_GetMoveRatesVec = new ProfilerMarker("MoveRates.GetMoveRates(float3, float3, float, float)");
        private static readonly ProfilerMarker _pm_Move = new ProfilerMarker("MoveRates.Move(TransformAccess, TransformPropertiesFlag, float3, float, quaternion, float, float3, float, float, bool)");
        
        #endregion
        
        /// <summary>
        /// Rate at which to move Position.
        /// </summary>
        public float Position;
        /// <summary>
        /// Rate at which to move Rotation.
        /// </summary>
        public float Rotation;
        /// <summary>
        /// Rate at which to move Scale.
        /// </summary>
        public float Scale;
        /// <summary>
        /// Time remaining until the move is complete.
        /// </summary>
        public float TimeRemaining;
        /// <summary>
        /// Value used when data is not set.
        /// </summary>
        public const float UNSET_VALUE = float.NegativeInfinity;
        /// <summary>
        /// Value used when move rate should be instant.
        /// </summary>
        public const float INSTANT_VALUE = float.PositiveInfinity;
        /// <summary>
        /// True if any data is set. Once set, this will remain true until ResetState is called.
        /// </summary>
        public bool IsValid { get; private set; }

        public MoveRates(float value) : this()
        {
            Position = value;
            Rotation = value;
            Scale = value;

            IsValid = true;
        }

        public MoveRates(float position, float rotation) : this()
        {
            Position = position;
            Rotation = rotation;
            Scale = INSTANT_VALUE;

            IsValid = true;
        }

        public MoveRates(float position, float rotation, float scale) : this()
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;

            IsValid = true;
        }

        public MoveRates(float position, float rotation, float scale, float timeRemaining)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            TimeRemaining = timeRemaining;

            IsValid = true;
        }

        /// <summary>
        /// True if a positional move rate is set.
        /// </summary>
        public bool IsPositionSet => Position != UNSET_VALUE;
        /// <summary>
        /// True if rotation move rate is set.
        /// </summary>
        public bool IsRotationSet => Rotation != UNSET_VALUE;
        /// <summary>
        /// True if a scale move rate is set.
        /// </summary>
        public bool IsScaleSet => Scale != UNSET_VALUE;
        /// <summary>
        /// True if position move rate should be instant.
        /// </summary>
        public bool IsPositionInstantValue => Position == INSTANT_VALUE;
        /// <summary>
        /// True if rotation move rate should be instant.
        /// </summary>
        public bool IsRotationInstantValue => Rotation == INSTANT_VALUE;
        /// <summary>
        /// True if scale move rate should be instant.
        /// </summary>
        public bool IsScaleInstantValue => Scale == INSTANT_VALUE;

        /// <summary>
        /// Sets all rates to instant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInstantRates() => Update(INSTANT_VALUE);

        /// <summary>
        /// Sets all rates to the same value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float value) => Update(value, value, value);

        /// <summary>
        /// Sets rates for each property.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float position, float rotation, float scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;

            IsValid = true;
        }

        /// <summary>
        /// Sets rates for each property.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float position, float rotation, float scale, float timeRemaining)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            TimeRemaining = timeRemaining;

            IsValid = true;
        }

        /// <summary>
        /// Updates to new values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(MoveRates moveRates) => Update(moveRates.Position, moveRates.Rotation, moveRates.Scale, moveRates.TimeRemaining);

        /// <summary>
        /// Updates to new values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(MoveRatesCls moveRates) => Update(moveRates.Position, moveRates.Rotation, moveRates.Scale, moveRates.TimeRemaining);

        /// <summary>
        /// Resets to unset values.
        /// </summary>
        public void ResetState()
        {
            Update(UNSET_VALUE, UNSET_VALUE, UNSET_VALUE, timeRemaining: 0f);

            IsValid = false;
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetWorldMoveRates(Transform from, Transform to, float duration, float teleportThreshold)
        {
            from.GetPositionAndRotation(out var fromPos, out var fromRot);
            to.GetPositionAndRotation(out var toPos, out var toRot);
            return GetMoveRates(fromPos, toPos, fromRot, toRot, from.localScale, to.localScale, duration, teleportThreshold);
        }
        
        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetWorldMoveRates(TransformAccess from, TransformAccess to, float duration, float teleportThreshold)
        {
            from.GetPositionAndRotation(out var fromPos, out var fromRot);
            to.GetPositionAndRotation(out var toPos, out var toRot);
            return GetMoveRates(fromPos, toPos, fromRot, toRot, from.localScale, to.localScale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetLocalMoveRates(Transform from, Transform to, float duration, float teleportThreshold)
        {
            from.GetPositionAndRotation(out var fromPos, out var fromRot);
            to.GetPositionAndRotation(out var toPos, out var toRot);
            return GetMoveRates(fromPos, toPos, fromRot, toRot, from.localScale, to.localScale, duration, teleportThreshold);
        }
        
        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetLocalMoveRates(TransformAccess from, TransformAccess to, float duration, float teleportThreshold)
        {
            from.GetPositionAndRotation(out var fromPos, out var fromRot);
            to.GetPositionAndRotation(out var toPos, out var toRot);
            return GetMoveRates(fromPos, toPos, fromRot, toRot, from.localScale, to.localScale, duration, teleportThreshold);
        }
        
        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetWorldMoveRates(TransformProperties prevValues, Transform t, float duration, float teleportThreshold)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            return GetMoveRates(prevValues.Position, pos, prevValues.Rotation, rot, prevValues.Scale, t.localScale, duration, teleportThreshold);
        }
        
        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetWorldMoveRates(TransformProperties prevValues, TransformAccess t, float duration, float teleportThreshold)
        {
            t.GetPositionAndRotation(out var pos, out var rot);
            return GetMoveRates(prevValues.Position, pos, prevValues.Rotation, rot, prevValues.Scale, t.localScale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetLocalMoveRates(TransformProperties prevValues, Transform t, float duration, float teleportThreshold)
        {
            t.GetLocalPositionAndRotation(out var pos, out var rot);
            return GetMoveRates(prevValues.Position, pos, prevValues.Rotation, rot, prevValues.Scale, t.localScale, duration, teleportThreshold);
        }
        
        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetLocalMoveRates(TransformProperties prevValues, TransformAccess t, float duration, float teleportThreshold)
        {
            t.GetCorrectLocalPositionAndRotation(out var pos, out var rot);
            return GetMoveRates(prevValues.Position, pos, prevValues.Rotation, rot, prevValues.Scale, t.localScale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetMoveRates(TransformProperties prevValues, TransformProperties nextValues, float duration, float teleportThreshold)
        {
            return GetMoveRates(prevValues.Position, nextValues.Position, prevValues.Rotation, nextValues.Rotation, prevValues.Scale, nextValues.Scale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveRates GetMoveRates(float3 fromPosition, float3 toPosition, quaternion fromRotation, quaternion toRotation, float3 fromScale, float3 toScale, float duration, float teleportThreshold)
        {
            using (_pm_GetMoveRatesFull.Auto())
            {
                float rate;

                /* Position. */
                rate = toPosition.GetRate(fromPosition, duration, out float distance);
                // Basic teleport check.
                if (teleportThreshold != UNSET_VALUE && distance > teleportThreshold)
                    return new(INSTANT_VALUE, INSTANT_VALUE, INSTANT_VALUE, duration);

                //Smoothing.
                float positionRate = rate.SetIfUnderTolerance(0.0001f, INSTANT_VALUE);
                rate = toRotation.GetRate(fromRotation, duration, out _);
                float rotationRate = rate.SetIfUnderTolerance(0.2f, INSTANT_VALUE);
                rate = toScale.GetRate(fromScale, duration, out _);
                float scaleRate = rate.SetIfUnderTolerance(0.0001f, INSTANT_VALUE);

                return new(positionRate, rotationRate, scaleRate, duration);
            }
        }

        /// <summary>
        /// Gets a move rate for two Vector3s.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMoveRate(float3 fromPosition, float3 toPosition, float duration, float teleportThreshold)
        {
            using (_pm_GetMoveRatesVec.Auto())
            {
                float rate;
                float distance;

                /* Position. */
                rate = toPosition.GetRate(fromPosition, duration, out distance);
                //Basic teleport check.
                if (teleportThreshold != UNSET_VALUE && distance > teleportThreshold)
                {
                    return INSTANT_VALUE;
                }
                //Smoothing.
                else
                {
                    float positionRate = rate.SetIfUnderTolerance(0.0001f, INSTANT_VALUE);
                    return positionRate;
                }
            }
        }

        /// <summary>
        /// Gets a move rate for two Quaternions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMoveRate(quaternion fromRotation, quaternion toRotation, float duration)
        {
            float rate = toRotation.GetRate(fromRotation, duration, out _);
            float rotationRate = rate.SetIfUnderTolerance(0.2f, INSTANT_VALUE);
            return rotationRate;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Move(Transform movingTransform, TransformProperties goalProperties, float delta, bool useWorldSpace)
        {
            if (!IsValid)
                return;

            Move(movingTransform, TransformPropertiesFlag.Everything, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.Scale, Scale, delta, useWorldSpace);
            TimeRemaining -= delta;
        }
        
        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Move(TransformAccess movingTransform, TransformProperties goalProperties, float delta, bool useWorldSpace)
        {
            if (!IsValid)
                return;

            Move(movingTransform, TransformPropertiesFlag.Everything, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.Scale, Scale, delta, useWorldSpace);
            TimeRemaining -= delta;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Move(Transform movingTransform, TransformProperties goalProperties, TransformPropertiesFlag movedProperties, float delta, bool useWorldSpace)
        {
            if (!IsValid)
                return;

            Move(movingTransform, movedProperties, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.Scale, Scale, delta, useWorldSpace);
            TimeRemaining -= delta;
        }
        
        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Move(TransformAccess movingTransform, TransformProperties goalProperties, TransformPropertiesFlag movedProperties, float delta, bool useWorldSpace)
        {
            if (!IsValid)
                return;

            Move(movingTransform, movedProperties, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.Scale, Scale, delta, useWorldSpace);
            TimeRemaining -= delta;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        public static void Move(Transform movingTransform, TransformPropertiesFlag movedProperties, float3 posGoal, float posRate, quaternion rotGoal, float rotRate, float3 scaleGoal, float scaleRate, float delta, bool useWorldSpace)
        {
            using (_pm_Move.Auto())
            {
                Transform t = movingTransform;

                bool containsPosition = movedProperties.FastContains(TransformPropertiesFlag.Position);
                bool containsRotation = movedProperties.FastContains(TransformPropertiesFlag.Rotation);
                bool containsScale = movedProperties.FastContains(TransformPropertiesFlag.Scale);

                Vector3 pos;
                Quaternion rot;
                if (useWorldSpace)
                    t.GetPositionAndRotation(out pos, out rot);
                else t.GetLocalPositionAndRotation(out pos, out rot);
                
                if (containsPosition)
                    pos = MoveTowardsFast(pos, posGoal, posRate, delta);

                if (containsRotation)
                    rot = RotateTowardsFast(rot, rotGoal, rotRate, delta);
                
                if (containsPosition || containsRotation)
                    ApplyPosRot(t, useWorldSpace, pos, rot);
                
                if (containsScale)
                {
                    var scale = t.localScale;
                    t.localScale = MoveTowardsFast(scale, scaleGoal, scaleRate, delta);
                }
            }
        }
        
        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        public static void Move(TransformAccess movingTransform, TransformPropertiesFlag movedProperties, float3 posGoal, float posRate, quaternion rotGoal, float rotRate, float3 scaleGoal, float scaleRate, float delta, bool useWorldSpace)
        {
            using (_pm_Move.Auto())
            {
                TransformAccess t = movingTransform;

                bool containsPosition = movedProperties.FastContains(TransformPropertiesFlag.Position);
                bool containsRotation = movedProperties.FastContains(TransformPropertiesFlag.Rotation);
                bool containsScale = movedProperties.FastContains(TransformPropertiesFlag.Scale);

                Vector3 pos;
                Quaternion rot;
                if (useWorldSpace)
                    t.GetPositionAndRotation(out pos, out rot);
                else t.GetCorrectLocalPositionAndRotation(out pos, out rot);
                
                if (containsPosition)
                    pos = MoveTowardsFast(pos, posGoal, posRate, delta);

                if (containsRotation)
                    rot = RotateTowardsFast(rot, rotGoal, rotRate, delta);
                
                if (containsPosition || containsRotation)
                    ApplyPosRot(t, useWorldSpace, pos, rot);
                
                if (containsScale)
                {
                    var scale = t.localScale;
                    t.localScale = MoveTowardsFast(scale, scaleGoal, scaleRate, delta);
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 MoveTowardsFast(float3 current, float3 goal, float rate, float delta)
        {
            if (rate == INSTANT_VALUE) return goal;
            if (rate == UNSET_VALUE) return current;
            
            float3 diff = goal - current;
            float  maxDelta = math.max(0f, rate * delta);

            float lenSq = math.lengthsq(diff);
            if (lenSq <= maxDelta * maxDelta) return goal;

            float invLen = math.rsqrt(lenSq); // 1 / sqrt(lenSq)
            float t = math.min(maxDelta * invLen, 1f);
            return current + diff * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static quaternion RotateTowardsFast(quaternion current, quaternion goal, float rate, float delta)
        {
            if (rate == INSTANT_VALUE) return goal;
            if (rate == UNSET_VALUE) return current;
            
            float maxDelta = math.max(0f, rate * delta);

            float dot = math.dot(current.value, goal.value);
            float c = math.saturate(math.abs(dot)); // min(|dot|, 1)
            
            float angle = math.degrees(2f * math.acos(c));
            if (angle <= maxDelta) return goal;
            
            float t = math.min(1f, maxDelta / angle);
            return math.slerp(current, goal, t);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ApplyPosRot(Transform t, bool worldSpace, float3 pos, quaternion rot)
        {
            if (worldSpace)
                t.SetPositionAndRotation(pos, rot);
            else
                t.SetLocalPositionAndRotation(pos, rot);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ApplyPosRot(TransformAccess t, bool worldSpace, float3 pos, quaternion rot)
        {
            if (worldSpace)
                t.SetPositionAndRotation(pos, rot);
            else
                t.SetLocalPositionAndRotation(pos, rot);
        }
    }

    /// <summary>
    /// Used to make calculations and perform actions in moving transforms over time.
    /// </summary>
    /// <remarks>This acts as a wrapper for MoveRates struct.</remarks>
    public class MoveRatesCls : IResettable
    {
        /// <summary>
        /// Container of all move rate information.
        /// </summary>
        private MoveRates _moveRates = new();
        /// <summary>
        /// Rate at which to move Position.
        /// </summary>
        public float Position => _moveRates.Position;
        /// <summary>
        /// Rate at which to move Rotation.
        /// </summary>
        public float Rotation => _moveRates.Rotation;
        /// <summary>
        /// Rate at which to move Scale.
        /// </summary>
        public float Scale => _moveRates.Scale;
        /// <summary>
        /// Time remaining until the move is complete.
        /// </summary>
        public float TimeRemaining => _moveRates.TimeRemaining;
        /// <summary>
        /// True if position move rate should be instant.
        /// </summary>
        public bool IsPositionInstantValue => _moveRates.IsPositionInstantValue;
        /// <summary>
        /// True if rotation move rate should be instant.
        /// </summary>
        public bool IsRotationInstantValue => _moveRates.IsRotationInstantValue;
        /// <summary>
        /// True if scale move rate should be instant.
        /// </summary>
        public bool IsScaleInstantValue => _moveRates.IsScaleInstantValue;
        /// <summary>
        /// True if any data is set.
        /// </summary>
        public bool IsValid => _moveRates.IsValid;
        public MoveRatesCls(float value) => _moveRates = new(value);
        public MoveRatesCls(float position, float rotation) => _moveRates = new(position, rotation);
        public MoveRatesCls(float position, float rotation, float scale) => _moveRates = new(position, rotation, scale);
        public MoveRatesCls(float position, float rotation, float scale, float timeRemaining) => _moveRates = new(position, rotation, scale, timeRemaining);
        public MoveRatesCls() => _moveRates.ResetState();

        /// <summary>
        /// Sets all rates to instant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInstantRates() => _moveRates.SetInstantRates();

        /// <summary>
        /// Sets all rates to the same value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float value) => _moveRates.Update(value);

        /// <summary>
        /// Updates values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float position, float rotation, float scale) => _moveRates.Update(position, rotation, scale);

        /// <summary>
        /// Updates values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float position, float rotation, float scale, float timeRemaining) => _moveRates.Update(position, rotation, scale, timeRemaining);

        /// <summary>
        /// Updaes values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(MoveRatesCls mr) => _moveRates.Update(mr.Position, mr.Rotation, mr.Scale);
        
        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Move(Transform movingTransform, TransformProperties goalProperties, float delta, bool useWorldSpace) => _moveRates.Move(movingTransform, goalProperties, delta, useWorldSpace);

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Move(TransformAccess movingTransform, TransformProperties goalProperties, float delta, bool useWorldSpace) => _moveRates.Move(movingTransform, goalProperties, delta, useWorldSpace);

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Move(Transform movingTransform, TransformProperties goalProperties, TransformPropertiesFlag movedProperties, float delta, bool useWorldSpace) => _moveRates.Move(movingTransform, goalProperties, movedProperties, delta, useWorldSpace);
        
        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Move(TransformAccess movingTransform, TransformProperties goalProperties, TransformPropertiesFlag movedProperties, float delta, bool useWorldSpace) => _moveRates.Move(movingTransform, goalProperties, movedProperties, delta, useWorldSpace);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetState() => _moveRates.ResetState();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InitializeState() { }
    }
}