using FishNet.Object;
using FishNet.Observing;
using System.Collections.Generic;
using UnityEngine;


namespace FishNet.Managing.Observing
{

    /// <summary>
    /// Additional options for managing the observer system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObserverManager : MonoBehaviour
    {
        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Default observer conditions for networked objects.")]
        [SerializeField]
        private List<ObserverCondition> _defaultConditions = new List<ObserverCondition>();
        #endregion

        /// <summary>
        /// Adds default observer conditions to nob and returns the NetworkObserver used.
        /// </summary>
        internal NetworkObserver AddDefaultConditions(NetworkObject nob, ref NetworkObserver obs)
        {
            bool nullObs = (obs == null);
            /* NetworkObserver is null and there are no
             * conditions to add. Nothing will change by adding
             * the NetworkObserver component so exit early. */
            if (nullObs && _defaultConditions.Count == 0)
                return obs;

            //If null then add.
            if (nullObs)
                obs = nob.gameObject.AddComponent<NetworkObserver>();
            //If not null and ignoring manager.
            else if (obs.OverrideType == NetworkObserver.ConditionOverrideType.IgnoreManager)
                return obs;

            //If using manager then replace all with conditions.
            if (obs.OverrideType == NetworkObserver.ConditionOverrideType.UseManager)
            {
                obs.ObserverConditionsInternal = _defaultConditions;
            }
            //Adding only new.
            else
            {
                int count = _defaultConditions.Count;
                for (int i = 0; i < count; i++)
                {
                    ObserverCondition oc = _defaultConditions[i];
                    if (!obs.ObserverConditionsInternal.Contains(oc))
                        obs.ObserverConditionsInternal.Add(oc);
                }
            }

            return obs;
        }
    }

}