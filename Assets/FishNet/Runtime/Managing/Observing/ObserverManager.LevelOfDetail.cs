using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace FishNet.Managing.Observing
{
    /// <summary>
    /// Handles level of detail actions.
    /// </summary>
    public sealed partial class ObserverManager : MonoBehaviour
    {
        /// <summary>
        /// True to enable level of detail.
        /// </summary>
        internal bool UseLevelOfDetail => _useLevelOfDetail;
        [Tooltip("True to enable level of detail.")]
        [SerializeField]
        private bool _useLevelOfDetail;
        /// <summary>
        /// The maximum delay between updates when an object is using the highest level of detail.
        /// </summary>
        [Tooltip("The maximum delay between updates when an object is using the highest level of detail.")]
        [Range(MINIMUM_LEVEL_OF_DETAIL_SEND_INTERVAL, MAXIMUM_LEVEL_OF_DETAIL_SEND_INTERVAL)]
        [SerializeField]
        private float _maximumLevelOfDetailInterval = 2f;
        /// <summary>
        /// The time it will take to calculate new level of detail values. 
        /// </summary>
        [Tooltip("The time it will take to calculate new level of detail values.")]
        [Range(MINIMUM_LEVEL_OF_DETAIL_UPDATE_DURATION, MAXIMUM_LEVEL_OF_DETAIL_UPDATE_DURATION)]
        [SerializeField]
        private float _levelOfDetailUpdateDuration = 2f;
        /// <summary>
        /// Distances for each level of detail change. Each distance is an exponential increase. When the largest distance is surpassed the maximum delay is used; while within the first distance standard delays are used. 
        /// </summary>
        [Tooltip("Distances for each level of detail change. Each distance is an exponential increase. When the largest distance is surpassed the maximum delay is used; while within the first distance standard delays are used.")]
        [SerializeField]
        private List<float> _levelOfDetailDistances = new();
        /// <summary>
        /// All NetworkObjects using level of detail.
        /// </summary>
        private List<NetworkObject> _levelOfDetailObjects = new();
        /// <summary>
        /// The next index to iterate on levelOfDetailObjects.
        /// </summary>
        private int _currentLevelOfDetailsObjectIndex;
        /// <summary>
        /// Tick delay values for each level of detail entry.
        /// </summary>
        internal IReadOnlyList<uint> LevelOfDetailTickDivisors => _levelOfDetailTickDivisors;
        private List<uint> _levelOfDetailTickDivisors = new();
        /// <summary>
        /// True once level of detail has been initialized.
        /// </summary>
        private bool _levelOfDetailInitialized;
        /// <summary>
        /// The next time level of detail can be recalculated in segments.
        /// </summary>
        private float _nextLevelOfDetailRecalculation = 0f;
        /// <summary>
        /// Level of detail entries which have changed for the current object being iterated.
        /// </summary>
        private List<(NetworkConnection Connection, uint Level)> _changedLevelOfDetails = new();
        /// <summary>
        /// Squared values to level of detail distances.
        /// </summary>
        private List<float> _levelOfDetailDistancesSquared = new();

        #region Consts.
        /// <summary>
        /// Minimum time allowed for the maximum level of detail send interval.
        /// </summary>
        private const float MINIMUM_LEVEL_OF_DETAIL_SEND_INTERVAL = 0.1f;
        /// <summary>
        /// Maximum time allowed for the maximum level of detail send interval.
        /// </summary>
        private const float MAXIMUM_LEVEL_OF_DETAIL_SEND_INTERVAL = 15f;
        /// <summary>
        /// Minimum time which can be used for the level of detail update duration.
        /// </summary>
        private const float MINIMUM_LEVEL_OF_DETAIL_UPDATE_DURATION = 0.5f;
        /// <summary>
        /// Maximum time which can be used for the level of detail update duration.
        /// </summary>
        private const float MAXIMUM_LEVEL_OF_DETAIL_UPDATE_DURATION = 10f;
        #endregion

        /// <summary>
        /// Updates the duration of how long level of update values should be recalculated.
        /// </summary>
        public void SetLevelOfDetailRecalculationDuration(float duration) => _levelOfDetailUpdateDuration = Mathf.Clamp(duration, MINIMUM_LEVEL_OF_DETAIL_UPDATE_DURATION, MAXIMUM_LEVEL_OF_DETAIL_UPDATE_DURATION);

        /// <summary>
        /// Initializes for level of detail use.
        /// </summary>
        /// <returns>New UseLevelOfDetail value.</returns>
        private bool InitializeLevelOfDetailValues()
        {
            return false;
        }

        /// <summary>
        /// Gets the level of detail index to use based on distance.
        /// </summary>
        internal byte GetLevelOfDetailIndexUnsafe(float sqrDistance)
        {
            int lodDistancesCount = _levelOfDetailDistancesSquared.Count;

            for (int i = lodDistancesCount - 1; i >= 0; i--)
            {
                /* If larger or equal to the lod then
                 * return the current index + 1, keeping in mind the first entry
                 * (index 0) of _levelOfDetailTickDivisors is 1 tick.
                 *
                 * When a result is found we skip beyond the added entry.
                 * When not found, the 1 tick divisor is used. */
                if (sqrDistance >= _levelOfDetailDistancesSquared[i])
                    return (byte)(i + 1);
            }

            /* If the distance is not >= any value then index
             * 0 is returned, which indicates no addtional LOD interval. */
            return 0;
        }

        /// <summary>
        /// Adds a NetworkObject to level of detail objects.
        /// </summary>
        internal void AddLevelOfDetailNetworkObject(NetworkObject networkObject)
        {
            _levelOfDetailObjects.AddUnique(networkObject);
        }

        /// <summary>
        /// removes a NetworkObject to level of detail objects.
        /// </summary>
        internal void RemoveLevelOfDetailNetworkObject(NetworkObject networkObject)
        {
            _levelOfDetailObjects.Remove(networkObject);
        }

        /// <summary>
        /// Updates level of detail values incrementally for all objects which support it.
        /// </summary>
        internal void UpdateLevelOfDetails()
        {
            if (!_networkManager.IsServerStarted)
                return;

            int objectsCount = _levelOfDetailObjects.Count;
            if (objectsCount == 0)
                return;

            //Safety check before beginning.
            if (_levelOfDetailDistancesSquared == null || _levelOfDetailDistancesSquared.Count == 0)
                return;

            float unscaledTime = Time.unscaledTime;

            //Cannot update yet.
            if (unscaledTime < _nextLevelOfDetailRecalculation)
                return;

            _nextLevelOfDetailRecalculation = unscaledTime + _levelOfDetailUpdateDuration / objectsCount;

            /* Always perform 1 iteration per interval. If the frame delta
             * is larger than the target time that is okay, it will just take
             * a little longer; this allows scalability. */


            //Must start over at the beginning.
            if (_currentLevelOfDetailsObjectIndex >= objectsCount)
                _currentLevelOfDetailsObjectIndex = 0;


            NetworkObject networkObject = _levelOfDetailObjects[_currentLevelOfDetailsObjectIndex++];

            Vector3 position = networkObject.transform.position;

            Dictionary<NetworkConnection, uint> levelOfDetailConnections = networkObject.ObserverLevelOfDetailDivisors;

            foreach (KeyValuePair<NetworkConnection, uint> kvp in levelOfDetailConnections)
            {
                NetworkConnection connection = kvp.Key;

                if (!TryGetLevelOfDetailIndex(position, connection, out byte levelOfDetailIndex))
                    continue;

                uint value = kvp.Value;
                uint newValue = _levelOfDetailTickDivisors[levelOfDetailIndex];
                //No change.
                if (newValue == value)
                    continue;

                _changedLevelOfDetails.Add((connection, newValue));
            }

            /* Update any changed level of details. */
            foreach ((NetworkConnection Connection, uint Ticks) change in _changedLevelOfDetails)
                levelOfDetailConnections[change.Connection] = change.Ticks;

            _changedLevelOfDetails.Clear();
        }

        /// <summary>
        /// Gets the level of detail divisor index using an objects position against a connections owned objects.
        /// </summary>
        private bool TryGetLevelOfDetailIndex(Vector3 position, NetworkConnection connectionToCompare, out byte levelOfDetailIndex)
        {
            //This will be overriden.
            levelOfDetailIndex = 0;

            /* If the player does not own any objects then do not update for this connection.
             * This will use whatever the current value is for the connection. */
            if (connectionToCompare.Objects.Count == 0)
            {
                levelOfDetailIndex = (byte)(_levelOfDetailDistancesSquared.Count - 1);
                return false;
            }

            float closestDistance = float.MaxValue;

            foreach (NetworkObject connectionObjects in connectionToCompare.Objects)
            {
                float sqrDistance = (position - connectionObjects.transform.position - position).sqrMagnitude;
                if (sqrDistance < closestDistance)
                    closestDistance = sqrDistance;
            }

            levelOfDetailIndex = GetLevelOfDetailIndexUnsafe(closestDistance);

            return true;
        }

        /// <summary>
        /// Gets the divisor to use with a modulo operation using an objects position against the local client's owned objects.
        /// </summary>
        internal uint GetLocalLevelOfDetailDivisorUnsafe(Vector3 position)
        {
            NetworkConnection localConnection = _networkManager.ClientManager.Connection;

            if (!localConnection.IsValid())
            {
                _networkManager.LogError($"The client socket was expected to be valid but was not.");

                //Return the first entry when possible.
                if (_levelOfDetailTickDivisors != null && _levelOfDetailTickDivisors.Count > 0)
                    return _levelOfDetailTickDivisors[0];

                _networkManager.LogError($"Level of detail divisors are not set.");
                //Return 1 as a fallback.
                return 1;
            }

            TryGetLevelOfDetailIndex(position, localConnection, out byte levelOfDetailIndex);

            return _levelOfDetailTickDivisors[levelOfDetailIndex];
        }
    }
}