using FishNet.Documenting;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using System.Runtime.CompilerServices;
using FishNet.Managing;
using UnityEngine;

namespace FishNet.Example.ComponentStateSync
{
    /// <summary>
    /// It's very important to exclude this from codegen.
    /// However, whichever value you are synchronizing must not be excluded. This is why the value is outside the StructySync class.
    /// </summary>
    public class ComponentStateSync<T> : SyncBase, ICustomSync where T : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Gets or Sets the enabled state for Component.
        /// </summary>
        public bool Enabled
        {
            get => (Component == null) ? false : GetState();
            set => SetState(value);
        }
        /// <summary>
        /// Component to state sync.
        /// </summary>
        public T Component { get; private set; }

        /// <summary>
        /// Delegate signature for when the component changes.
        /// </summary>
        public delegate void StateChanged(T component, bool prevState, bool nextState, bool asServer);

        /// <summary>
        /// Called when the component state changes.
        /// </summary>
        public event StateChanged OnChange;
        #endregion

        /// <summary>
        /// Initializes this StateSync with a component.
        /// </summary>
        /// <param name="monoComponent"></param>
        public void Initialize(T component)
        {
            Component = component;
        }

        /// <summary>
        /// Sets the enabled state for Component.
        /// </summary>
        /// <param name="enabled"></param>
        private void SetState(bool enabled)
        {
            if (base.NetworkManager == null)
                return;

            if (Component == null)
                base.NetworkManager.LogError($"State cannot be changed as Initialize has not been called with a valid component.");

            //If hasn't changed then ignore.
            bool prev = GetState();
            if (enabled == prev)
                return;

            //Set to new value and add operation.
            Component.enabled = enabled;
            AddOperation(Component, prev, enabled);
        }

        /// <summary>
        /// Gets the enabled state for Component.
        /// </summary>
        /// <returns></returns>
        private bool GetState()
        {
            return Component.enabled;
        }

        /// <summary>
        /// Adds an operation to synchronize.
        /// </summary>
        private void AddOperation(T component, bool prev, bool next)
        {
            if (!base.IsInitialized)
                return;

            if (base.NetworkManager != null && !base.NetworkBehaviour.IsServerStarted)
            {
                NetworkManager.LogWarning($"Cannot complete operation as server when server is not active.");
                return;
            }

            base.Dirty();

            //Data can currently only be set from server, so this is always asServer.
            bool asServer = true;
            OnChange?.Invoke(component, prev, next, asServer);
        }

        /// <summary>
        /// Writes all changed values.
        /// </summary>
        ///<param name="resetSyncTick">True to set the next time data may sync.</param>
        protected internal override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            base.WriteDelta(writer, resetSyncTick);
            writer.WriteBoolean(Component.enabled);
        }

        /// <summary>
        /// Writes all values.
        /// </summary>
        protected internal override void WriteFull(PooledWriter writer)
        {
            /* Always write full for this custom sync type.
             * It would be difficult to know if the
             * state has changed given it's a boolean, and
             * may or may not be true/false after pooling is added. */
            WriteDelta(writer, false);
        }

        /// <summary>
        /// Reads and sets the current values for server or client.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [APIExclude]
        protected internal override void Read(PooledReader reader, bool asServer)
        {
            base.SetReadArguments(reader, asServer, out bool newChangeId, out bool _, out bool canModifyValues);
            
            bool prevValue = GetState();
            bool nextValue = reader.ReadBoolean();

            /* When !asServer don't make changes if server is running.
             * This is because changes would have already been made on
             * the server side and doing so again would result in duplicates
             * and potentially overwrite data not yet sent. */
            if (canModifyValues)
                Component.enabled = nextValue;

            if (newChangeId)
                OnChange?.Invoke(Component, prevValue, nextValue, asServer);
        }

        /// <summary>
        /// Return the serialized type.
        /// </summary>
        /// <returns></returns>
        public object GetSerializedType() => null;
    }
}