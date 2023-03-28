using FishNet.Connection; //remove on 2023/01/01 move to correct folder.
using FishNet.Object;
using FishNet.Observing;
using FishNet.Utility.Constant;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

[assembly: InternalsVisibleTo(UtilityConstants.DEMOS_ASSEMBLY_NAME)]
namespace FishNet.Managing.Observing
{
    /// <summary>
    /// Additional options for managing the observer system.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Manager/ObserverManager")]
    public sealed class ObserverManager : MonoBehaviour
    {
        #region Internal.
        /// <summary>
        /// Current index to use for level of detail based on tick.
        /// </summary>
        internal byte LevelOfDetailIndex { get; private set; }
        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to use the NetworkLOD system.")]
        [SerializeField]
        private bool _useNetworkLod;
        /// <summary>
        /// True to use the NetworkLOD system.
        /// </summary>
        /// <returns></returns>
        internal bool GetUseNetworkLod() => _useNetworkLod;
        /// <summary>
        /// Distance for each level of detal.
        /// </summary>
        internal List<float> GetLevelOfDetailDistances() => (_useNetworkLod) ? _levelOfDetailDistances : _singleLevelOfDetailDistances;
        [Tooltip("Distance for each level of detal.")]
        [SerializeField]
        private List<float> _levelOfDetailDistances = new List<float>();
        /// <summary>
        /// Returned when network LOD is off. Value contained is one level of detail with max distance.
        /// </summary>
        private List<float> _singleLevelOfDetailDistances = new List<float>() { float.MaxValue };
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to update visibility for clientHost based on if they are an observer or not.")]
        [FormerlySerializedAs("_setHostVisibility")]
        [SerializeField]
        private bool _updateHostVisibility = true;
        /// <summary>
        /// True to update visibility for clientHost based on if they are an observer or not.
        /// </summary>
        public bool UpdateHostVisibility
        {
            get => _updateHostVisibility;
            private set => _updateHostVisibility = value;
        }
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Default observer conditions for networked objects.")]
        [SerializeField]
        private List<ObserverCondition> _defaultConditions = new List<ObserverCondition>();
        #endregion

        #region Private.
        /// <summary>
        /// NetworkManager on object.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Intervals for each level of detail.
        /// </summary>
        private uint[] _levelOfDetailIntervals;
        #endregion

        private void Awake()
        {
            if (_useNetworkLod && _levelOfDetailDistances.Count > 1)
            {
                Debug.LogWarning("Network Level of Detail has been disabled while bugs are resolved in relation to this feature. You do not need to make any changes to your project. This warning will be removed once all issues are resolved.");
                _useNetworkLod = false;
            }
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void InitializeOnce_Internal(NetworkManager manager)
        {
            _networkManager = manager;
            ValidateLevelOfDetails();
        }

        /// <summary>
        /// Sets a new value for UpdateHostVisibility.
        /// </summary>
        /// <param name="value">New value.</param>
        /// <param name="updateType">Which objects to update.</param>
        public void SetUpdateHostVisibility(bool value, HostVisibilityUpdateTypes updateType)
        {
            //Unchanged.
            if (value == UpdateHostVisibility)
                return;

            /* Update even if server state is not known.
             * The setting should be updated so when the server
             * does start spawned objects have latest setting. */
            if (HostVisibilityUpdateContains(updateType, HostVisibilityUpdateTypes.Manager))
                UpdateHostVisibility = value;

            /* If to update spawned as well then update all networkobservers
             * with the setting and also update renderers. */
            if (_networkManager.IsServer && HostVisibilityUpdateContains(updateType, HostVisibilityUpdateTypes.Spawned))
            {
                NetworkConnection clientConn = _networkManager.ClientManager.Connection;
                foreach (NetworkObject n in _networkManager.ServerManager.Objects.Spawned.Values)
                {
                    n.NetworkObserver.SetUpdateHostVisibility(value);

                    //Only check to update renderers if clientHost. If not client then clientConn won't be active.
                    if (clientConn.IsActive)
                        n.SetRenderersVisible(n.Observers.Contains(clientConn), true);
                }
            }

            bool HostVisibilityUpdateContains(HostVisibilityUpdateTypes whole, HostVisibilityUpdateTypes part)
            {
                return (whole & part) == part;
            }
        }

        /// <summary>
        /// Adds default observer conditions to nob and returns the NetworkObserver used.
        /// </summary>
        internal NetworkObserver AddDefaultConditions(NetworkObject nob)
        {
            bool isGlobal = (nob.IsGlobal && !nob.IsSceneObject);
            bool obsAdded;

            NetworkObserver result;
            if (!nob.TryGetComponent<NetworkObserver>(out result))
            {
                obsAdded = true;
                result = nob.gameObject.AddComponent<NetworkObserver>();
            }
            else
            {
                obsAdded = false;
            }

            /* NetworkObserver is null and there are no
             * conditions to add. Nothing will change by adding
             * the NetworkObserver component so exit early. */
            if (!obsAdded && _defaultConditions.Count == 0)
                return result;

            //If the NetworkObserver component was just added.
            if (obsAdded)
            {
                /* Global nobs do not need a NetworkObserver.
                 * Ultimately, a global NetworkObject is one without
                 * any conditions. */
                if (isGlobal)
                    return result;
                //If there are no conditions then there's nothing to add.
                if (_defaultConditions.Count == 0)
                    return result;
                /* If here then there not a global networkobject and there are conditions to use.
                 * Since the NetworkObserver is being added fresh, set OverrideType to UseManager
                 * so that the NetworkObserver is populated with the manager conditions. */
                result.OverrideType = NetworkObserver.ConditionOverrideType.UseManager;
            }
            //NetworkObject has a NetworkObserver already on it.
            else
            {
                //If global the NetworkObserver has to be cleared and set to ignore manager.
                if (isGlobal)
                {
                    result.ObserverConditionsInternal.Clear();
                    result.OverrideType = NetworkObserver.ConditionOverrideType.IgnoreManager;
                }
            }

            //If ignoring manager then use whatever is already configured.
            if (result.OverrideType == NetworkObserver.ConditionOverrideType.IgnoreManager)
            {
                //Do nothing.
            }
            //If using manager then replace all with conditions.
            else if (result.OverrideType == NetworkObserver.ConditionOverrideType.UseManager)
            {
                result.ObserverConditionsInternal.Clear();
                AddMissing(result);
            }
            //Adding only new.
            else if (result.OverrideType == NetworkObserver.ConditionOverrideType.AddMissing)
            {
                AddMissing(result);
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

            return result;
        }

        /// <summary>
        /// Gets the tick interval to use for a lod level.
        /// </summary>
        /// <param name="lodLevel"></param>
        /// <returns></returns>
        public byte GetLevelOfDetailInterval(byte lodLevel)
        {
            if (LevelOfDetailIndex == 0)
                return 1;

            return (byte)System.Math.Pow(2, lodLevel);
        }

        /// <summary>
        /// Calculates and sets the current level of detail index for the tick.
        /// </summary>
        internal void CalculateLevelOfDetail(uint tick)
        {
            int count = GetLevelOfDetailDistances().Count;
            for (int i = (count - 1); i > 0; i--)
            {
                uint interval = _levelOfDetailIntervals[i];
                if (tick % interval == 0)
                {
                    LevelOfDetailIndex = (byte)i;
                    return;
                }
            }

            //If here then index is 0 and interval is every tick.
            LevelOfDetailIndex = 0;
        }

        /// <summary>
        /// Validates that level of detail intervals are proper.
        /// </summary>
        private void ValidateLevelOfDetails()
        {
            if (!_useNetworkLod)
                return;

            //No distances specified.
            if (_levelOfDetailDistances == null || _levelOfDetailDistances.Count == 0)
            {
                if (_networkManager != null)
                {
                    _networkManager.LogWarning("Level of detail distances contains no entries. NetworkLOD has been disabled.");
                    _useNetworkLod = false;
                }
                return;
            }

            //Make sure every distance is larger than the last.
            float lastDistance = float.MinValue;
            foreach (float dist in _levelOfDetailDistances)
            {
                if (dist <= 0f || dist <= lastDistance)
                {
                    if (_networkManager != null)
                    {
                        _networkManager.LogError($"Level of detail distances must be greater than 0f, and each distance larger than the previous. NetworkLOD has been disabled.");
                        _useNetworkLod = false;
                    }
                    return;
                }
                lastDistance = dist;
            }

            int maxEntries = 8;
            //Too many distances.
            if (_levelOfDetailDistances.Count > maxEntries)
            {
                _networkManager?.LogWarning("There can be a maximum of 8 level of detail distances. Entries beyond this quantity have been discarded.");
                while (_levelOfDetailDistances.Count > maxEntries)
                    _levelOfDetailDistances.RemoveAt(_levelOfDetailDistances.Count - 1);
            }

            if (Application.isPlaying)
            {
                //Build intervals and sqr distances.
                int count = _levelOfDetailDistances.Count;
                _levelOfDetailIntervals = new uint[count];
                for (int i = (count - 1); i > 0; i--)
                {
                    uint power = (uint)Mathf.Pow(2, i);
                    _levelOfDetailIntervals[i] = power;

                }
                //Sqr
                for (int i = 0; i < count; i++)
                {
                    float dist = _levelOfDetailDistances[i];
                    dist *= dist;
                    _levelOfDetailDistances[i] = dist;
                }
            }
        }

    }

}