using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Observing
{
    public abstract class ObserverCondition : ScriptableObject
    {
        #region Public.
        /// <summary>
        /// NetworkObject this condition is for.
        /// </summary>
        [HideInInspector]
        public NetworkObject NetworkObject;
        #endregion

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="networkObject"></param>
        public virtual void FirstInitialize(NetworkObject networkObject)
        {
            NetworkObject = networkObject;
        }
        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection"></param>
        public abstract bool ConditionMet(NetworkConnection connection);
        /// <summary>
        /// True if the condition requires regular updates.
        /// </summary>
        /// <returns></returns>
        public abstract bool Timed();
        /// <summary>
        /// Creates a clone of this condition to be instantiated.
        /// </summary>
        /// <returns></returns>
        public abstract ObserverCondition Clone();

    }
}
