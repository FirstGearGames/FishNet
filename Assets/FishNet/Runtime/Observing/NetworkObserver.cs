using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Object;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace FishNet.Observing
{
    /// <summary>
    /// Controls which clients can see and get messages for an object.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FishNet/Component/NetworkObserver")]
    public sealed class NetworkObserver : MonoBehaviour
    {
        #region Types.
        /// <summary>
        /// How ObserverManager conditions are used.
        /// </summary>
        public enum ConditionOverrideType
        {
            /// <summary>
            /// Keep current conditions, add new conditions from manager.
            /// </summary>
            AddMissing = 1,
            /// <summary>
            /// Replace current conditions with manager conditions.
            /// </summary>
            UseManager = 2,
            /// <summary>
            /// Keep current conditions, ignore manager conditions.
            /// </summary>
            IgnoreManager = 3,
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("How ObserverManager conditions are used.")]
        [SerializeField]
        private ConditionOverrideType _overrideType = ConditionOverrideType.IgnoreManager;
        /// <summary>
        /// How ObserverManager conditions are used.
        /// </summary>
        public ConditionOverrideType OverrideType
        {
            get => _overrideType;
            internal set => _overrideType = value;
        }

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
        [Tooltip("Conditions connections must met to be added as an observer. Multiple conditions may be used.")]
        [SerializeField]
        internal List<ObserverCondition> _observerConditions = new List<ObserverCondition>();
        /// <summary>
        /// Conditions connections must met to be added as an observer. Multiple conditions may be used.
        /// </summary>
        public IReadOnlyList<ObserverCondition> ObserverConditions => _observerConditions;
        [APIExclude]
#if MIRROR
        public List<ObserverCondition> ObserverConditionsInternal
#else
        internal List<ObserverCondition> ObserverConditionsInternal
#endif
        {
            get => _observerConditions;
            set => _observerConditions = value;
        }
        #endregion

        #region Private.
        /// <summary>
        /// Conditions under this component which are timed.
        /// </summary>
        private List<ObserverCondition> _timedConditions = new List<ObserverCondition>();
        /// <summary>
        /// Connections which have all non-timed conditions met.
        /// </summary>
        private HashSet<NetworkConnection> _nonTimedMet = new HashSet<NetworkConnection>();
        /// <summary>
        /// NetworkObject this belongs to.
        /// </summary>
        private NetworkObject _networkObject;
        /// <summary>
        /// Becomes true when registered with ServerObjects as Timed observers.
        /// </summary>
        private bool _registeredAsTimed;
        /// <summary>
        /// True if already pre-initialized.
        /// </summary>
        private bool _preintiialized;
        /// <summary>
        /// True if ParentNetworkObject was visible last iteration.
        /// This value will also be true if there is no ParentNetworkObject.
        /// </summary>
        private bool _lastParentVisible;
        #endregion

        private void OnEnable()
        {
            if (_networkObject != null && _networkObject.IsServer)
                RegisterTimedConditions();
        }
        private void OnDisable()
        {
            if (_networkObject != null && _networkObject.IsDeinitializing)
            {
                _lastParentVisible = false;
                _nonTimedMet.Clear();
                UnregisterTimedConditions();
            }
        }
        private void OnDestroy()
        {
            if (_networkObject != null)
                UnregisterTimedConditions();
        }

        internal void Deinitialize()
        {
            if (_networkObject != null && _networkObject.IsDeinitializing)
            {
                _networkObject.ServerManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
                UnregisterTimedConditions();
            }
        }

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="networkManager"></param>
        internal void PreInitialize(NetworkObject networkObject)
        {
            if (!_preintiialized)
            {
                _preintiialized = true;
                _networkObject = networkObject;
                bool ignoringManager = (OverrideType == ConditionOverrideType.IgnoreManager);

                //Check to override SetHostVisibility.
                if (!ignoringManager)
                    UpdateHostVisibility = networkObject.ObserverManager.UpdateHostVisibility;

                bool observerFound = false;
                for (int i = 0; i < _observerConditions.Count; i++)
                {
                    if (_observerConditions[i] != null)
                    {
                        observerFound = true;

                        /* Make an instance of each condition so values are
                         * not overwritten when the condition exist more than
                         * once in the scene. Double edged sword of using scriptable
                         * objects for conditions. */
                        _observerConditions[i] = _observerConditions[i].Clone();
                        ObserverCondition oc = _observerConditions[i];
                        oc.InitializeOnce(_networkObject);
                        //If timed also register as containing timed conditions.
                        if (oc.Timed())
                            _timedConditions.Add(oc);
                    }
                    else
                    {
                        _observerConditions.RemoveAt(i);
                        i--;
                    }
                }

                //No observers specified, do not need to take further action.
                if (!observerFound)
                    return;

                _networkObject.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
            }

            RegisterTimedConditions();
        }

        /// <summary>
        /// Returns a condition if found within Conditions.
        /// </summary>
        /// <returns></returns>
        public ObserverCondition GetObserverCondition<T>() where T : ObserverCondition
        {
            /* Do not bother setting local variables,
             * condition collections aren't going to be long
             * enough to make doing so worth while. */

            System.Type conditionType = typeof(T);
            for (int i = 0; i < _observerConditions.Count; i++)
            {
                if (_observerConditions[i].GetType() == conditionType)
                    return _observerConditions[i];
            }

            //Fall through, not found.
            return null;
        }

        /// <summary>
        /// Returns ObserverStateChange by comparing conditions for a connection.
        /// </summary>
        /// <returns>True if added to Observers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ObserverStateChange RebuildObservers(NetworkConnection connection, bool timedOnly)
        {
            bool currentlyAdded = (_networkObject.Observers.Contains(connection));
            //True if all conditions are met.
            bool allConditionsMet = true;

            //Only need to check beyond this if conditions exist.
            if (_observerConditions.Count > 0)
            {
                /* If cnnection is owner then they can see the object. */
                bool notOwner = (connection != _networkObject.Owner);

                /* Only check conditions if not owner. Owner will always
                * have visibility. */
                if (notOwner)
                {
                    bool parentVisible = true;
                    if (_networkObject.ParentNetworkObject != null)
                    {
                        parentVisible = _networkObject.ParentNetworkObject.Observers.Contains(connection);
                        /* If parent is visible but was not previously
                         * then unset timedOnly to make sure all conditions
                         * are checked again. This ensures that the _nonTimedMet
                         * collection is updated. */
                        if (parentVisible && !_lastParentVisible)
                            timedOnly = false;
                        _lastParentVisible = parentVisible;
                    }

                    //If parent is not visible no further checks are required.
                    if (!parentVisible)
                    {
                        allConditionsMet = false;
                    }
                    //Parent is visible, perform checks.
                    else
                    {
                        //True if connection starts with meeting non-timed conditions.
                        bool startNonTimedMet = _nonTimedMet.Contains(connection);
                        /* If a timed update an1d nonTimed
                         * have not been met then there's
                         * no reason to check timed. */
                        if (timedOnly && !startNonTimedMet)
                        {
                            allConditionsMet = false;
                        }
                        else
                        {
                            //Becomes true if a non-timed condition fails.
                            bool nonTimedMet = true;

                            List<ObserverCondition> collection = (timedOnly) ? _timedConditions : _observerConditions;
                            for (int i = 0; i < collection.Count; i++)
                            {
                                ObserverCondition condition = collection[i];
                                /* If any observer returns removed then break
                                 * from loop and return removed. If one observer has
                                 * removed then there's no reason to iterate
                                 * the rest.
                                 * 
                                 * A condition is automatically met if it's not enabled. */
                                bool notProcessed = false;
                                bool conditionMet = (!condition.GetIsEnabled() || condition.ConditionMet(connection, currentlyAdded, out notProcessed));

                                if (notProcessed)
                                    conditionMet = currentlyAdded;

                                //Condition not met.
                                if (!conditionMet)
                                {
                                    allConditionsMet = false;
                                    if (!condition.Timed())
                                        nonTimedMet = false;
                                    break;
                                }
                            }

                            //If all conditions are being checked and nonTimedMet has updated.
                            if (!timedOnly && (startNonTimedMet != nonTimedMet))
                            {
                                if (nonTimedMet)
                                    _nonTimedMet.Add(connection);
                                else
                                    _nonTimedMet.Remove(connection);
                            }
                        }
                    }
                }
            }

            //If all conditions met.
            if (allConditionsMet)
                return ReturnPassedConditions(currentlyAdded);
            else
                return ReturnFailedCondition(currentlyAdded);
        }

        /// <summary>
        /// Registers timed observer conditions.
        /// </summary>
        private void RegisterTimedConditions()
        {
            if (_timedConditions.Count == 0)
                return;
            //Already registered or no timed conditions.
            if (_registeredAsTimed)
                return;

            _registeredAsTimed = true;
            _networkObject.NetworkManager.ServerManager.Objects.AddTimedNetworkObserver(_networkObject);
        }

        /// <summary>
        /// Unregisters timed conditions.
        /// </summary>
        private void UnregisterTimedConditions()
        {
            if (_timedConditions.Count == 0)
                return;
            if (!_registeredAsTimed)
                return;

            _registeredAsTimed = false;
            _networkObject.NetworkManager.ServerManager.Objects.RemoveTimedNetworkObserver(_networkObject);
        }

        /// <summary>
        /// Returns an ObserverStateChange when a condition fails.
        /// </summary>
        /// <param name="currentlyAdded"></param>
        /// <returns></returns>
        private ObserverStateChange ReturnFailedCondition(bool currentlyAdded)
        {
            if (currentlyAdded)
                return ObserverStateChange.Removed;
            else
                return ObserverStateChange.Unchanged;
        }

        /// <summary>
        /// Returns an ObserverStateChange when all conditions pass.
        /// </summary>
        /// <param name="currentlyAdded"></param>
        /// <returns></returns>
        private ObserverStateChange ReturnPassedConditions(bool currentlyAdded)
        {
            if (currentlyAdded)
                return ObserverStateChange.Unchanged;
            else
                return ObserverStateChange.Added;
        }

        /// <summary>
        /// Called when a remote client state changes with the server.
        /// </summary>
        private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs arg2)
        {
            if (arg2.ConnectionState == RemoteConnectionState.Stopped)
                _nonTimedMet.Remove(conn);
        }

        /// <summary>
        /// Sets a new value for UpdateHostVisibility.
        /// This does not immediately update renderers.
        /// You may need to combine with NetworkObject.SetRenderersVisible(bool).
        /// </summary>
        /// <param name="value">New value.</param>
        public void SetUpdateHostVisibility(bool value)
        {
            //Unchanged.
            if (value == UpdateHostVisibility)
                return;

            UpdateHostVisibility = value;
        }

    }
}
