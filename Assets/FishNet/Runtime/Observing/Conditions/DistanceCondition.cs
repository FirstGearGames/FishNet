using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using UnityEngine;

namespace FishNet.Component.Observing
{
    [CreateAssetMenu(menuName = "FishNet/Observers/Distance Condition", fileName = "New Distance Condition")]
    public class DistanceCondition : ObserverCondition
    {
        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum distance a client must be within this object to see it.")]
        [SerializeField]
        private float _maximumDistance = 100f;
        /// <summary>
        /// Maximum distance a client must be within this object to see it.
        /// </summary>
        public float MaximumDistance { get => _maximumDistance; set => _maximumDistance = value; }

        #endregion
        public void ConditionConstructor(float maximumDistance)
        {
            MaximumDistance = maximumDistance;
        }

        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection"></param>
        public override bool ConditionMet(NetworkConnection connection)
        {
            float sqrMaximumDistance = (MaximumDistance * MaximumDistance);
            Vector3 thisPosition = NetworkObject.transform.position;
            foreach (NetworkObject nob in connection.Objects)
            {
                //If within distance.
                if (Vector3.SqrMagnitude(nob.transform.position - thisPosition) <= sqrMaximumDistance)
                    return true;
            }

            /* If here no client objects are within distance. */
            return false;
        }

        /// <summary>
        /// True if the condition requires regular updates.
        /// </summary>
        /// <returns></returns>
        public override bool Timed()
        {
            return true;
        }

        /// <summary>
        /// Clones referenced ObserverCondition. This must be populated with your conditions settings.
        /// </summary>
        /// <returns></returns>
        public override ObserverCondition Clone()
        {
            DistanceCondition copy = ScriptableObject.CreateInstance<DistanceCondition>();
            copy.ConditionConstructor(MaximumDistance);
            return copy;
        }
    }
}
