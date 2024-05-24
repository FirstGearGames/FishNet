using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Utility.Template
{

    /// <summary>
    /// Subscribes to tick events making them available as virtual methods.
    /// </summary>
    public abstract class TickNetworkBehaviour : NetworkBehaviour
    {
        #region Types.
        [System.Flags]
        [System.Serializable]
        private enum TickCallback : int
        {
            None = 0,
            PreTick = 1,
            Tick = 2,
            PostTick = 4,
            All = ~0,
        }
        #endregion

        /// <summary>
        /// Tick callbacks to use.
        /// </summary>
        [Tooltip("Tick callbacks to use.")]
        [SerializeField]
        private TickCallback _tickCallbacks = (TickCallback.Tick | TickCallback.PostTick);

        /// <summary>
        /// Last subscription state.
        /// </summary>
        private bool _subscribed;

        internal override void OnStartNetwork_Internal()
        {
            base.OnStartNetwork_Internal();
            ChangeSubscriptions(true);
        }

        internal override void OnStopNetwork_Internal()
        {
            base.OnStopNetwork_Internal();
            ChangeSubscriptions(false);
        }

        private void ChangeSubscriptions(bool subscribe)
        {
            TimeManager tm = base.TimeManager;
            if (tm == null)
                return;
            if (subscribe == _subscribed)
                return;
            _subscribed = subscribe;

            if (subscribe)
            {
                if (TickCallbackContains(_tickCallbacks, TickCallback.PreTick))
                        tm.OnPreTick += TimeManager_OnPreTick;
                if (TickCallbackContains(_tickCallbacks, TickCallback.Tick))
                    tm.OnTick += TimeManager_OnTick;
                if (TickCallbackContains(_tickCallbacks, TickCallback.PostTick))
                    tm.OnPostTick += TimeManager_OnPostTick;
            }
            else
            {
                if (TickCallbackContains(_tickCallbacks, TickCallback.PreTick))
                    tm.OnPreTick -= TimeManager_OnPreTick;
                if (TickCallbackContains(_tickCallbacks, TickCallback.Tick))
                    tm.OnTick -= TimeManager_OnTick;
                if (TickCallbackContains(_tickCallbacks, TickCallback.PostTick))
                    tm.OnPostTick -= TimeManager_OnPostTick;
            }
        }
        protected virtual void TimeManager_OnPreTick() { }
        protected virtual void TimeManager_OnTick() { }
        protected virtual void TimeManager_OnPostTick() { }
        private bool TickCallbackContains(TickCallback whole, TickCallback part) => ((whole & part) == part);
    }
}