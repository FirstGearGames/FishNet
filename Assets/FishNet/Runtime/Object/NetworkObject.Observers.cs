using FishNet.Component.Observing;
using FishNet.Connection;
using FishNet.Observing;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called when the clientHost gains or loses visibility of this object.
        /// Boolean value will be true if clientHost has visibility.
        /// </summary>        
        public event HostVisibilityUpdatedDelegate OnHostVisibilityUpdated;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="prevVisible">True if clientHost was known to have visibility of the object prior to this invoking.</param>
        /// <param name="nextVisible">True if the clientHost now has visibility of the object.</param>
        public delegate void HostVisibilityUpdatedDelegate(bool prevVisible, bool nextVisible);
        /// <summary>
        /// Called when this NetworkObject losses all observers or gains observers while previously having none.
        /// </summary>
        public event Action<NetworkObject> OnObserversActive;
        /// <summary>
        /// NetworkObserver on this object.
        /// </summary>
        [HideInInspector]
        public NetworkObserver NetworkObserver = null;
        /// <summary>
        /// Clients which can see and get messages from this NetworkObject.
        /// </summary>
        [HideInInspector]
        public HashSet<NetworkConnection> Observers = new HashSet<NetworkConnection>();
        #endregion

        #region Internal.
        /// <summary>
        /// Current HashGrid entry this belongs to.
        /// </summary>
        internal GridEntry HashGridEntry;
        #endregion

        #region Private.
        /// <summary>
        /// True if NetworkObserver has been initialized.
        /// </summary>
        private bool _networkObserverInitiliazed = false;
        /// <summary>
        /// Found renderers on the NetworkObject and it's children. This is only used as clientHost to hide non-observers objects.
        /// </summary>
        [System.NonSerialized]
        private Renderer[] _renderers;
        /// <summary>
        /// True if renderers have been looked up.
        /// </summary>
        private bool _renderersPopulated;
        /// <summary>
        /// Last visibility value for clientHost on this object.
        /// </summary>
        private bool _lastClientHostVisibility;
        /// <summary>
        /// HashGrid for this object.
        /// </summary>
        private HashGrid _hashGrid;
        /// <summary>
        /// Next time this object may update it's position for HashGrid.
        /// </summary>
        private float _nextHashGridUpdateTime;
        /// <summary>
        /// True if this gameObject is static.
        /// </summary>
        private bool _isStatic;
        /// <summary>
        /// Current grid position.
        /// </summary>
        private Vector2Int _hashGridPosition = HashGrid.UnsetGridPosition;
        #endregion

        /// <summary>
        /// Updates Objects positions in the HashGrid for this Networkmanager.
        /// </summary>
        internal void UpdateForNetworkObject(bool force)
        {
            if (_hashGrid == null)
                return;
            if (_isStatic)
                return;

            float unscaledTime = Time.unscaledTime;
            //Not enough time has passed to update.
            if (!force && unscaledTime < _nextHashGridUpdateTime)
                return;

            const float updateInterval = 1f;
            _nextHashGridUpdateTime = unscaledTime + updateInterval;
            Vector2Int newPosition = _hashGrid.GetHashGridPosition(this);
            if (newPosition != _hashGridPosition)
            {
                _hashGridPosition = newPosition;
                HashGridEntry = _hashGrid.GetGridEntry(newPosition);
            }            
        }

        /// <summary>
        /// Updates cached renderers used to managing clientHost visibility.
        /// </summary>
        /// <param name="updateVisibility">True to also update visibility if clientHost.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateRenderers(bool updateVisibility = true)
        {
            UpdateRenderers_Internal(updateVisibility);
        }

        /// <summary>
        /// Sets the renderer visibility for clientHost.
        /// </summary>
        /// <param name="visible">True if renderers are to be visibile.</param>
        /// <param name="force">True to skip blocking checks.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRenderersVisible(bool visible, bool force = false)
        {
            if (!force)
            {
                if (!NetworkObserver.UpdateHostVisibility)
                    return;
            }

            if (!_renderersPopulated)
            {
                UpdateRenderers_Internal(false);
                _renderersPopulated = true;
            }

            UpdateRenderVisibility(visible);
        }

        /// <summary>
        /// Clears and updates renderers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateRenderers_Internal(bool updateVisibility)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            List<Renderer> enabledRenderers = new List<Renderer>();
            foreach (Renderer r in _renderers)
            {
                if (r.enabled)
                    enabledRenderers.Add(r);
            }
            //If there are any disabled renderers then change _renderers to cached values.
            if (enabledRenderers.Count != _renderers.Length)
                _renderers = enabledRenderers.ToArray();

            if (updateVisibility)
                UpdateRenderVisibility(_lastClientHostVisibility);
        }

        /// <summary>
        /// Updates visibilites on renders without checks.
        /// </summary>
        /// <param name="visible"></param>
        private void UpdateRenderVisibility(bool visible)
        {
            bool rebuildRenderers = false;

            Renderer[] rs = _renderers;
            int count = rs.Length;
            for (int i = 0; i < count; i++)
            {
                Renderer r = rs[i];
                if (r == null)
                {
                    rebuildRenderers = true;
                    break;
                }

                r.enabled = visible;
            }

            OnHostVisibilityUpdated?.Invoke(_lastClientHostVisibility, visible);
            _lastClientHostVisibility = visible;

            //If to rebuild then do so, while updating visibility.
            if (rebuildRenderers)
                UpdateRenderers(true);
        }

        /// <summary>
        /// Adds the default NetworkObserver conditions using the ObserverManager.
        /// </summary>
        private void AddDefaultNetworkObserverConditions()
        {
            if (_networkObserverInitiliazed)
                return;

            NetworkObserver = NetworkManager.ObserverManager.AddDefaultConditions(this);
        }


        /// <summary>
        /// Removes a connection from observers for this object returning if the connection was removed.
        /// </summary>
        /// <param name="connection"></param>
        internal bool RemoveObserver(NetworkConnection connection)
        {
            int startCount = Observers.Count;
            bool removed = Observers.Remove(connection);
            if (removed)
                TryInvokeOnObserversActive(startCount);

            return removed;
        }

        /// <summary>
        /// Adds the connection to observers if conditions are met.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns>True if added to Observers.</returns>
        internal ObserverStateChange RebuildObservers(NetworkConnection connection, bool timedOnly)
        {
            //If not a valid connection.
            if (!connection.IsValid)
            {
                NetworkManager.LogWarning($"An invalid connection was used when rebuilding observers.");
                return ObserverStateChange.Unchanged;
            }
            //Valid not not active.
            else if (!connection.IsActive)
            {
                /* Just remove from observers since connection isn't active
                 * and return unchanged because nothing should process
                 * given the connection isnt active. */
                Observers.Remove(connection);
                return ObserverStateChange.Unchanged;
            }
            else if (IsDeinitializing)
            {
                /* If object is deinitializing it's either being despawned
                 * this frame or it's not spawned. If we've made it this far,
                 * it's most likely being despawned. */
                return ObserverStateChange.Unchanged;
            }

            //Update hashgrid if needed.
            UpdateForNetworkObject(!timedOnly);

            int startCount = Observers.Count;
            ObserverStateChange osc = NetworkObserver.RebuildObservers(connection, timedOnly);

            if (osc == ObserverStateChange.Added)
                Observers.Add(connection);
            else if (osc == ObserverStateChange.Removed)
                Observers.Remove(connection);

            if (osc != ObserverStateChange.Unchanged)
                TryInvokeOnObserversActive(startCount);

            return osc;
        }

        /// <summary>
        /// Invokes OnObserversActive if observers are now 0 but previously were not, or if was previously 0 but now has observers.
        /// </summary>
        /// <param name="startCount"></param>
        private void TryInvokeOnObserversActive(int startCount)
        {
            if ((Observers.Count > 0 && startCount == 0) ||
                Observers.Count == 0 && startCount > 0)
                OnObserversActive?.Invoke(this);
        }

    }

}

