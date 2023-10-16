
using GameKit.Utilities;
using System.Runtime.CompilerServices;

namespace FishNet.Object.Prediction
{
    /// <summary>
    /// Data to be used to configure smoothing for an owned predicted object.
    /// </summary>
    internal struct MoveRates
    {
        public float Position;
        public float Rotation;
        public float Scale;

        public MoveRates(float value)
        {
            Position = value;
            Rotation = value;
            Scale = value;
        }
        public MoveRates(float position, float rotation)
        {
            Position = position;
            Rotation = rotation;
            Scale = INSTANT_VALUE;
        }
        public MoveRates(float position, float rotation, float scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
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

        /// <summary>
        /// Sets all rates to instant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInstantRates()
        {
            Update(INSTANT_VALUE);
        }
        /// <summary>
        /// Sets all rates to the same value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// Value used when data is not set.
        /// </summary>
        public const float UNSET_VALUE = float.NegativeInfinity;
        /// <summary>
        /// Value used when move rate should be instant.
        /// </summary>
        public const float INSTANT_VALUE = float.PositiveInfinity;
    }


    /// <summary>
    /// Data to be used to configure smoothing for an owned predicted object.
    /// </summary>
    internal class MoveRatesCls : IResettable
    {
        public float Position;
        public float Rotation;
        public float Scale;

        public float LastMultiplier { get; private set; } = 1f;

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
        /// Multiplies all rates by value.
        /// </summary>
        public void Multiply(float value)
        {
            LastMultiplier = value;
            Position *= value;
            Rotation *= value;
            Scale *= value;
        }

        /// <summary>
        /// Sets all rates to instant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInstantRates()
        {
            Update(INSTANT_VALUE);
        }
        /// <summary>
        /// Sets all rates to the same value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(MoveRatesCls mr)
        {
            Update(mr.Position, mr.Rotation, mr.Scale);
        }

        public void ResetState()
        {
            Position = UNSET_VALUE;
            Rotation = UNSET_VALUE;
            Scale = UNSET_VALUE;
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
    }


}