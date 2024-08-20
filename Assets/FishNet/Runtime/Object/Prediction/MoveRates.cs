
using GameKit.Dependencies.Utilities;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Object.Prediction
{
    /// <summary>
    /// Data to be used to configure smoothing for an owned predicted object.
    /// </summary>
    [Preserve]
    internal struct MoveRates
    {
        public float Position;
        public float Rotation;
        public float Scale;
        public float TimeRemaining;

        public MoveRates(float value) : this()
        {
            Position = value;
            Rotation = value;
            Scale = value;
        }
        public MoveRates(float position, float rotation) : this()
        {
            Position = position;
            Rotation = rotation;
            Scale = MoveRatesCls.INSTANT_VALUE;
        }
        public MoveRates(float position, float rotation, float scale) : this()
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
        public MoveRates(float position, float rotation, float scale, float timeRemaining)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            TimeRemaining = timeRemaining;
        }

        /// <summary>
        /// True if a positional move rate is set.
        /// </summary>
        public bool PositionSet => (Position != MoveRatesCls.UNSET_VALUE);
        /// <summary>
        /// True if rotation move rate is set.
        /// </summary>
        public bool RotationSet => (Rotation != MoveRatesCls.UNSET_VALUE);
        /// <summary>
        /// True if a scale move rate is set.
        /// </summary>
        public bool ScaleSet => (Scale != MoveRatesCls.UNSET_VALUE);
        /// <summary>
        /// True if any move rate is set.
        /// </summary>
        public bool AnySet => (PositionSet || RotationSet || ScaleSet);

        /// <summary>
        /// True if position move rate should be instant.
        /// </summary>
        public bool InstantPosition => (Position == MoveRatesCls.INSTANT_VALUE);
        /// <summary>
        /// True if rotation move rate should be instant.
        /// </summary>
        public bool InstantRotation => (Rotation == MoveRatesCls.INSTANT_VALUE);
        /// <summary>
        /// True if scale move rate should be instant.
        /// </summary>
        public bool InstantScale => (Scale == MoveRatesCls.INSTANT_VALUE);

        /// <summary>
        /// Sets all rates to instant.
        /// </summary>
        
        public void SetInstantRates()
        {
            Update(MoveRatesCls.INSTANT_VALUE);
        }
        /// <summary>
        /// Sets all rates to the same value.
        /// </summary>
        
        public void Update(float value)
        {
            Update(value, value, value);
        }
        /// <summary>
        /// Sets rates for each property.
        /// </summary>
        public void Update(float position, float rotation, float scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        
        public static MoveRates GetWorldMoveRates(Transform from, Transform to, float duration, float teleportThreshold)
        {
            return GetMoveRates(from.position, to.position, from.rotation, to.rotation, from.localScale, to.localScale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        
        public static MoveRates GetLocalMoveRates(Transform from, Transform to, float duration, float teleportThreshold)
        {
            return GetMoveRates(from.localPosition, to.localPosition, from.localRotation, to.localRotation, from.localScale, to.localScale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        
        public static MoveRates GetWorldMoveRates(TransformProperties prevValues, Transform t, float duration, float teleportThreshold)
        {
            return GetMoveRates(prevValues.Position, t.position, prevValues.Rotation, t.rotation, prevValues.LocalScale, t.localScale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        
        public static MoveRates GetLocalMoveRates(TransformProperties prevValues, Transform t, float duration, float teleportThreshold)
        {
            return GetMoveRates(prevValues.Position, t.localPosition, prevValues.Rotation, t.localRotation, prevValues.LocalScale, t.localScale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        
        public static MoveRates GetMoveRates(TransformProperties prevValues, TransformProperties nextValues, float duration, float teleportThreshold)
        {
            return GetMoveRates(prevValues.Position, nextValues.Position, prevValues.Rotation, nextValues.Rotation, prevValues.LocalScale, nextValues.LocalScale, duration, teleportThreshold);
        }

        /// <summary>
        /// Returns a new MoveRates based on previous values, and a transforms current position.
        /// </summary>
        
        public static MoveRates GetMoveRates(Vector3 fromPosition, Vector3 toPosition, Quaternion fromRotation, Quaternion toRotation, Vector3 fromScale, Vector3 toScale, float duration, float teleportThreshold)
        {
            float rate;
            float distance;

            /* Position. */
            rate = toPosition.GetRate(fromPosition, duration, out distance);
            //Basic teleport check.
            if (teleportThreshold != MoveRatesCls.UNSET_VALUE && distance > teleportThreshold)
            {
                return new MoveRates(MoveRatesCls.INSTANT_VALUE);
            }
            //Smoothing.
            else
            {
                float positionRate = rate.SetIfUnderTolerance(0.0001f, MoveRatesCls.INSTANT_VALUE);
                rate = toRotation.GetRate(fromRotation, duration, out _);
                float rotationRate = rate.SetIfUnderTolerance(0.2f, MoveRatesCls.INSTANT_VALUE);
                rate = toScale.GetRate(fromScale, duration, out _);
                float scaleRate = rate.SetIfUnderTolerance(0.0001f, MoveRatesCls.INSTANT_VALUE);

                return new MoveRates(positionRate, rotationRate, scaleRate);
            }
        }

        /// <summary>
        /// Gets a move rate for two Vector3s.
        /// </summary>
        
        public static float GetMoveRate(Vector3 fromPosition, Vector3 toPosition, float duration, float teleportThreshold)
        {
            float rate;
            float distance;

            /* Position. */
            rate = toPosition.GetRate(fromPosition, duration, out distance);
            //Basic teleport check.
            if (teleportThreshold != MoveRatesCls.UNSET_VALUE && distance > teleportThreshold)
            {
                return MoveRatesCls.INSTANT_VALUE;
            }
            //Smoothing.
            else
            {
                float positionRate = rate.SetIfUnderTolerance(0.0001f, MoveRatesCls.INSTANT_VALUE);
                return positionRate;
            }
        }


        /// <summary>
        /// Gets a move rate for two Quaternions.
        /// </summary>
        
        public static float GetMoveRate(Quaternion fromRotation, Quaternion toRotation, float duration)
        {
            float rate = toRotation.GetRate(fromRotation, duration, out _);
            float rotationRate = rate.SetIfUnderTolerance(0.2f, MoveRatesCls.INSTANT_VALUE);
            return rotationRate;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        
        public void MoveLocalToTarget(Transform movingTransform, TransformProperties goalProperties, float delta)
        {
            //No rates are set.
            if (!AnySet)
                return;

            MoveRatesCls.MoveLocalToTarget(movingTransform, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.LocalScale, Scale, delta);
            TimeRemaining -= delta;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        
        public void MoveWorldToTarget(Transform movingTransform, TransformProperties goalProperties, float delta)
        {
            //No rates are set.
            if (!AnySet)
                return;

            MoveRatesCls.MoveWorldToTarget(movingTransform, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.LocalScale, Scale, delta);
            TimeRemaining -= delta;
        }
        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        
        public void MoveWorldToTarget(Transform movingTransform, TransformProperties goalProperties, TransformPropertiesFlag movedProperties, float delta)
        {
            //No rates are set.
            if (!AnySet)
                return;

            MoveRatesCls.MoveWorldToTarget(movingTransform, movedProperties, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.LocalScale, Scale, delta);
            TimeRemaining -= delta;
        }
    }


    /// <summary>
    /// Data to be used to configure smoothing for an owned predicted object.
    /// </summary>
    internal class MoveRatesCls : IResettable
    {
        public float Position;
        public float Rotation;
        public float Scale;
        public float TimeRemaining;

        public MoveRatesCls(float value)
        {
            Position = value;
            Rotation = value;
            Scale = value;
        }
        public MoveRatesCls(float position, float rotation)
        {
            Position = position;
            Rotation = rotation;
            Scale = INSTANT_VALUE;
        }
        public MoveRatesCls(float position, float rotation, float scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public MoveRatesCls(float position, float rotation, float scale, float timeRemaining)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            TimeRemaining = timeRemaining;
        }

        /// <summary>
        /// True if a positional move rate is set.
        /// </summary>
        public bool PositionSet => (Position != UNSET_VALUE);
        /// <summary>
        /// True if rotation move rate is set.
        /// </summary>
        public bool RotationSet => (Rotation != UNSET_VALUE);
        /// <summary>
        /// True if a scale move rate is set.
        /// </summary>
        public bool ScaleSet => (Scale != UNSET_VALUE);
        /// <summary>
        /// True if any move rate is set.
        /// </summary>
        public bool AnySet => (PositionSet || RotationSet || ScaleSet);

        /// <summary>
        /// True if position move rate should be instant.
        /// </summary>
        public bool InstantPosition => (Position == INSTANT_VALUE);
        /// <summary>
        /// True if rotation move rate should be instant.
        /// </summary>
        public bool InstantRotation => (Rotation == INSTANT_VALUE);
        /// <summary>
        /// True if scale move rate should be instant.
        /// </summary>
        public bool InstantScale => (Scale == INSTANT_VALUE);

        public MoveRatesCls()
        {
            Update(UNSET_VALUE, UNSET_VALUE, UNSET_VALUE);
        }

        /// <summary>
        /// Sets all rates to instant.
        /// </summary>
        
        public void SetInstantRates()
        {
            Update(INSTANT_VALUE);
        }
        /// <summary>
        /// Sets all rates to the same value.
        /// </summary>
        
        public void Update(float value)
        {
            Update(value, value, value);
        }
        /// <summary>
        /// Updaes values.
        /// </summary>
        public void Update(float position, float rotation, float scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
        /// <summary>
        /// Updaes values.
        /// </summary>
        
        public void Update(MoveRatesCls mr)
        {
            Update(mr.Position, mr.Rotation, mr.Scale);
        }

        public void ResetState()
        {
            Position = UNSET_VALUE;
            Rotation = UNSET_VALUE;
            Scale = UNSET_VALUE;
            TimeRemaining = UNSET_VALUE;
        }

        public void InitializeState() { }

        /// <summary>
        /// Value used when data is not set.
        /// </summary>
        public const float UNSET_VALUE = float.NegativeInfinity;
        /// <summary>
        /// Value used when move rate should be instant.
        /// </summary>
        public const float INSTANT_VALUE = float.PositiveInfinity;


        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        
        public void MoveLocalToTarget(Transform movingTransform, TransformPropertiesCls goalProperties, float delta)
        {
            //No rates are set.
            if (!AnySet)
                return;

            MoveRatesCls.MoveLocalToTarget(movingTransform, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.LocalScale, Scale, delta);
            TimeRemaining -= delta;
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        
        public static void MoveLocalToTarget(Transform movingTransform, Vector3 posGoal, float posRate, Quaternion rotGoal, float rotRate, Vector3 scaleGoal, float scaleRate, float delta)
        {
            MoveLocalToTarget(movingTransform, TransformPropertiesFlag.Everything, posGoal, posRate, rotGoal, rotRate, scaleGoal, scaleRate, delta);
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        
        public static void MoveLocalToTarget(Transform movingTransform, TransformPropertiesFlag movedProperties, Vector3 posGoal, float posRate, Quaternion rotGoal, float rotRate, Vector3 scaleGoal, float scaleRate, float delta)
        {
            Transform t = movingTransform;
            float rate;

            if (movedProperties.FastContains(TransformPropertiesFlag.Position))
            {
                rate = posRate;
                if (rate == MoveRatesCls.INSTANT_VALUE)
                    t.localPosition = posGoal;
                else
                    t.localPosition = Vector3.MoveTowards(t.localPosition, posGoal, rate * delta);
            }

            if (movedProperties.FastContains(TransformPropertiesFlag.Rotation))
            {
                rate = rotRate;
                if (rate == MoveRatesCls.INSTANT_VALUE)
                    t.localRotation = rotGoal;
                else
                    t.localRotation = Quaternion.RotateTowards(t.localRotation, rotGoal, rate * delta);
            }

            if (movedProperties.FastContains(TransformPropertiesFlag.LocalScale))
            {
                rate = scaleRate;
                if (rate == MoveRatesCls.INSTANT_VALUE)
                    t.localScale = scaleGoal;
                else
                    t.localScale = Vector3.MoveTowards(t.localScale, scaleGoal, rate * delta);
            }
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        
        public static void MoveWorldToTarget(Transform movingTransform, Vector3 posGoal, float posRate, Quaternion rotGoal, float rotRate, Vector3 scaleGoal, float scaleRate, float delta)
        {
            MoveWorldToTarget(movingTransform, TransformPropertiesFlag.Everything, posGoal, posRate, rotGoal, rotRate, scaleGoal, scaleRate, delta);
        }

        /// <summary>
        /// Moves transform to target values.
        /// </summary>
        
        public static void MoveWorldToTarget(Transform movingTransform, TransformPropertiesFlag movedProperties, Vector3 posGoal, float posRate, Quaternion rotGoal, float rotRate, Vector3 scaleGoal, float scaleRate, float delta)
        {
            Transform t = movingTransform;
            float rate;

            if (movedProperties.FastContains(TransformPropertiesFlag.Position))
            {
                rate = posRate;
                if (rate == MoveRatesCls.INSTANT_VALUE)
                    t.position = posGoal;
                else if (rate == MoveRatesCls.UNSET_VALUE) { }
                else
                    t.position = Vector3.MoveTowards(t.position, posGoal, rate * delta);
            }

            //Debug.Log($"StartX {start.x.ToString("0.00")}. End {t.position.x.ToString("0.00")}. Rate {posRate}. Delta {delta}");

            if (movedProperties.FastContains(TransformPropertiesFlag.Rotation))
            {
                rate = rotRate;
                if (rate == MoveRatesCls.INSTANT_VALUE)
                    t.rotation = rotGoal;
                else if (rate == MoveRatesCls.UNSET_VALUE) { }
                else
                    t.rotation = Quaternion.RotateTowards(t.rotation, rotGoal, rate * delta);
            }

            if (movedProperties.FastContains(TransformPropertiesFlag.LocalScale))
            {
                rate = scaleRate;
                if (rate == MoveRatesCls.INSTANT_VALUE)
                    t.localScale = scaleGoal;
                else if (rate == MoveRatesCls.UNSET_VALUE) { }
                else
                    t.localScale = Vector3.MoveTowards(t.localScale, scaleGoal, rate * delta);
            }
        }

    }


}