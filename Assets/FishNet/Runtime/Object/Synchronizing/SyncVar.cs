using FishNet.CodeGenerating;
using FishNet.Documenting;
using FishNet.Object.Helping;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishNet.Object.Synchronizing
{
    [APIExclude]
    [System.Serializable]
    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
    public class SyncVar<T> : SyncBase
    {
        #region Types.
        /// <summary>
        /// Information needed to invoke a callback.
        /// </summary>
        private struct CachedOnChange
        {
            internal readonly T Previous;
            internal readonly T Next;

            public CachedOnChange(T previous, T next)
            {
                Previous = previous;
                Next = next;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Gets and sets the current value for this SyncVar.
        /// </summary>
        public T Value
        {
            get => _value;
            set => SetValue(value, true);
        }
        ///// <summary>
        ///// Sets the current value for this SyncVar while sending it immediately.
        ///// </summary>
        //public T ValueRpc
        //{
        //    set => SetValue(value, true, true);
        //}
        ///// <summary>
        ///// Gets the current value for this SyncVar while marking it dirty. This could be useful to change properties or fields on a reference type SyncVar and have the SyncVar be dirtied after.
        ///// </summary>
        //public T ValueDirty
        //{
        //    get
        //    {
        //        base.Dirty();
        //        return _value;
        //    }
        //}
        ///// <summary>
        ///// Gets the current value for this SyncVar while sending it imediately. This could be useful to change properties or fields on a reference type SyncVar and have the SyncVar send after.
        ///// </summary>
        //public T ValueDirtyRpc
        //{
        //    get
        //    {
        //        base.Dirty(true);
        //        return _value;
        //    }
        //}
        /// <summary>
        /// Called when the SyncDictionary changes.
        /// </summary>
        public event OnChanged OnChange;
        public delegate void OnChanged(T prev, T next, bool asServer);
        #endregion

        #region Private.
        /// <summary>
        /// Server OnChange event waiting for start callbacks.
        /// </summary>
        private CachedOnChange? _serverOnChange;
        /// <summary>
        /// Client OnChange event waiting for start callbacks.
        /// </summary>
        private CachedOnChange? _clientOnChange;
        /// <summary>
        /// Value before the network is initialized on the containing object.
        /// </summary>
        private T _initialValue;
        /// <summary>
        /// Previous value on the client.
        /// </summary>
        private T _previousClientValue;
        /// <summary>
        /// Current value on the server, or client.
        /// </summary>
        [SerializeField]
        private T _value;
        #endregion

        #region Constructors.
        public SyncVar(SyncTypeSettings settings = new SyncTypeSettings()) : this(default(T), settings) { }
        public SyncVar(T initialValue, SyncTypeSettings settings = new SyncTypeSettings()) : base(settings) => SetInitialValues(initialValue);
        #endregion

        /// <summary>
        /// Called when the SyncType has been registered, but not yet initialized over the network.
        /// </summary>
        protected override void Initialized()
        {
            base.Initialized();
            _initialValue = _value;
        }

        /// <summary>
        /// Sets initial values.
        /// Initial values are not automatically synchronized, as it is assumed clients and server already have them set to the specified value.
        /// When a SyncVar is reset, such as when the object despawns, current values are set to initial values.
        /// </summary>
        public void SetInitialValues(T value)
        {
            _initialValue = value;
            UpdateValues(value);
        }
        /// <summary>
        /// Sets current and previous values.
        /// </summary>
        /// <param name="next"></param>
        private void UpdateValues(T next)
        {
            _previousClientValue = next;
            _value = next;
        }
        /// <summary>
        /// Sets current value and marks the SyncVar dirty when able to. Returns if able to set value.
        /// </summary>
        /// <param name="calledByUser">True if SetValue was called in response to user code. False if from automated code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetValue(T nextValue, bool calledByUser, bool sendRpc = false)
        {
            /* If not registered then that means Awake
             * has not completed on the owning class. This would be true
             * when setting values within awake on the owning class. Registered
             * is called at the end of awake, so it would be unset until awake completed.
             * 
             * Registered however will be true when setting from another script,
             * even if the owning class of this was just spawned. This is because
             * the unity cycle will fire awake on the object soon as it's spawned, 
             * completing awake, and the user would set the value after. */
            if (!base.IsInitialized)
                return;

            /* If not client or server then set skipChecks
             * as true. When neither is true it's likely user is changing
             * value before object is initialized. This is allowed
             * but checks cannot be processed because they would otherwise
             * stop setting the value. */
            bool isNetworkInitialized = base.IsNetworkInitialized;

            //Object is deinitializing.
            if (isNetworkInitialized && CodegenHelper.NetworkObject_Deinitializing(this.NetworkBehaviour))
                return;

            //If being set by user code.
            if (calledByUser)
            {
                if (!base.CanNetworkSetValues(true))
                    return;

                /* We will only be this far if the network is not active yet,
                 * server is active, or client has setting permissions. 
                 * We only need to set asServerInvoke to false if the network
                 * is initialized and the server is not active. */
                bool asServerInvoke = (!isNetworkInitialized || base.NetworkBehaviour.IsServerStarted);

                /* If the network has not been network initialized then
                 * Value is expected to be set on server and client since
                 * it's being set before the object is initialized. */
                if (!isNetworkInitialized)
                {
                    T prev = _value;
                    UpdateValues(nextValue);
                    //Still call invoke because change will be cached for when the network initializes.
                    InvokeOnChange(prev, _value, calledByUser);
                }
                else
                {
                    if (Comparers.EqualityCompare<T>(_value, nextValue))
                        return;

                    T prev = _value;
                    _value = nextValue;
                    InvokeOnChange(prev, _value, asServerInvoke);
                }

                TryDirty(asServerInvoke);
            }
            //Not called by user.
            else
            {
                /* Previously clients were not allowed to set values
                 * but this has been changed because clients may want
                 * to update values locally while occasionally
                 * letting the syncvar adjust their side. */
                T prev = _previousClientValue;
                if (Comparers.EqualityCompare<T>(prev, nextValue))
                    return;

                /* If also server do not update value.
                 * Server side has say of the current value. */
                if (!base.NetworkManager.IsServerStarted)
                    UpdateValues(nextValue);
                else
                    _previousClientValue = nextValue;

                InvokeOnChange(prev, nextValue, calledByUser);
            }


            /* Tries to dirty so update
             * is sent over network. This needs to be called
             * anytime the data changes because there is no way
             * to know if the user set the value on both server
             * and client or just one side. */
            void TryDirty(bool asServer)
            {
                //Cannot dirty when network is not initialized.
                if (!isNetworkInitialized)
                    return;

                if (asServer)
                    base.Dirty();
                //base.Dirty(sendRpc);
            }
        }

        /// <summary>
        /// Invokes OnChanged callback.
        /// </summary>
        private void InvokeOnChange(T prev, T next, bool asServer)
        {
            if (asServer)
            {
                if (base.NetworkBehaviour.OnStartServerCalled)
                    OnChange?.Invoke(prev, next, asServer);
                else
                    _serverOnChange = new CachedOnChange(prev, next);
            }
            else
            {
                if (base.NetworkBehaviour.OnStartClientCalled)
                    OnChange?.Invoke(prev, next, asServer);
                else
                    _clientOnChange = new CachedOnChange(prev, next);
            }
        }

        /// <summary>
        /// Called after OnStartXXXX has occurred.
        /// </summary>
        /// <param name="asServer">True if OnStartServer was called, false if OnStartClient.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [MakePublic]
        internal protected override void OnStartCallback(bool asServer)
        {
            base.OnStartCallback(asServer);

            if (OnChange != null)
            {
                CachedOnChange? change = (asServer) ? _serverOnChange : _clientOnChange;
                if (change != null)
                    InvokeOnChange(change.Value.Previous, change.Value.Next, asServer);
            }

            if (asServer)
                _serverOnChange = null;
            else
                _clientOnChange = null;
        }

        /// <summary>
        /// Writes current value.
        /// </summary>
        /// <param name="resetSyncTick">True to set the next time data may sync.</param>
        [MakePublic]
        internal protected override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
        {
            base.WriteDelta(writer, resetSyncTick);
            writer.Write<T>(_value);
        }

        /// <summary>
        /// Writes current value if not initialized value.
        /// </summary>m>
        [MakePublic]
        internal protected override void WriteFull(PooledWriter obj0)
        {
            if (Comparers.EqualityCompare<T>(_initialValue, _value))
                return;
            /* SyncVars only hold latest value, so just
             * write current delta. */
            WriteDelta(obj0, false);
        }

        /// <summary>
        /// Reads a SyncVar value.
        /// </summary>
        protected internal override void Read(PooledReader reader, bool asServer)
        {
            T value = reader.Read<T>();
            SetValue(value, false);
        }

        /// <summary>
        /// Resets to initialized values.
        /// </summary>
        [MakePublic]
        internal protected override void ResetState()
        {
            base.ResetState();
            _value = _initialValue;
            _previousClientValue = _initialValue;
        }
    }
}


