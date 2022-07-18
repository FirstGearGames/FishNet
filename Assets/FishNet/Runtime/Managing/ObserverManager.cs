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
            bool isGlobal = (nob.IsGlobal && !nob.IsSceneObject);
            bool nullObs = (obs == null);
            /* NetworkObserver is null and there are no
             * conditions to add. Nothing will change by adding
             * the NetworkObserver component so exit early. */
            if (nullObs && _defaultConditions.Count == 0)
                return obs;

            //If NetworkObject does not have a NetworkObserver component.
            if (nullObs)
            {
                /* Global nobs do not need a NetworkObserver.
                 * Ultimately, a global NetworkObject is one without
                 * any conditions. */
                if (isGlobal)
                    return null;
                //If there are no conditions then there's nothing to add.
                if (_defaultConditions.Count == 0)
                    return null;
                /* If here then there not a global networkobject and there are conditions to use.
                 * Since the NetworkObserver is being added fresh, set OverrideType to UseManager
                 * so that the NetworkObserver is populated with the manager conditions. */
                obs = nob.gameObject.AddComponent<NetworkObserver>();
                obs.OverrideType = NetworkObserver.ConditionOverrideType.UseManager;
            }
            //NetworkObject has a NetworkObserver already on it.
            else
            {
                //If global the NetworkObserver has to be cleared and set to ignore manager.
                if (isGlobal)
                {
                    obs.ObserverConditionsInternal.Clear();
                    obs.OverrideType = NetworkObserver.ConditionOverrideType.IgnoreManager;
                }
            }

            //If ignoring manager then use whatever is already configured.
            if (obs.OverrideType == NetworkObserver.ConditionOverrideType.IgnoreManager)
            {
                //Do nothing.
            }
            //If using manager then replace all with conditions.
            else if (obs.OverrideType == NetworkObserver.ConditionOverrideType.UseManager)
            {
                obs.ObserverConditionsInternal.Clear();
                AddMissing(obs);
            }
            //Adding only new.
            else if (obs.OverrideType == NetworkObserver.ConditionOverrideType.AddMissing)
            {
                AddMissing(obs);
            }

            void AddMissing(NetworkObserver networkObserver)
            {
                int count = _defaultConditions.Count;
                for (int i = 0; i < count; i++)
                {
                    ObserverCondition oc = _defaultConditions[i];
                    if (!networkObserver.ObserverConditionsInternal.Contains(oc))
                        networkObserver.ObserverConditionsInternal.Add(oc);
                }
            }

            return obs;
        }
    }

}