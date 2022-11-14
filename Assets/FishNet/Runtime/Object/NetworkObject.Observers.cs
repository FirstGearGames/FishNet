using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Observing;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object
{
    public sealed partial class NetworkObject : MonoBehaviour
    {
        #region Public.
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
        public HashSet<NetworkConnection> Observers = new HashSet<NetworkConnection>();
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
        #endregion

        /// <summary>
        /// Updates cached renderers used to managing clientHost visibility.
        /// </summary>
        /// <param name="updateVisibility">True to also update visibility if clientHost.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateRenderers(bool updateVisibility = true)
        {
            UpdateRenderersInternal(updateVisibility);
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
                UpdateRenderersInternal(false);
                _renderersPopulated = true;
            }

            UpdateRenderVisibility(visible);
        }

        /// <summary>
        /// Clears and updates renderers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateRenderersInternal(bool updateVisibility)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
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
                if (NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"An invalid connection was used when rebuilding observers.");
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

