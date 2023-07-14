using UnityEngine;

namespace GameKit.Utilities.Types
{


    [System.Serializable]
    public struct Vector2Range
    {
        public Vector2Range(Vector2 minimum, Vector2 maximum)
        {
            X = new FloatRange(minimum.x, maximum.x);
            Y = new FloatRange(minimum.y, maximum.y);
        }
        public Vector2Range(FloatRange minimum, FloatRange maximum)
        {
            X = minimum;
            Y = maximum;
        }
        /// <summary>
        /// Minimum range.
        /// </summary>
        public FloatRange X;
        /// <summary>
        /// Maximum range.
        /// </summary>
        public FloatRange Y;

        /// <summary>
        /// Returns a random value between Minimum and Maximum.
        /// </summary>
        /// <returns></returns>
        public Vector2 RandomInclusive()
        {
            return new Vector2(
                Floats.RandomInclusiveRange(X.Minimum, X.Maximum),
                Floats.RandomInclusiveRange(Y.Minimum, Y.Maximum)
                );
        }
    }


}