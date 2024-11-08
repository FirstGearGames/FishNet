using FishNet.Managing.Timing;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
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
        public enum TickCallback : uint
        {
            None = 0,
            PreTick = (1 << 0),
            Tick = (1 << 1),
            PostTick = (1 << 2),
            Update = (1 << 3),
            LateUpdate = (1 << 4),
            Everything = Enums.SHIFT_EVERYTHING_UINT,
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

        /// <summary>
        /// Updates callbacks to use and changes subscriptions accordingly.
        /// </summary>
        /// <param name="value">Next value.</param>
        public void SetTickCallbacks(TickCallback value)
        {
            ChangeSubscriptions(subscribe: false);
            _tickCallbacks = value;
            ChangeSubscriptions(subscribe: true);            
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
                if (TickCallbackContains(_tickCallbacks, TickCallback.Update))
                    tm.OnUpdate += TimeManager_OnUpdate;
                if (TickCallbackContains(_tickCallbacks, TickCallback.LateUpdate))
                    tm.OnUpdate += TimeManager_OnLateUpdate;
            }
            else
            {
                if (TickCallbackContains(_tickCallbacks, TickCallback.PreTick))
                    tm.OnPreTick -= TimeManager_OnPreTick;
                if (TickCallbackContains(_tickCallbacks, TickCallback.Tick))
                    tm.OnTick -= TimeManager_OnTick;
                if (TickCallbackContains(_tickCallbacks, TickCallback.PostTick))
                    tm.OnPostTick -= TimeManager_OnPostTick;
                if (TickCallbackContains(_tickCallbacks, TickCallback.Update))
                    tm.OnUpdate -= TimeManager_OnUpdate;
                if (TickCallbackContains(_tickCallbacks, TickCallback.LateUpdate))
                    tm.OnUpdate -= TimeManager_OnLateUpdate;
            }
        }

        protected virtual void TimeManager_OnPreTick() { }
        protected virtual void TimeManager_OnTick() { }
        protected virtual void TimeManager_OnPostTick() { }
        protected virtual void TimeManager_OnUpdate() { }
        protected virtual void TimeManager_OnLateUpdate() { }

        private bool TickCallbackContains(TickCallback whole, TickCallback part) => ((whole & part) == part);
    }
}