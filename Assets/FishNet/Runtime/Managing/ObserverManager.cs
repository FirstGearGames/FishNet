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
        [Tooltip("True to update visibility for clientHost based on if they are an observer or not.")]
        [SerializeField]
        private bool _setHostVisibility = true;
        /// <summary>
        /// True to update visibility for clientHost based on if they are an observer or not.
        /// </summary>
        public bool SetHostVisibility => _setHostVisibility;
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

            //If null then add and default to use manager.
            if (nullObs)
            { 
                obs = nob.AddAndSerialize<NetworkObserver>();
                obs.OverrideType = NetworkObserver.ConditionOverrideType.UseManager;
            }

            //If global then ignore manager and clear all. This overrides other settings.
            if (nob.IsGlobal && !nob.IsSceneObject)
            {
                obs.OverrideType = NetworkObserver.ConditionOverrideType.IgnoreManager;
                obs.ObserverConditionsInternal.Clear();
                return obs;
            }            
            //If ignoring manager.
            else if (obs.OverrideType == NetworkObserver.ConditionOverrideType.IgnoreManager)
                return obs;
            //If using manager then replace all with conditions.
            else if (obs.OverrideType == NetworkObserver.ConditionOverrideType.UseManager)
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