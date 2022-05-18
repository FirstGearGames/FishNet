using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Observing;
using System;
using System.Collections.Generic;
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
        /// NetworkObserver on this object. May be null if not using observers.
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
        #endregion

        /// <summary>
        /// Sets the renderer visibility for clientHost.
        /// </summary>
        /// <param name="visible"></param>
        internal void SetHostVisibility(bool visible)
        {
            /* If renderers are not set then the object
            * was never despawned. This means the renderers
            * could not possibly be hidden. */
            if (visible && !_renderersPopulated)
                return;

            if (!visible && !_renderersPopulated)
            { 
                _renderers = GetComponentsInChildren<Renderer>(true);
                _renderersPopulated = true;
            }

            Renderer[] rs = _renderers;
            int count = rs.Length;
            for (int i = 0; i < count; i++)
                rs[i].enabled = visible;
        }

        /// <summary>
        /// Adds the default NetworkObserver conditions using the ObserverManager.
        /// </summary>
        private void AddDefaultNetworkObserverConditions()
        {
            if (_networkObserverInitiliazed)
                return;

            NetworkObserver = GetComponent<NetworkObserver>();
            NetworkManager.ObserverManager.AddDefaultConditions(this, ref NetworkObserver);
        }
        /// <summary>
        /// Initializes NetworkObserver. This will only call once even as host.
        /// </summary>
        private void InitializeOnceObservers()
        {
            if (_networkObserverInitiliazed)
                return;

            if (NetworkObserver != null)
                NetworkObserver.PreInitialize(this);

            _networkObserverInitiliazed = true;
        }

        /// <summary>
        /// Removes a connection from observers for this object.
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

            int startCount = Observers.Count;
            //Not using observer system, this object is seen by everything.
            if (NetworkObserver == null)
            {
                bool added = Observers.Add(connection);
                if (added)
                    TryInvokeOnObserversActive(startCount);

                return (added) ? ObserverStateChange.Added : ObserverStateChange.Unchanged;
            }
            else
            {
                ObserverStateChange osc = NetworkObserver.RebuildObservers(connection, timedOnly);
                if (osc == ObserverStateChange.Added)
                    Observers.Add(connection);
                else if (osc == ObserverStateChange.Removed)
                    Observers.Remove(connection);

                if (osc != ObserverStateChange.Unchanged)
                    TryInvokeOnObserversActive(startCount);

                return osc;
            }

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

