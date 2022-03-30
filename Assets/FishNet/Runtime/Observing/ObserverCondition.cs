using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Observing
{
    /// <summary>
    /// Condition a connection must meet to be added as an observer.
    /// This class can be inherited from for custom conditions.
    /// </summary>
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
        public virtual void InitializeOnce(NetworkObject networkObject)
        {
            NetworkObject = networkObject;
        }
        /// <summary>
        /// Returns if the object which this condition resides should be visible to connection.
        /// </summary>
        /// <param name="connection">Connection which the condition is being checked for.</param>
        /// <param name="currentlyAdded">True if the connection currently has visibility of this object.</param>
        /// <param name="notProcessed">True if the condition was not processed. This can be used to skip processing for performance. While output as true this condition result assumes the previous ConditionMet value.</param>
        public abstract bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed);
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
