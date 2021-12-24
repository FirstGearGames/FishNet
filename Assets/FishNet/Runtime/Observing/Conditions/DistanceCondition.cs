using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.Component.Observing
{
    /// <summary>
    /// When this observer condition is placed on an object, a client must be within the specified distance to view the object.
    /// </summary>
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
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How often this condition may change for a connection. This prevents objects from appearing and disappearing rapidly. A value of 0f will cause the object the update quickly as possible while any other value will be used as a delay.")]
        [Range(0f, 60f)]
        [SerializeField]
        private float _updateFrequency;
        /// <summary>
        /// How often this condition may change for a connection. This prevents objects from appearing and disappearing rapidly. A value of 0f will cause the object the update quickly as possible while any other value will be used as a delay.
        /// </summary>
        public float UpdateFrequency { get => _updateFrequency; set => _updateFrequency = value; }
        #endregion

        #region Private.
        /// <summary>
        /// Tracks when connections may be updated for this object.
        /// </summary>
        private Dictionary<NetworkConnection, float> _timedUpdates = new Dictionary<NetworkConnection, float>();
        #endregion

        public void ConditionConstructor(float maximumDistance, float updateFrequency)
        {
            MaximumDistance = maximumDistance;
            _updateFrequency = updateFrequency;
        }

        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="notProcessed">True if the condition was not processed. This can be used to skip processing for performance. While output as true this condition result assumes the previous ConditionMet value.</param>
        public override bool ConditionMet(NetworkConnection connection, out bool notProcessed)
        {
            if (_updateFrequency > 0f)
            {
                float nextAllowedUpdate;
                float currentTime = Time.time;
                if (!_timedUpdates.TryGetValue(connection, out nextAllowedUpdate))
                {
                    _timedUpdates[connection] = (currentTime + _updateFrequency);
                }
                else
                {
                    //Not enough time to process again.
                    if (currentTime < nextAllowedUpdate)
                    {
                        notProcessed = true;
                        //The return does not really matter since notProcessed is returned.
                        return false;
                    }
                    //Can process again.
                    else
                    {
                        _timedUpdates[connection] = (currentTime + _updateFrequency);
                    }
                }
            }
            //If here then checks are being processed.
            notProcessed = false;

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
            copy.ConditionConstructor(MaximumDistance, _updateFrequency);
            return copy;
        }
    }
}
